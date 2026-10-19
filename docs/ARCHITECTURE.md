# Arquitectura

## Capas

```
Kumquat.App ─────────────┐   bandeja, preferencias, une rueda ↔ cola de trabajos
Kumquat.Cli ─────────────┤   misma lógica sin UI
                         ▼
Kumquat.Hosting              composición DI (motores, cola, logging)
   │            │
   ▼            ▼
Kumquat.Interaction        Kumquat.Engines.*            Kumquat.Infrastructure
 rueda (HWND en capas)      FFmpeg (proceso aparte)      ProcessRunner + Job Object
 IDropTarget (COM)          libvips (en proceso)         TempWorkspace, SafeExtractor
 hooks LL, bandeja          …                            EngineLocator
   │                           │                            │
   └───────────────┬───────────┴────────────────────────────┘
                   ▼
             Kumquat.Core   (sin dependencias de Windows: net10.0)
             formatos + sniffer, grafo de capacidades, política,
             ConversionService (planifica, encadena, publica atómico), JobRunner
```

`Kumquat.Core` no conoce Windows, ni motores, ni UI: se prueba en cualquier runner.

## El grafo de capacidades

- **Nodos** = formatos (`KnownFormats`), detectados por **bytes mágicos**; la extensión solo desambigua familias (ZIP vs DOCX, ASF vs WMA/WMV).
- **Aristas** = conversiones directas que declara cada `IConverter`, con un **costo** que combina tiempo y pérdida de calidad.
- **Rutas**: Dijkstra sobre el estado *(formato, saltos)* con máximo 3 saltos. Si dos motores ofrecen la misma arista, gana la más barata.
- **Política** (`ConversionPolicy`): filtra destinos finales sin sentido (PNG→GIF→MP4→MP3 es una ruta válida pero nadie quiere un MP3 de una foto).
- La rueda consulta `CommonTargets` (intersección para varios archivos) y `WheelItemOrdering` decide posiciones estables: los 4 favoritos de cada familia en los ejes cardinales (más rápidos y precisos según Kurtenbach & Buxton).

## Ejecución de una conversión

1. `Plan`: detectar formato real → buscar ruta → errores esperados como `Result` (`unknown-format`, `no-route`…).
2. Pasos intermedios en una carpeta temporal propia (`%TEMP%\Kumquat\<guid>`), borrada al terminar; restos de crashes se limpian al arrancar.
3. El último paso escribe un `.kumquat-<guid>.<ext>` oculto **en la carpeta destino** y se publica con `File.Move` (atómico en el mismo volumen) al primer nombre libre (`foto.png`, `foto (2).png`…). Nunca hay archivos a medias con el nombre final y nunca se sobrescribe.
4. Cancelación: el token llega hasta el motor; `ProcessRunner` mata el Job Object completo (incluidos procesos nietos).

## La interacción (lo difícil)

Windows no avisa "empezó un arrastre". La solución:

1. `GlobalInputHook` (hilo propio) observa `WH_MOUSE_LL`/`WH_KEYBOARD_LL`. El callback solo alimenta `DragGestureTracker` (máquina de estados pura) y publica un mensaje: Windows elimina en silencio hooks que tardan más de `LowLevelHooksTimeout`. Nunca se traga eventos ni registra pulsaciones.
2. La rueda (`WheelWindow`) es una ventana `WS_EX_LAYERED | TOPMOST | NOACTIVATE | TOOLWINDOW` **creada al arrancar y registrada con `RegisterDragDrop` una sola vez**, visible pero estacionada fuera de la pantalla. Al detectar el gesto se mueve bajo el cursor sin activarse. `DoDragDrop` reevalúa la ventana bajo el cursor en cada iteración, así que la rueda se convierte en destino.
   - Una ventana mostrada por primera vez a mitad del arrastre no recibe el drop (caso documentado con WebView2), y tomar el foco rompe el bucle de arrastre de Explorer: por eso no se usa WinUI/WebView para la rueda.
3. `DropTarget` (COM generado en compilación) lee `CF_HDROP` llamando a la vtable de `IDataObject`. **Siempre responde COPY (o LINK), nunca MOVE**: Explorer podría borrar el original tras un "movimiento optimizado".
4. Hit-test polar (`WheelLayout`): ángulo → sector, radio → centro (cancelar) o fuera.
5. Píxeles con alfa 0 dejan pasar el mouse: el disco se pinta opaco y lo de afuera, transparente.

Shift, Ctrl y Alt ya significan algo en el protocolo de arrastre de Windows. Kumquat usa Shift por defecto (como Tangerine) **sin interceptarlo**; como destino decide el efecto, así que la semántica de "mover" no aplica. Alternativas configurables: Ctrl, Bloq Mayús, clic derecho.

Kumquat corre sin elevación: UIPI bloquea drops desde Explorer hacia procesos elevados.

## Decisiones y alternativas descartadas

| Decisión | Por qué | Descartado |
|---|---|---|
| C# / .NET 10 LTS | Soporte hasta nov. 2028; P/Invoke y COM generados en compilación | .NET 8/9 (fin de soporte nov. 2026) |
| Rueda en HWND nativo + GDI+ | Transparencia por píxel, sin foco, destino OLE pre-registrado | WinUI 3 (compone mal con `WS_EX_LAYERED`), Tauri/Electron (WebView2 registra el drop tarde) |
| Motores fuera de proceso | Aislar crashes/exploits, cancelar de verdad, límites de memoria | Enlazar FFmpeg en proceso |
| libvips en proceso | Madura, fuzzeada, rápida; caché desactivada para no retener archivos | ImageMagick (superficie de ataque mayor) |
| GPL-3.0-or-later | Permite x264/x265, MuPDF, Ghostscript, Calibre; impide forks cerrados | MIT |

Investigación completa: [docs/research](research/).
