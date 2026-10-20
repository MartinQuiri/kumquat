# Publicar una versión

## 1. Etiqueta

```powershell
git tag v0.2.0
git push origin v0.2.0
```

El workflow [release.yml](../.github/workflows/release.yml) arma, para x64 y ARM64:

| Archivo | Qué es |
|---|---|
| `Kumquat-<versión>-<rid>.zip` | Portable: se descomprime y se abre `Kumquat.exe` |
| `Kumquat-<versión>-<rid>.msi` | Instalador por usuario, sin administrador (ver abajo) |
| `*.sha256` | Hash de cada uno |
| `winget-manifest.zip` | Manifiesto para winget (si está definida la variable `WINGET_PACKAGE_ID`) |

y crea un release **en borrador**: se revisa y se publica a mano.

En local es lo mismo:

```powershell
./tools/publish.ps1 -Runtime win-x64 -Version 0.2.0         # zip + prueba de arranque
./tools/build-installer.ps1 -Runtime win-x64 -Version 0.2.0 # MSI
```

## 2. El instalador

[installer/Package.wxs](../installer/Package.wxs), con WiX 5 (se descarga como paquete NuGet al compilar: no hay que instalar nada).

- Se instala en `%LOCALAPPDATA%\Programs\Kumquat` **sin pedir administrador**, con acceso en el menú Inicio y entrada en "Aplicaciones instaladas".
- Al terminar abre Kumquat (salvo con `NOLAUNCH=1`, que es lo que usa winget).
- Una versión nueva se instala encima de la anterior y la reemplaza; Kumquat se cierra antes de tocar archivos.
- Al desinstalar corre `Kumquat.exe --unregister`, que quita el menú contextual y el inicio con Windows. La configuración y los registros (`%LOCALAPPDATA%\Kumquat`) quedan.

Por qué WiX 5 y no 7: desde la 6, los binarios de WiX piden aceptar un EULA de "Open Source Maintenance Fee" (pagan quienes facturan más de 10.000 USD al año). La 5 es MS-RL pura. Subir de versión es una decisión del mantenedor.

Antes de publicar, probar a mano en una máquina limpia (o en Windows Sandbox): instalar, abrir desde Inicio, convertir algo, instalar la versión siguiente encima con Kumquat abierto y desinstalar ([TESTING-MANUAL.md](TESTING-MANUAL.md), casos 50–54).

## 3. Firma de código

Sin firma, Windows SmartScreen avisa al abrir el MSI o el zip. Para proyectos de código abierto, [SignPath Foundation](https://signpath.org) firma gratis:

1. Postular el proyecto en signpath.org (piden el repositorio público, la licencia OSI y builds reproducibles desde CI: ya los hay).
2. Configurar los secretos `SIGNPATH_API_TOKEN` y los datos de la organización.
3. Descomentar el paso de firma en `release.yml`: primero los `.exe`/`.dll` publicados, después el MSI.

## 4. winget

1. Definir en el repositorio la variable `WINGET_PACKAGE_ID` (p. ej. `Usuario.Kumquat`, con el usuario u organización de GitHub).
2. Tras publicar el release, el workflow deja `winget-manifest.zip`; o en local:
   `./tools/winget-manifest.ps1 -Version 0.2.0 -Repository usuario/kumquat -PackageIdentifier Usuario.Kumquat`
   (valida con `winget validate` si está instalado).
3. Enviarlo con un PR a [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) (a mano o con `wingetcreate submit`). Los MSI sin firmar se aceptan, pero tardan más en revisión.

Después se instala con `winget install Usuario.Kumquat`.

## 5. Menú moderno de Windows 11

"Kumquat…" aparece en el menú nuevo (sin entrar en "Mostrar más opciones") con un **paquete disperso** que solo da identidad a la carpeta donde está Kumquat:

- [packaging/sparse/AppxManifest.xml](../packaging/sparse/AppxManifest.xml) declara un `IExplorerCommand` para archivos y carpetas.
- El comando es [src/Kumquat.ShellExtension](../src/Kumquat.ShellExtension): C# compilado con **Native AOT** (DLL nativa, sin runtime de .NET compartido). Windows lo carga en `dllhost.exe`, no dentro del Explorador: si fallara, el Explorador sigue funcionando. Abre `Kumquat.exe --wheel` **una sola vez** con todos los archivos elegidos.
- El MSI lo registra si trae `Kumquat.ContextMenu.msix` **firmado**, y lo quita antes de reemplazar o borrar archivos. Al quitarlo, Windows cierra el `dllhost` y libera la DLL, así que actualizar o desinstalar no pide reiniciar.
- Sin firma (o en Windows 10) queda el menú clásico, que sigue funcionando igual.

En el release, con el certificado:

```powershell
./tools/context-menu.ps1 -Build -Runtime win-x64 -Version 0.2.0 -Publisher "CN=<sujeto del certificado>" -CertificateThumbprint <huella>
./tools/build-installer.ps1 -Runtime win-x64 -Version 0.2.0   # el MSI incluye el .msix firmado
```

Para probarlo en desarrollo sin firma (Windows con **Modo de desarrollador**):

```powershell
./tools/publish.ps1 -Runtime win-x64
./tools/context-menu.ps1 -Build -Runtime win-x64
./tools/context-menu.ps1 -Register -Dev -InstallDir artifacts/publish/win-x64
./tools/context-menu.ps1 -Unregister          # para quitarlo
```

La primera vez puede hacer falta reiniciar el Explorador para que lea el paquete.
