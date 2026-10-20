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

## 5. Menú moderno de Windows 11 (pendiente de la firma)

Hoy "Kumquat…" está en el menú clásico (Mostrar más opciones). Para que aparezca en el menú nuevo, Windows exige un **`IExplorerCommand` declarado en un paquete con identidad**, es decir, firmado:

- **Paquete disperso** (*sparse package* / *packaging with external location*): un `AppxManifest.xml` con la extensión `desktop4:FileExplorerContextMenus` y un `com:ComServer`, que da identidad a la app instalada por el MSI sin convertirla en MSIX. Lo registra el MSI con `Add-AppxPackage -ExternalLocation`.
- **El manejador** se carga dentro de `explorer.exe`: tiene que ser nativo (C++ o C# con Native AOT, sin runtime de .NET compartido), devolver el título y el ícono, y al invocarse lanzar `Kumquat.exe --wheel <rutas>` (lo que ya hace el menú clásico).
- **La firma** del paquete disperso tiene que ser de un certificado en el que el equipo confíe: el de SignPath sirve. Sin eso, Windows no lo registra (con un certificado de prueba hay que instalarlo en "Personas de confianza" de cada equipo).

Por eso queda para cuando haya firma: sin ella no se puede probar de punta a punta, y código que no se puede probar no entra.
