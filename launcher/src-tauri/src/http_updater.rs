use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use std::fs;
use std::path::{Path, PathBuf};
use tauri::Emitter;

// Self-update del launcher
const GITHUB_BASE: &str = "https://raw.githubusercontent.com/matthew7990/MuVoid";
const LAUNCHER_BRANCH: &str = "launcher-release";

// Cliente: descarga desde rama client (MuVoidClient/) — cambiar si el repo es distinto
const GITHUB_CLIENT_REPO: &str = "matthew7990/MuVoidClient";
const CLIENT_BRANCH: &str = "client";

/// TCP address of the game ConnectServer (host:port).
const GAME_SERVER_ADDR: &str = "34.176.13.14:44405";

// ── Config del launcher ───────────────────────────────────────────────────────

#[derive(Debug, Serialize, Deserialize, Default, Clone)]
struct LauncherConfig {
    /// Ruta al directorio del cliente compilado (configurable por el usuario).
    client_source: Option<String>,
}

fn config_path() -> PathBuf {
    std::env::var("LOCALAPPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(|_| exe_dir())
        .join("MuVoid")
        .join("config.json")
}

fn load_config() -> LauncherConfig {
    if let Ok(data) = fs::read_to_string(config_path()) {
        serde_json::from_str(&data).unwrap_or_default()
    } else {
        LauncherConfig::default()
    }
}

fn save_config(config: &LauncherConfig) -> Result<(), String> {
    let path = config_path();
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).map_err(|e| e.to_string())?;
    }
    let data = serde_json::to_string_pretty(config).map_err(|e| e.to_string())?;
    fs::write(&path, data).map_err(|e| e.to_string())
}

// ── Manifest structs ──────────────────────────────────────────────────────────

#[derive(Debug, Deserialize)]
struct VersionManifest {
    version: String,
    #[serde(default)]
    changelog: Vec<String>,
    files: Vec<FileEntry>,
}

#[derive(Debug, Deserialize)]
struct FileEntry {
    path: String,
    sha256: String,
    #[allow(dead_code)]
    size: u64,
}

// ── Public types returned to the frontend ─────────────────────────────────────

#[derive(Debug, Serialize, Clone)]
pub struct ClientInfo {
    pub version: String,
    pub changelog: Vec<String>,
}

#[derive(Debug, Serialize, Clone)]
pub struct DownloadProgress {
    pub current: usize,
    pub total: usize,
    pub file: String,
}

// ── Path helpers ──────────────────────────────────────────────────────────────

fn exe_dir() -> PathBuf {
    std::env::current_exe()
        .unwrap_or_else(|_| PathBuf::from("."))
        .parent()
        .unwrap_or(Path::new("."))
        .to_path_buf()
}

/// Directorio de instalación del cliente: %LOCALAPPDATA%\MuVoid\client
fn client_dir() -> PathBuf {
    std::env::var("LOCALAPPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(|_| exe_dir())
        .join("MuVoid")
        .join("client")
}

/// Busca el directorio del cliente compilado (donde está Main.exe + version.json).
/// Orden de búsqueda:
///   1. Ruta guardada en config.json (configurada por el usuario)
///   2. Carpeta "client\" al lado del launcher (modo distribución)
///   3. Rutas típicas del build de MuMain en modo desarrollo
fn find_client_source_dir() -> Option<PathBuf> {
    // 1. Ruta configurada
    let config = load_config();
    if let Some(ref src) = config.client_source {
        let p = PathBuf::from(src);
        if p.join("version.json").exists() {
            return Some(p);
        }
    }

    let exe = exe_dir();

    // Candidatos relativos al exe del launcher
    let candidates: &[&str] = &[
        // Distribución: launcher y cliente en la misma carpeta (MuVoidClient/)
        ".",
        // Distribución: carpeta "client\" al lado del exe
        "client",
        // Desarrollo — VS generator (launcher/src-tauri/target/release/ → 4 niveles arriba)
        "../../../../MuMain/out/build/vs-x86/src/Release",
        "../../../../MuMain/out/build/vs-x86/Release",
        "../../../../MuMain/out/build/vs-x86/src/Main/Release",
        // Desarrollo — Ninja generator
        "../../../../MuMain/out/build/windows-x86/src/Release",
        // client-dist en raíz del repo
        "../../../../client-dist",
        "../../../client-dist",
        // Debug build
        "../../../../MuMain/out/build/vs-x86/src/Debug",
        "../../../../MuMain/out/build/vs-x86/Debug",
    ];

    for rel in candidates {
        let candidate = exe.join(rel);
        if candidate.join("version.json").exists() {
            return Some(candidate.canonicalize().unwrap_or(candidate));
        }
    }

    None
}

/// Lee el version.json del directorio del cliente instalado.
fn get_installed_version() -> Option<String> {
    let path = client_dir().join("version.json");
    let data = fs::read_to_string(&path).ok()?;
    let v: serde_json::Value = serde_json::from_str(&data).ok()?;
    v["version"].as_str().map(|s| s.to_string())
}

// ── Network helpers (usadas solo para el self-update del launcher) ────────────

fn fetch_json<T: for<'de> Deserialize<'de>>(url: &str) -> Result<T, String> {
    let resp = reqwest::blocking::get(url).map_err(|e| e.to_string())?;
    if !resp.status().is_success() {
        return Err(format!("HTTP {}: {}", resp.status(), url));
    }
    let text = resp.text().map_err(|e| e.to_string())?;
    serde_json::from_str(&text).map_err(|e| e.to_string())
}

fn download_file(url: &str, dest: &Path) -> Result<(), String> {
    if let Some(parent) = dest.parent() {
        fs::create_dir_all(parent).map_err(|e| e.to_string())?;
    }
    let resp = reqwest::blocking::get(url).map_err(|e| e.to_string())?;
    if !resp.status().is_success() {
        return Err(format!("HTTP {}: {}", resp.status(), url));
    }
    let bytes = resp.bytes().map_err(|e| e.to_string())?;
    fs::write(dest, &bytes).map_err(|e| e.to_string())
}

fn compute_sha256(path: &Path) -> Option<String> {
    let data = fs::read(path).ok()?;
    let mut hasher = Sha256::new();
    hasher.update(&data);
    Some(format!("{:x}", hasher.finalize()))
}

fn launcher_base_url() -> String {
    format!("{}/{}", GITHUB_BASE, LAUNCHER_BRANCH)
}

/// URL base del cliente en GitHub (rama client, carpeta MuVoidClient/)
fn client_base_url() -> String {
    format!(
        "https://raw.githubusercontent.com/{}/{}/MuVoidClient",
        GITHUB_CLIENT_REPO, CLIENT_BRANCH
    )
}

// ── Tauri commands ────────────────────────────────────────────────────────────

/// Ruta de instalación del cliente (donde se copian los archivos para jugar).
#[tauri::command]
pub fn get_client_path() -> String {
    client_dir().to_string_lossy().to_string()
}

/// Ruta del cliente compilado detectada (fuente del update).
#[tauri::command]
pub fn get_client_source_path() -> String {
    find_client_source_dir()
        .map(|p| p.to_string_lossy().to_string())
        .unwrap_or_else(|| String::from("No detectado — ejecuta compile-client.bat"))
}

/// Permite al usuario configurar manualmente la ruta del cliente compilado.
#[tauri::command]
pub fn set_client_source_path(path: String) -> Result<(), String> {
    let p = PathBuf::from(&path);
    if !p.exists() {
        return Err(format!("La ruta no existe: {}", path));
    }
    if !p.join("Main.exe").exists() && !p.join("version.json").exists() {
        return Err("La carpeta no contiene Main.exe ni version.json".to_string());
    }
    let mut config = load_config();
    config.client_source = Some(path);
    save_config(&config)
}

/// Devuelve la versión e changelog del cliente.
/// Origen: local (desarrollo) > instalado > GitHub (rama client).
#[tauri::command]
pub fn get_client_info() -> Result<ClientInfo, String> {
    // 1. Local (desarrollo): directorio compilado
    if let Some(source) = find_client_source_dir() {
        let data = fs::read_to_string(source.join("version.json"))
            .map_err(|e| format!("Error leyendo version.json: {}", e))?;
        let manifest: VersionManifest =
            serde_json::from_str(&data).map_err(|e| format!("version.json inválido: {}", e))?;
        return Ok(ClientInfo {
            version: manifest.version,
            changelog: manifest.changelog,
        });
    }

    // 2. Instalado: %LOCALAPPDATA%\MuVoid\client
    let installed = client_dir().join("version.json");
    if installed.exists() {
        let data = fs::read_to_string(&installed)
            .map_err(|e| format!("Error leyendo version.json: {}", e))?;
        let manifest: VersionManifest =
            serde_json::from_str(&data).map_err(|e| format!("version.json inválido: {}", e))?;
        return Ok(ClientInfo {
            version: manifest.version,
            changelog: manifest.changelog,
        });
    }

    // 3. GitHub (rama client): para usuarios que descargan el launcher sin compilar
    let url = format!("{}/version.json", client_base_url());
    let manifest: VersionManifest = fetch_json(&url)?;
    Ok(ClientInfo {
        version: manifest.version,
        changelog: manifest.changelog,
    })
}

/// TCP-conecta al servidor del juego para saber si está online.
#[tauri::command]
pub fn check_server_online() -> bool {
    use std::net::TcpStream;
    use std::time::Duration;
    let Ok(addr) = GAME_SERVER_ADDR.parse() else {
        return false;
    };
    TcpStream::connect_timeout(&addr, Duration::from_secs(3)).is_ok()
}

/// Abre la carpeta de instalación del cliente en el Explorador.
#[tauri::command]
pub fn open_client_folder() -> Result<(), String> {
    let path = client_dir();
    fs::create_dir_all(&path).map_err(|e| e.to_string())?;
    #[cfg(target_os = "windows")]
    {
        std::process::Command::new("explorer")
            .arg(&path)
            .spawn()
            .map_err(|e| e.to_string())?;
    }
    Ok(())
}

/// Abre una URL en el navegador predeterminado.
#[tauri::command]
pub fn open_url(url: String) -> Result<(), String> {
    #[cfg(target_os = "windows")]
    {
        std::process::Command::new("cmd")
            .args(["/C", "start", "", url.as_str()])
            .spawn()
            .map_err(|e| e.to_string())?;
    }
    Ok(())
}

/// Verifica si existe una nueva versión del launcher en GitHub.
#[tauri::command]
pub fn check_launcher_update() -> Result<bool, String> {
    let current = env!("CARGO_PKG_VERSION");
    let url = format!("{}/version.json", launcher_base_url());
    let manifest: VersionManifest = fetch_json(&url)?;
    Ok(manifest.version != current)
}

/// Descarga e instala la nueva versión del launcher via script batch.
#[tauri::command]
pub fn start_launcher_update() -> Result<(), String> {
    let url = format!("{}/version.json", launcher_base_url());
    let manifest: VersionManifest = fetch_json(&url)?;

    let entry = manifest
        .files
        .iter()
        .find(|f| f.path.ends_with(".exe"))
        .ok_or_else(|| "No se encontró exe en el manifest del launcher".to_string())?;

    let file_url = format!(
        "{}/{}",
        launcher_base_url(),
        entry.path.replace('\\', "/")
    );
    let dest = exe_dir().join("muvoid-launcher-new.exe");
    download_file(&file_url, &dest)?;

    #[cfg(target_os = "windows")]
    {
        let current = std::env::current_exe().map_err(|e| e.to_string())?;
        let batch = exe_dir().join("_launcher_update.bat");
        let script = format!(
            "@echo off\r\nping 127.0.0.1 -n 3 > nul\r\nmove /Y \"{}\" \"{}\"\r\nstart \"\" \"{}\"\r\ndel \"%~f0\"\r\n",
            dest.display(),
            current.display(),
            current.display()
        );
        fs::write(&batch, script).map_err(|e| e.to_string())?;
        std::process::Command::new("cmd")
            .args(["/C", "start", "/B", "", batch.to_string_lossy().as_ref()])
            .spawn()
            .map_err(|e| e.to_string())?;
    }

    std::process::exit(0);
}

/// Actualiza el cliente: copia desde local (desarrollo) o descarga desde GitHub (rama client).
/// Emite eventos `download-progress` durante la operación.
#[tauri::command]
pub fn check_and_update_client(app: tauri::AppHandle) -> Result<(), String> {
    let install_dir = client_dir();
    fs::create_dir_all(&install_dir).map_err(|e| e.to_string())?;

    let manifest: VersionManifest;
    let from_github: bool;

    if let Some(source) = find_client_source_dir() {
        // Modo desarrollo: copiar desde directorio compilado local
        from_github = false;
        let data = fs::read_to_string(source.join("version.json"))
            .map_err(|e| format!("Error leyendo version.json: {}", e))?;
        manifest = serde_json::from_str(&data).map_err(|e| format!("version.json inválido: {}", e))?;

        apply_manifest_from_local(&app, &manifest, &source, &install_dir)?;
    } else {
        // Modo distribución: descargar desde GitHub (rama client)
        from_github = true;
        let url = format!("{}/version.json", client_base_url());
        manifest = fetch_json(&url)?;

        apply_manifest_from_github(&app, &manifest, &install_dir)?;
    }

    // Escribir version.json instalado para trackear la versión
    let dest_manifest = install_dir.join("version.json");
    let manifest_json = serde_json::to_string_pretty(&manifest).map_err(|e| e.to_string())?;
    fs::write(&dest_manifest, manifest_json).map_err(|e| e.to_string())?;

    let _ = from_github;
    Ok(())
}

/// Copia archivos desde el directorio local según el manifest.
fn apply_manifest_from_local(
    app: &tauri::AppHandle,
    manifest: &VersionManifest,
    source: &Path,
    install_dir: &Path,
) -> Result<(), String> {
    let installed_ver = get_installed_version();
    let version_ok = installed_ver.as_deref() == Some(manifest.version.as_str());
    let total = manifest.files.len();
    let sep = std::path::MAIN_SEPARATOR_STR;

    for (i, entry) in manifest.files.iter().enumerate() {
        let src_path = source.join(entry.path.replace('/', sep));
        let dest_path = install_dir.join(entry.path.replace('/', sep));

        let need_copy = if version_ok {
            fs::metadata(&dest_path)
                .ok()
                .and_then(|_| compute_sha256(&dest_path))
                .as_deref() != Some(entry.sha256.as_str())
        } else {
            fs::metadata(&dest_path)
                .ok()
                .and_then(|_| compute_sha256(&dest_path))
                .as_deref() != Some(entry.sha256.as_str())
        };

        if need_copy {
            let _ = app.emit("download-progress", DownloadProgress {
                current: i,
                total,
                file: entry.path.clone(),
            });
            if !src_path.exists() {
                return Err(format!("Archivo no encontrado: {}", src_path.display()));
            }
            if let Some(parent) = dest_path.parent() {
                fs::create_dir_all(parent).map_err(|e| e.to_string())?;
            }
            fs::copy(&src_path, &dest_path)
                .map_err(|e| format!("Error copiando {}: {}", entry.path, e))?;
        }
        let _ = app.emit("download-progress", DownloadProgress {
            current: i + 1,
            total,
            file: entry.path.clone(),
        });
    }
    Ok(())
}

/// Descarga archivos desde GitHub según el manifest.
fn apply_manifest_from_github(
    app: &tauri::AppHandle,
    manifest: &VersionManifest,
    install_dir: &Path,
) -> Result<(), String> {
    let base = client_base_url();
    let installed_ver = get_installed_version();
    let version_ok = installed_ver.as_deref() == Some(manifest.version.as_str());
    let total = manifest.files.len();
    let sep = std::path::MAIN_SEPARATOR_STR;

    for (i, entry) in manifest.files.iter().enumerate() {
        let dest_path = install_dir.join(entry.path.replace('/', sep));

        let need_download = if version_ok {
            fs::metadata(&dest_path)
                .ok()
                .and_then(|_| compute_sha256(&dest_path))
                .as_deref() != Some(entry.sha256.as_str())
        } else {
            true
        };

        if need_download {
            let _ = app.emit("download-progress", DownloadProgress {
                current: i,
                total,
                file: entry.path.clone(),
            });
            let url = format!("{}/{}", base, entry.path.replace('\\', "/"));
            download_file(&url, &dest_path)?;
        }
        let _ = app.emit("download-progress", DownloadProgress {
            current: i + 1,
            total,
            file: entry.path.clone(),
        });
    }
    Ok(())
}

/// Lanza Main.exe desde el directorio de instalación del cliente.
#[tauri::command]
pub fn launch_game() -> Result<(), String> {
    let client_path = client_dir();
    let main_exe = client_path.join("Main.exe");

    if !main_exe.exists() {
        return Err("Main.exe no encontrado. Actualiza el cliente primero.".to_string());
    }

    #[cfg(target_os = "windows")]
    {
        std::process::Command::new(&main_exe)
            .current_dir(&client_path)
            .spawn()
            .map_err(|e| e.to_string())?;
    }

    #[cfg(not(target_os = "windows"))]
    {
        return Err("Solo Windows es compatible.".to_string());
    }

    Ok(())
}
