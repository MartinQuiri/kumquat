# Pruebas manuales de la interacción

Los tests automáticos cubren la lógica y un arrastre OLE sintético (`tests/Kumquat.E2E.Tests`). El comportamiento con aplicaciones reales hay que verificarlo a mano antes de cada release y en cada PR que toque `Kumquat.Interaction`.

Ejecuta `dotnet run --project src/Kumquat.App` y anota ✅/❌ y la versión de Windows.

## Fuentes de arrastre

| # | Fuente | Pasos | Esperado |
|---|---|---|---|
| 1 | Explorador, un PNG | Arrastrar, Shift, soltar en JPG | `foto.jpg` junto al original; original intacto |
| 2 | Explorador, 5 WAV | Arrastrar selección, Shift, MP3 | 5 MP3; una sola notificación "5 archivos convertidos" |
| 3 | Escritorio | Igual que 1 | Igual |
| 4 | Chrome/Edge (imagen de una página) | Arrastrar imagen | Hoy: sin sectores ("Sin conversiones") porque no trae `CF_HDROP` (fase 2) |
| 5 | Outlook (adjunto) | Arrastrar adjunto | Igual que 4 (archivos virtuales: fase 2) |
| 6 | Carpeta ZIP abierta en Explorer | Arrastrar un archivo de adentro | Igual que 4 |
| 7 | App elevada (p. ej. un Explorer como admin) | Arrastrar | La rueda puede no recibir el drop (UIPI): limitación documentada |

## Gesto y cancelación

| # | Caso | Esperado |
|---|---|---|
| 8 | Shift sin arrastrar | Nada |
| 9 | Mantener Shift (auto-repetición) | La rueda aparece una sola vez |
| 10 | Soltar en el centro ✕ | No se crea nada |
| 11 | Esc con la rueda abierta | La rueda desaparece; el arrastre sigue normal en el origen |
| 12 | Soltar fuera de la rueda | El drop va a lo que esté debajo (comportamiento normal de Windows) |
| 13 | Mover una ventana por su barra + Shift | La rueda aparece pero no recibe nada; desaparece al soltar |
| 14 | Cambiar el gesto a clic derecho en el menú | Shift deja de abrir la rueda; clic derecho durante el arrastre la abre |

## Pantallas

| # | Caso | Esperado |
|---|---|---|
| 15 | Monitor al 100 % y otro al 150 % | La rueda tiene el mismo tamaño físico relativo en ambos y el sector bajo el cursor es el resaltado |
| 16 | Monitor a la izquierda del principal (coordenadas negativas) | Igual |
| 17 | Tema claro / oscuro de Windows | La rueda sigue el tema |

## Robustez

| # | Caso | Esperado |
|---|---|---|
| 18 | Convertir un video largo y salir desde la bandeja | Kumquat cierra en ≤ 5 s y no quedan `ffmpeg.exe` vivos |
| 19 | Archivo `.jpg` que en realidad es PNG | Se detecta como PNG (no ofrece PNG como destino) |
| 20 | Nombre con ñ, tildes, emoji o espacios | Funciona y el log muestra el nombre correcto |
