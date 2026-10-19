# Seguridad

Kumquat abre archivos que no controla (descargas, adjuntos) con motores nativos. Nos tomamos en serio cualquier forma de que un archivo malicioso ejecute código, lea o escriba fuera de lo esperado, o borre el original.

## Reportar una vulnerabilidad

**No abras un issue público.** Usa el [reporte privado de vulnerabilidades de GitHub](../../security/advisories/new) de este repositorio. Incluye, si puedes, un archivo de prueba mínimo y la versión de Kumquat.

Respondemos en un plazo de 7 días y coordinamos la divulgación una vez publicada la corrección.

## Alcance

- Ejecución de código o lectura/escritura fuera de la carpeta destino a partir de un archivo convertido.
- Inyección de argumentos u opciones en motores a través de nombres de archivo.
- Pérdida o modificación del archivo original.
- Fugas de información a través de logs o reportes.

Las vulnerabilidades de los motores de terceros (FFmpeg, libvips, etc.) se reportan a sus proyectos; avísanos igualmente para actualizar el binario fijado en `tools/engines.json`.

## Defensas actuales

- Motores fuera de proceso en un Job Object con límite de memoria y muerte garantizada.
- Argumentos siempre separados (`ArgumentList`), sin shell; rutas de FFmpeg con prefijo `file:` y lista blanca de protocolos.
- Binarios de terceros con hash SHA-256 fijado.
- Extracción de ZIP con protección contra Zip Slip y bombas de descompresión.
- Kumquat corre sin elevación y nunca sobrescribe archivos.
