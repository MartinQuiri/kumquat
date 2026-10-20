# Pruebas manuales de la interacción

Los tests automáticos cubren la lógica y un arrastre OLE sintético (`tests/Kumquat.E2E.Tests`). El comportamiento con aplicaciones reales hay que verificarlo a mano antes de cada release y en cada PR que toque `Kumquat.Interaction`.

Ejecuta `dotnet run --project src/Kumquat.App` y anota ✅/❌ y la versión de Windows.

## Fuentes de arrastre

| # | Fuente | Pasos | Esperado |
|---|---|---|---|
| 1 | Explorador, un PNG | Arrastrar, Shift, soltar en JPG | `foto.jpg` junto al original; original intacto |
| 2 | Explorador, 5 WAV | Arrastrar selección, Shift, MP3 | 5 MP3; una sola notificación "5 archivos convertidos" |
| 3 | Escritorio | Igual que 1 | Igual |
| 4 | Chrome/Edge (imagen de una página) | Arrastrar imagen, Shift, soltar en PNG | El PNG aparece en la carpeta de Configuración (Escritorio); la etiqueta lo anunciaba |
| 5 | Outlook (adjunto PDF) | Arrastrar adjunto, Shift, "Comprimir" | El PDF comprimido aparece en el Escritorio; el correo no cambia |
| 6 | Carpeta ZIP abierta en Explorer | Arrastrar un archivo de adentro, "Guardar en Escritorio" | El archivo extraído aparece en el Escritorio |
| 6b | Igual que 5 con Configuración → otra carpeta | | El resultado va a esa carpeta |
| 7 | App elevada (p. ej. un Explorer como admin) | Arrastrar | La rueda puede no recibir el drop (UIPI): limitación documentada |

## Gesto y cancelación

| # | Caso | Esperado |
|---|---|---|
| 8 | Shift sin arrastrar | Nada |
| 9 | Mantener Shift (auto-repetición) | La rueda aparece una sola vez |
| 10 | Soltar en el centro (el logo) | No se crea nada |
| 11 | Esc con la rueda abierta | La rueda desaparece; el arrastre sigue normal en el origen |
| 12 | Soltar fuera de la rueda | El drop va a lo que esté debajo (comportamiento normal de Windows) |
| 13 | Mover una ventana por su barra + Shift | La rueda aparece pero no recibe nada; desaparece al soltar |
| 14 | Cambiar el gesto a clic derecho en el menú | Shift deja de abrir la rueda; clic derecho durante el arrastre la abre |

## Sin arrastrar: atajo y menú contextual

| # | Caso | Esperado |
|---|---|---|
| 21 | Seleccionar 3 fotos en el Explorador y presionar Ctrl+Alt+K | Aparece el buscador con "3 JPG · …", ya listo para escribir; "png" + Enter convierte las 3 |
| 21b | En el buscador: "achicar", "cancion" (sin tilde), "ext pag", flechas, Esc, clic afuera | Encuentra Comprimir / lo que tenga "Canción" / Extraer páginas; las flechas dan la vuelta; Esc y el clic afuera cierran sin hacer nada |
| 22 | Ctrl+Alt+K con una pestaña de Windows 11 que no es la primera | Usa la selección de la pestaña activa |
| 23 | Ctrl+Alt+K en el escritorio con un archivo seleccionado | Funciona igual |
| 24 | Ctrl+Alt+K sin nada seleccionado | Aviso "Nada seleccionado"; no aparece la rueda |
| 25 | Clic derecho → Mostrar más opciones → Kumquat… con 5 archivos | Un solo buscador con los 5, al frente y recibiendo lo que se escribe |
| 26 | Igual con Kumquat cerrado | Kumquat arranca y abre el buscador |
| 27 | Mover la carpeta de Kumquat y abrirlo | El menú apunta a la ruta nueva |
| 28 | Desactivar el menú desde el ícono; `Kumquat.exe --unregister` | La entrada desaparece del Explorador |

## Ventana de opciones

| # | Caso | Esperado |
|---|---|---|
| 29 | Recortar un MP4, MKV y WebM: ▶ | Se ve y se oye; el cabezal avanza; para al final del tramo |
| 30 | Mover las asas del tramo | El video muestra el fotograma exacto; "Desde/Hasta" se actualizan |
| 31 | Recortar un MP3, OGG y FLAC: ▶ | Se oye; la onda se colorea dentro del tramo |
| 32 | Fotograma: clic en la línea, arrastrar, o ▶ y pausar | "Instante" sigue al cabezal; el resultado es ese fotograma |
| 33 | Extraer páginas: clic en miniaturas, "Todas", "Ninguna", escribir "2-4" | Miniaturas y campo siempre coinciden |
| 34 | Cerrar la ventana durante la reproducción | El sonido para al instante; no quedan `ffmpeg.exe` |
| 35 | Sin parlantes conectados | El video se reproduce igual, sin sonido |

## Pantallas

| # | Caso | Esperado |
|---|---|---|
| 15 | Monitor al 100 % y otro al 150 % | La rueda tiene el mismo tamaño físico relativo en ambos y el sector bajo el cursor es el resaltado |
| 16 | Monitor a la izquierda del principal (coordenadas negativas) | Igual |
| 17 | Tema claro / oscuro de Windows | La rueda y las ventanas siguen el tema |
| 17b | Windows 11 | Las ventanas de opciones dejan ver el fondo desenfocado (acrílico) |

## Robustez

| # | Caso | Esperado |
|---|---|---|
| 18 | Convertir un video largo y salir desde la bandeja | Kumquat cierra en ≤ 5 s y no quedan `ffmpeg.exe` vivos |
| 19 | Archivo `.jpg` que en realidad es PNG | Se detecta como PNG (no ofrece PNG como destino) |
| 20 | Nombre con ñ, tildes, emoji o espacios | Funciona y el log muestra el nombre correcto |
