# MuVoid Client

Cliente Mu Online + Launcher para MuVoid.

## Contenido

- **launcher/** - Launcher Tauri (descarga y actualiza el cliente)
- **MuMain/** - Cliente Mu Online (fuente: [sven-n/MuMain](https://github.com/sven-n/MuMain))

## Ramas Git (no se mezclan)

| Rama | Contenido |
|------|-----------|
| **source** (o main) | Código fuente: launcher, MuMain, scripts |
| **client** | Solo distribucion: ejecutables, version.json (lo que descargan los usuarios) |

El build compila todo en una sola carpeta **MuVoidClient/** (launcher + cliente + version.json). Las ramas nunca se fusionan: `source` para desarrollo, `client` para releases.

## Setup

1. **Descargar MuMain** (si no existe la carpeta):
   ```
   clone-mumain.bat
   ```

2. **Compilar el cliente**:
   ```
   compile-client.bat
   ```

3. **Iniciar**:
   ```
   start-client.bat
   ```

## Build completo y publicacion (todo manual, desde tu PC)

1. **Compilar todo** (launcher + cliente) en `MuVoidClient/`:
   ```
   build-all.bat
   ```

2. **Publicar en rama client** (subir el compilado a Git):
   ```
   deploy-client.bat
   git push origin client
   ```

3. **Recompilar el launcher** (usa la rama `client` para descargar el cliente):
   ```
   cd launcher
   npm run tauri build
   ```
   El launcher descarga el cliente desde `https://raw.githubusercontent.com/OWNER/REPO/client/MuVoidClient/`. Si el repo es distinto, editar `launcher/src-tauri/src/http_updater.rs` (GITHUB_CLIENT_REPO).

## Requisitos

- Visual Studio 2022+ con workload "Desarrollo para C++"
- CMake
- .NET SDK (para ClientLibrary del cliente)
- Node.js + npm (para el launcher Tauri)
