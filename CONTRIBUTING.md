# Contribuir a Kumquat

¡Gracias por querer ayudar! Estas reglas existen para que el proyecto se mantenga confiable mientras crece.

## Antes de empezar

- Para cambios grandes (un motor nuevo, una herramienta, cambios en la rueda), abre primero un issue para acordar el enfoque.
- Kumquat es **GPL-3.0-or-later**. Todo código y todo motor nuevo debe tener una licencia compatible. No se aceptan dependencias "no comerciales" ni propietarias.

## Flujo

1. Haz un fork y una rama desde `main` (`feat/pdf-merge`, `fix/heic-orientation`).
2. Commits en formato [Conventional Commits](https://www.conventionalcommits.org/es/) (`feat:`, `fix:`, `test:`, `docs:`, `refactor:`, `ci:`).
3. Firma cada commit con DCO: `git commit -s` (agrega `Signed-off-by:`). No pedimos CLA.
4. Abre el PR. La CI debe pasar en verde (x64 y ARM64) y se requiere una revisión.

## Definición de terminado

- [ ] `dotnet build` sin advertencias (todas son errores) y `dotnet format --verify-no-changes` limpio.
- [ ] Tests que fallarían sin tu cambio. Lógica pura → tests unitarios o de propiedades; motores → la arista aparece sola en `EdgeConversionTests`, pero agrega asserts específicos si la conversión tiene particularidades.
- [ ] Ninguna excepción para errores esperados: usa `Result`/`Error`.
- [ ] Nada de `DateTime.Now`: inyecta `TimeProvider`.
- [ ] Documentación XML en las APIs públicas de `Kumquat.Core`.

## Reglas de seguridad (no negociables)

- Los procesos externos solo se lanzan con `ProcessRunner` (argumentos separados, Job Object, timeout). El analizador lo impone.
- Las rutas del usuario nunca deben poder interpretarse como opciones o protocolos del motor (FFmpeg: prefijo `file:` y `-protocol_whitelist`).
- Los archivos comprimidos se extraen solo con `SafeExtractor`.
- Los hooks de teclado nunca registran pulsaciones: solo miran la tecla del gesto y Escape.
- Kumquat nunca responde `MOVE` a un drop y nunca modifica el archivo original.

## Agregar un motor

1. Agrega el binario a `tools/engines.json` con URL, **SHA-256**, licencia y enlace al código fuente.
2. Crea `src/Kumquat.Engines.<Nombre>` implementando `IConverter`; declara aristas con costos honestos (pérdida de calidad y tiempo).
3. Si el corpus sintético no puede generar muestras de su formato de entrada, agrega una muestra **con licencia CC0 o generada por script** en `corpus/` y regístrala en `corpus/LICENSES.md`.
4. Actualiza `THIRD-PARTY-NOTICES.md`.

## Probar la interacción

La rueda y el arrastre dependen del escritorio real. Además de los E2E automáticos, sigue [docs/TESTING-MANUAL.md](docs/TESTING-MANUAL.md) si tocas `Kumquat.Interaction`.

## Conducta

Este proyecto adopta el [Contributor Covenant 2.1](https://www.contributor-covenant.org/es/version/2/1/code_of_conduct/). Ver [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
