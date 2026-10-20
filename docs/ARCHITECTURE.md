# Arquitectura

## Capas

```
Kumquat.App ─────────────┐   bandeja, preferencias, menú contextual; une rueda ↔ ventanas ↔ cola
Kumquat.Cli ─────────────┤   misma lógica sin UI
                         ▼
Kumquat.Hosting              composición DI (motores, cola, logging), PreviewService
   │            │
   ▼            ▼
Kumquat.Interaction        Kumquat.Dialogs             Kumquat.Engines.*          Kumquat.Infrastructure
 WheelSession (lógica)      ventana de opciones (WPF)   FFmpeg, MuPDF, qpdf,       ProcessRunner + Job Object
 WheelController (Win32)    vistas previas, reproductor Tesseract, Office (proc.)  WaveOutPlayer, TempWorkspace
 hooks LL, OLE, atajo       sincronizadas con campos    libvips (en proceso)       SafeExtractor, EngineLocator
   │        │                  │                            │                          │
   │        └──── Kumquat.Design: íconos, trazados SVG, paleta, Inter, logo (sin UI) ───┤
   └───────────────────────────┴────────────────────────────┴──────────────────────────┘
                   ▼
             Kumquat.Core   (sin dependencias de Windows: net10.0)
             formatos + sniffer, grafo de capacidades, política, herramientas,
             ConversionService / StagedOutput (publica atómico), JobRunner, contratos de vista previa
```

`Kumquat.Core` no conoce Windows, ni motores, ni UI: se prueba en cualquier runner.

**Binarios:** toda librería es IL puro (`Directory.Build.targets` fija `PlatformTarget=AnyCPU`); la
arquitectura la deciden los ejecutables. Una librería compilada para una arquitectura concreta en la
carpeta de salida compartida no carga en un proceso de otra (lo vigila `ArquitecturaTests`).

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
   - **Archivos virtuales** (adjuntos de Outlook, contenido de un .zip, imágenes de una web): sin
     `CF_HDROP`, `VirtualDrops` lee los nombres de `FileGroupDescriptorW` en `DragEnter` (los anillos
     salen de la extensión) y recién en `Drop` copia `FileContents` (HGLOBAL o IStream) a una carpeta
     temporal: el contenido solo existe durante el drop y copiarlo antes dejaría al programa de origen
     esperando. Los nombres se limpian (sin carpetas ni `..\`), con límites de cantidad y tamaño. Los
     resultados van a la carpeta de Configuración y las copias se borran al terminar el lote.
5. Píxeles con alfa 0 dejan pasar el mouse: el disco se pinta opaco y lo de afuera, transparente.

Shift, Ctrl y Alt ya significan algo en el protocolo de arrastre de Windows. Kumquat usa Shift por defecto (como Tangerine) **sin interceptarlo**; como destino decide el efecto, así que la semántica de "mover" no aplica. Alternativas configurables: Ctrl, Bloq Mayús, clic derecho.

Kumquat corre sin elevación: UIPI bloquea drops desde Explorer hacia procesos elevados.

### Lógica separada de Win32

`WheelSession` decide todo (mostrar, resaltar, esperar el drop, elegir, cancelar) sobre una
`IWheelSurface`; `WheelController` solo traduce hooks, OLE, `WM_HOTKEY` y `WM_COPYDATA`. Así la
lógica se prueba con una superficie falsa y la geometría real (`WheelSessionTests`).

### Abrir la rueda sin arrastrar

- **Atajo Ctrl+Alt+K** (`RegisterHotKey`): `ExplorerSelection` lee la selección del Explorador en
  primer plano con las interfaces documentadas del shell — `IShellWindows` → `IShellBrowser` de la
  pestaña activa (`ShellTabWindowClass` en Windows 11) → `IShellView.GetItemObject(SVGIO_SELECTION)`
  → `IDataObject`, que lee el mismo `DataObjectReader` del drop. También el escritorio.
- **Buscador** (`CommandPalette`): el atajo y el menú contextual lo abren en vez de la rueda (que queda
  para el arrastre). Filtra con `CommandSearch` (sin tildes, prefijos, inicio de palabra, palabras
  clave, letras en orden) y devuelve el mismo id que un sector de la rueda. Si no hay buscador, la
  rueda se abre en **modo clic** (el hook informa `PointerMoved` y soltar el botón es el clic).
- **Primer plano:** el proceso que lanza el Explorador desde el menú contextual le cede el permiso
  (`AllowSetForegroundWindow`) a la instancia, para que el buscador aparezca adelante y reciba el teclado.
- **Menú contextual** (clásico, HKCU, sin administrador): el Explorador lanza `Kumquat.exe --wheel
  archivo`, uno por archivo seleccionado. El proceso nuevo pasa la ruta a la instancia en ejecución
  por `WM_COPYDATA` (con firma propia) y sale; la instancia junta los pedidos 250 ms y, si son parte
  de la selección del Explorador, abre la rueda una vez con la selección completa.

## Diseño

`Kumquat.Design` no depende de UI: íconos Phosphor (MIT) como trazados SVG generados por
`tools/build-design-assets.py`, un intérprete de trazados que reduce arcos y cuadráticas a Béziers
cúbicas (W3C SVG 1.1, F.6) para que GDI+ (rueda) y WPF (ventanas) dibujen lo mismo, la paleta por
familia de herramientas y la tipografía Inter (OFL) cargada desde archivos, sin instalarla.

La rueda simula vidrio (una ventana en capas no puede desenfocar lo de atrás): degradado
translúcido, borde de luz, sombra y sectores con esquinas redondeadas calculadas exactamente
(`SectorGeometry`). Las ventanas usan el material acrílico real de Windows 11.

## Ventana de opciones y vistas previas

- La ventana aparece enseguida; `PreviewService` genera la vista previa en segundo plano en una
  carpeta temporal propia: miniaturas de páginas (MuPDF), tira de fotogramas y forma de onda
  (FFmpeg), miniatura de fotos (libvips).
- Las vistas previas se sincronizan con los campos: páginas elegidas ⇄ "Páginas", asas ⇄
  "Desde/Hasta", cabezal ⇄ "Instante".
- **Reproductor propio** (`FfmpegPlayback`): un FFmpeg decodifica el video a BGRA y otro el audio a
  PCM, que suena por `waveOut` (`WaveOutPlayer`). El reloj es el audio que ya sonó; los fotogramas
  atrasados se descartan. Al mover un asa se pide el fotograma exacto, atendiendo solo el último
  pedido (nunca hay más de un FFmpeg a la vez).
- Errores inesperados de estas tareas se registran (`DialogDiagnostics`) y la ventana sigue usable.

## Decisiones y alternativas descartadas

| Decisión | Por qué | Descartado |
|---|---|---|
| C# / .NET 10 LTS | Soporte hasta nov. 2028; P/Invoke y COM generados en compilación | .NET 8/9 (fin de soporte nov. 2026) |
| Rueda en HWND nativo + GDI+ | Transparencia por píxel, sin foco, destino OLE pre-registrado | WinUI 3 (compone mal con `WS_EX_LAYERED`), Tauri/Electron (WebView2 registra el drop tarde) |
| Motores fuera de proceso | Aislar crashes/exploits, cancelar de verdad, límites de memoria | Enlazar FFmpeg en proceso |
| libvips en proceso | Madura, fuzzeada, rápida; caché desactivada para no retener archivos | ImageMagick (superficie de ataque mayor) |
| GPL-3.0-or-later | Permite x264/x265, MuPDF, Ghostscript, Calibre; impide forks cerrados | MIT |
| Reproducir con FFmpeg + waveOut | Cualquier formato que Kumquat acepta; fotograma exacto; sin componentes opcionales de Windows | `MediaElement` (depende del Reproductor de Windows heredado y de sus códecs: no abre MKV, WebM, Opus…) |
| OCR con Tesseract compilado por Kumquat | PDF buscable nativo con texto Unicode, más de 100 idiomas, mismo resultado en toda PC y en CI | OCR de Windows (depende de los idiomas instalados; el texto del PDF habría que armarlo a mano) |
| Office: usar lo instalado (Microsoft Office, si no LibreOffice) | Fidelidad máxima con Word/Excel/PowerPoint; sin cargar ~350 MB en el paquete | Distribuir LibreOffice (enorme); convertir con librerías .NET (pierden diseño) |
| Libros con Calibre instalado | El conversor de libros más completo (Kindle, FB2); GPL como Kumquat | Distribuirlo (~240 MB); librerías .NET (no escriben KF8) |
| Office por COM en un PowerShell aparte | Si Office se cuelga o abre un diálogo, Kumquat no se traba: se cierra solo esa instancia | COM desde el proceso de Kumquat |
| Rueda accesible con un proveedor UI Automation propio | La rueda nunca toma el foco (no puede: interrumpiría el arrastre); `UiaRaiseNotificationEvent` le permite al lector decir lo que pasa sin robar el foco | MSAA/`NotifyWinEvent` (sin texto libre); ventana WPF para la rueda (compone mal con `WS_EX_LAYERED`) |
| Menú contextual clásico (HKCU) | Sin administrador ni paquete; funciona desde Windows 10 | `IExplorerCommand` del menú nuevo de Windows 11 (exige app empaquetada y firmada: llegará con el instalador) |

Investigación completa: [docs/research](research/).
