use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use std::fs;
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};
use std::sync::atomic::{AtomicUsize, AtomicU64, Ordering};
use std::time::{Instant, Duration};
use std::io::Write;
use tauri::Emitter;
use rayon::prelude::*;
use std::sync::OnceLock;

static APP_HANDLE: OnceLock<tauri::AppHandle> = OnceLock::new();

// Self-update del launcher
const GITHUB_BASE: &str = "https://raw.githubusercontent.com/matthew7990/MuVoidClient-Release/main";

// Cliente: descarga desde el repo de Release
const GITHUB_CLIENT_REPO: &str = "matthew7990/MuVoidClient-Release";
const CLIENT_BRANCH: &str = "main";

/// TCP address of the game ConnectServer (host:port).
const GAME_SERVER_ADDR: &str = "34.176.13.14:44405";

// ── Config del launcher ───────────────────────────────────────────────────────

#[derive(Debug, Serialize, Deserialize, Default, Clone)]
struct LauncherConfig {
    /// Ruta al directorio del cliente compilado (configurable por el usuario).
    client_source: Option<String>,
    /// Ruta de instalación personalizada para el cliente.
    install_dir: Option<String>,
    /// Configuración del juego (resolución, audio, etc.) — NO IP/puerto.
    #[serde(default)]
    game: GameSettings,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct GameSettings {
    pub window_width: u32,
    pub window_height: u32,
    pub windowed: bool,
    pub sound_enabled: bool,
    pub music_enabled: bool,
    pub volume_level: u32,
    /// Idioma del juego: Eng, SPN, POR
    #[serde(default)]
    pub language: String,
}

impl Default for GameSettings {
    fn default() -> Self {
        Self {
            window_width: 1024,
            window_height: 768,
            windowed: true,
            sound_enabled: true,
            music_enabled: false,
            volume_level: 5,
            language: "SPN".to_string(),
        }
    }
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
        serde_json::from_str(strip_bom(&data)).unwrap_or_default()
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

#[derive(Debug, Serialize, Deserialize)]
struct VersionManifest {
    version: String,
    #[serde(default)]
    changelog: Vec<String>,
    files: Vec<FileEntry>,
}

#[derive(Debug, Serialize, Deserialize)]
struct FileEntry {
    path: String,
    sha256: String,
    #[serde(default)]
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
    pub bytes_downloaded: u64,
    pub total_bytes: u64,
    pub speed_mbps: f64,
    pub eta_seconds: u64,
}

// ── Path helpers ──────────────────────────────────────────────────────────────

fn exe_dir() -> PathBuf {
    std::env::current_exe()
        .unwrap_or_else(|_| PathBuf::from("."))
        .parent()
        .unwrap_or(Path::new("."))
        .to_path_buf()
}

/// Directorio de datos del launcher: %LOCALAPPDATA%\MuVoid
fn data_dir() -> PathBuf {
    std::env::var("LOCALAPPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(|_| exe_dir())
        .join("MuVoid")
}

fn log(msg: &str) {
    let dir = data_dir();
    let _ = fs::create_dir_all(&dir);
    let path = dir.join("launcher.log");
    
    // Log to file
    if let Ok(mut file) = fs::OpenOptions::new().create(true).append(true).open(path) {
        let now = chrono::Local::now().format("%Y-%m-%d %H:%M:%S");
        let _ = writeln!(file, "[{}] {}", now, msg);
    }
    
    // Log to frontend console
    if let Some(app) = APP_HANDLE.get() {
        let _ = app.emit("launcher-log", msg);
    }
    
    println!("{}", msg);
}

/// Directorio de instalación del cliente. Prioriza configuración > defecto (C:\Program Files\MuVoid).
fn client_dir() -> PathBuf {
    let config = load_config();
    if let Some(ref custom) = config.install_dir {
        return PathBuf::from(custom);
    }
    // Carpeta estándar en Program Files; el usuario puede cambiarla con "Seleccionar carpeta"
    PathBuf::from("C:\\Program Files\\MuVoid")
}

/// Busca el directorio donde hay archivos listos (version.json) para usar como FUENTE de actualización.
/// DESACTIVADO: Ahora el launcher solo se alimenta de GitHub.
fn find_client_source_dir() -> Option<PathBuf> {
    None
}

/// Lee el version.json del directorio del cliente instalado con reintentos si está bloqueado.
fn get_installed_version() -> Option<String> {
    let path = client_dir().join("version.json");
    
    // Reintentar lectura si falla (el archivo podría estar siendo escrito)
    for _ in 0..5 {
        if let Ok(data) = fs::read_to_string(&path) {
            let data = strip_bom(&data);
            if let Ok(v) = serde_json::from_str::<serde_json::Value>(data) {
                return v["version"].as_str().map(|s| s.to_string());
            }
        }
        std::thread::sleep(Duration::from_millis(100));
    }
    None
}

/// Escribe el version.json de forma atómica usando un archivo temporal.
fn save_installed_manifest(install_dir: &Path, manifest: &VersionManifest) -> Result<(), String> {
    let dest = install_dir.join("version.json");
    let tmp = install_dir.join("version.json.tmp");
    
    let json = serde_json::to_string_pretty(manifest).map_err(|e| e.to_string())?;
    
    // Reintentar escritura si falla por bloqueo
    for _ in 0..5 {
        if fs::write(&tmp, &json).is_ok() {
            if fs::rename(&tmp, &dest).is_ok() {
                return Ok(());
            }
        }
        std::thread::sleep(Duration::from_millis(100));
    }
    
    Err("No se pudo guardar version.json (archivo bloqueado)".to_string())
}

// ── Clientes HTTP ─────────────────────────────────────────────────────────────

/// Cliente compartido para todas las descargas del cliente.
/// Se crea una sola vez (lazy). El pool de conexiones reutiliza las TCP connections
/// entre archivos, y HTTP/2 multiplexa varias peticiones sobre el mismo socket.
static DOWNLOAD_CLIENT: OnceLock<reqwest::blocking::Client> = OnceLock::new();

fn shared_download_client() -> &'static reqwest::blocking::Client {
    DOWNLOAD_CLIENT.get_or_init(|| {
        reqwest::blocking::Client::builder()
            .timeout(Duration::from_secs(300))
            .connect_timeout(Duration::from_secs(20))
            .user_agent("MuVoidLauncher/1.0")
            // Hasta 32 conexiones idle reutilizables por host CDN
            .pool_max_idle_per_host(32)
            // HTTP/2 via ALPN en TLS: multiplexa N peticiones sobre el mismo socket
            .http2_adaptive_window(true)
            // Keep-alive para que las conexiones no se cierren entre archivos
            .tcp_keepalive(Duration::from_secs(30))
            .build()
            .expect("failed to build download client")
    })
}

/// Cliente ligero para JSON/metadatos (self-update, manifests).
/// No necesita el pool agresivo de descargas.
fn get_http_client() -> reqwest::blocking::Client {
    reqwest::blocking::Client::builder()
        .timeout(Duration::from_secs(30))
        .connect_timeout(Duration::from_secs(15))
        .user_agent("MuVoidLauncher/1.0")
        .build()
        .unwrap_or_default()
}

/// Quita BOM (UTF-8) del inicio del texto; PowerShell Set-Content -Encoding UTF8 lo agrega.
fn strip_bom(s: &str) -> &str {
    const BOM: &str = "\u{feff}";
    s.strip_prefix(BOM).unwrap_or(s)
}

fn fetch_json<T: for<'de> Deserialize<'de>>(url: &str) -> Result<T, String> {
    log(&format!("Fetching JSON: {}", url));
    let client = get_http_client();
    let resp = client.get(url).send().map_err(|e| {
        let err = format!("Error de conexión: {}", e);
        log(&err);
        err
    })?;
    
    if !resp.status().is_success() {
        let err = format!("HTTP {}: {}", resp.status(), url);
        log(&err);
        return Err(err);
    }
    let text = resp.text().map_err(|e| e.to_string())?;
    let text = strip_bom(&text);
    serde_json::from_str(text).map_err(|e| {
        log(&format!("Error parseando JSON de {}: {}", url, e));
        e.to_string()
    })
}

fn download_file(url: &str, dest: &Path) -> Result<(), String> {
    use std::io::BufWriter;

    if let Some(parent) = dest.parent() {
        fs::create_dir_all(parent).map_err(|e| e.to_string())?;
    }

    let resp = shared_download_client()
        .get(url)
        .send()
        .map_err(|e| {
            log(&format!("Error descargando {}: {}", url, e));
            e.to_string()
        })?;

    if !resp.status().is_success() {
        return Err(format!("HTTP {}: {}", resp.status(), url));
    }

    // Stream directo a disco: 256 KB de write-buffer evita cientos de syscalls
    // y nunca carga el archivo completo en RAM (crítico para archivos grandes como *.ozb).
    let file = fs::File::create(dest).map_err(|e| e.to_string())?;
    let mut writer = BufWriter::with_capacity(256 * 1024, file);
    let mut reader = resp;
    std::io::copy(&mut reader, &mut writer).map_err(|e| e.to_string())?;
    writer.flush().map_err(|e| e.to_string())
}

fn compute_sha256(path: &Path) -> Option<String> {
    let mut file = fs::File::open(path).ok()?;
    let mut hasher = Sha256::new();
    std::io::copy(&mut file, &mut hasher).ok()?;
    Some(format!("{:x}", hasher.finalize()))
}

fn launcher_base_url() -> String {
    GITHUB_BASE.to_string()
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

/// Ruta mostrada en la UI: fuente local (dev) si existe, sino carpeta de instalación.
#[tauri::command]
pub fn get_client_source_path() -> String {
    find_client_source_dir()
        .map(|p| p.to_string_lossy().to_string())
        .unwrap_or_else(|| client_dir().to_string_lossy().to_string())
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
    // 1. GitHub (rama client): SIEMPRE preguntar a GitHub primero para tener metadata fresca
    let url = format!("{}/version.json", client_base_url());
    if let Ok(manifest) = fetch_json::<VersionManifest>(&url) {
        return Ok(ClientInfo {
            version: manifest.version,
            changelog: manifest.changelog,
        });
    }

    // 2. Fallback: instalado (carpeta de instalación) - solo si no hay internet
    let installed = client_dir().join("version.json");
    if installed.exists() {
        let data = fs::read_to_string(&installed)
            .map_err(|e| format!("Error leyendo version.json: {}", e))?;
        let data = strip_bom(&data);
        let manifest: VersionManifest =
            serde_json::from_str(data).map_err(|e| format!("version.json inválido: {}", e))?;
        return Ok(ClientInfo {
            version: manifest.version,
            changelog: manifest.changelog,
        });
    }

    Err("No se pudo obtener información del cliente (GitHub no responde y no hay instalación local)".to_string())
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

/// Devuelve true si el cliente está instalado (tiene Main.exe).
#[tauri::command]
pub fn is_client_installed() -> bool {
    client_dir().join("Main.exe").exists()
}

/// Abre un diálogo para seleccionar la carpeta de instalación.
#[tauri::command]
pub fn select_install_directory() -> Option<String> {
    let start_dir = client_dir();
    let folder = rfd::FileDialog::new()
        .set_title("Seleccionar carpeta de instalación de MuVoid")
        .set_directory(&start_dir)
        .pick_folder();
    
    if let Some(p) = folder {
        let path_str = p.to_string_lossy().to_string();
        let mut config = load_config();
        config.install_dir = Some(path_str.clone());
        if let Err(_) = save_config(&config) {
            return None;
        }
        Some(path_str)
    } else {
        None
    }
}

/// Devuelve la configuración del juego (resolución, audio, etc.).
#[tauri::command]
pub fn get_game_settings() -> GameSettings {
    load_config().game
}

/// Guarda la configuración del juego.
#[tauri::command]
pub fn save_game_settings(settings: GameSettings) -> Result<(), String> {
    let mut config = load_config();
    config.game = settings;
    save_config(&config)
}

/// Escribe game_config.ini en %LOCALAPPDATA%\MuVoid\ para que el cliente lo lea.
fn write_game_config_ini() -> Result<(), String> {
    let config = load_config();
    let g = &config.game;
    let dir = data_dir();
    fs::create_dir_all(&dir).map_err(|e| e.to_string())?;
    let path = dir.join("game_config.ini");

    let lang = if g.language.is_empty() { "SPN" } else { g.language.as_str() };
    let (ip, port) = parse_game_server_addr(GAME_SERVER_ADDR).unwrap_or(("127.0.0.1".to_string(), 44405));
    let ini = format!(
        "[LOGIN]\r\nVersion=1.03.34\r\nTestVersion=1.03.34\r\nRememberMe=0\r\nLanguage={}\r\nEncryptedUsername=\r\nEncryptedPassword=\r\n\
[PARTITION]\r\nVersion=357\r\n\
[Window]\r\nWidth={}\r\nHeight={}\r\nWindowed={}\r\n\
[Graphics]\r\nColorDepth=0\r\nRenderTextType=0\r\n\
[Audio]\r\nSoundEnabled={}\r\nMusicEnabled={}\r\nVolumeLevel={}\r\n\
[CONNECTION SETTINGS]\r\nServerIP={}\r\nServerPort={}\r\n",
        lang,
        g.window_width,
        g.window_height,
        if g.windowed { "1" } else { "0" },
        if g.sound_enabled { "1" } else { "0" },
        if g.music_enabled { "1" } else { "0" },
        g.volume_level.min(10),
        ip,
        port
    );

    fs::write(&path, ini).map_err(|e| e.to_string())
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

/// Compara dos versiones (x.y.z) y devuelve true si remote es mayor que current.
fn is_newer_version(current: &str, remote: &str) -> bool {
    let current_parts: Vec<u32> = current.split('.').filter_map(|s| s.parse().ok()).collect();
    let remote_parts: Vec<u32> = remote.split('.').filter_map(|s| s.parse().ok()).collect();
    
    for i in 0..std::cmp::max(current_parts.len(), remote_parts.len()) {
        let v_curr = *current_parts.get(i).unwrap_or(&0);
        let v_rem = *remote_parts.get(i).unwrap_or(&0);
        if v_rem > v_curr { return true; }
        if v_rem < v_curr { return false; }
    }
    false
}

/// Verifica si existe una nueva versión del launcher en GitHub.
#[tauri::command]
pub fn check_launcher_update() -> Result<bool, String> {
    let current = env!("CARGO_PKG_VERSION");
    log(&format!("Checking for launcher update... Current v{}", current));
    let url = format!("{}/launcher_version.json", launcher_base_url());
    let manifest: VersionManifest = fetch_json(&url)?;
    let has_update = is_newer_version(current, &manifest.version);
    log(&format!("Update check: Remote v{}? New? {}", manifest.version, has_update));
    Ok(has_update)
}

/// Descarga e instala la nueva versión del launcher via script batch.
#[tauri::command]
pub fn start_launcher_update() -> Result<(), String> {
    let url = format!("{}/launcher_version.json", launcher_base_url());
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
        
        #[cfg(target_os = "windows")]
        {
            use std::os::windows::process::CommandExt;
            const CREATE_NO_WINDOW: u32 = 0x08000000;
            std::process::Command::new("cmd")
                .args(["/C", "start", "/B", "", batch.to_string_lossy().as_ref()])
                .creation_flags(CREATE_NO_WINDOW)
                .spawn()
                .map_err(|e| e.to_string())?;
        }
    }

    std::process::exit(0);
}

/// Actualiza el cliente: copia desde local (desarrollo) o descarga desde GitHub (rama client).
/// Emite eventos `download-progress` durante la operación.
/// Ejecuta en spawn_blocking para no bloquear la UI durante la verificación/descarga.
#[tauri::command]
pub async fn check_and_update_client(app: tauri::AppHandle) -> Result<(), String> {
    tauri::async_runtime::spawn_blocking(move || do_check_and_update_client(app))
        .await
        .map_err(|_| "Operación interrumpida".to_string())?
}

fn do_check_and_update_client(app: tauri::AppHandle) -> Result<(), String> {
    let _ = APP_HANDLE.set(app.clone());
    log("Iniciando check_and_update_client...");
    // PRIORIDAD: Verificar si el launcher necesita actualizarse primero.
    if check_launcher_update().unwrap_or(false) {
        log("Launcher update REQUIRED. Aborting client update.");
        return Err("LAUNCHER_UPDATE_REQUIRED".to_string());
    }

    let install_dir = client_dir();
    fs::create_dir_all(&install_dir).map_err(|e| e.to_string())?;

    let manifest: VersionManifest;
    let from_github: bool;

    // Modo distribución: descargar desde GitHub (rama client)
    let url = format!("{}/version.json", client_base_url());
    manifest = fetch_json(&url)?;

    apply_manifest_from_github(&app, &manifest, &install_dir)?;

    // Escribir version.json instalado para trackear la versión de forma atómica
    save_installed_manifest(&install_dir, &manifest)?;

    let _ = from_github;
    Ok(())
}

/// Elimina archivos en install_dir que NO están en el manifest (sincronización completa).
fn purge_obsolete_files(manifest: &VersionManifest, install_dir: &Path) -> Result<(), String> {
    use std::collections::HashSet;
    let sep = std::path::MAIN_SEPARATOR_STR;
    let manifest_paths: HashSet<String> = manifest.files.iter()
        .map(|e| {
            let p = install_dir.join(e.path.replace('/', sep));
            p.to_string_lossy().to_lowercase()
        })
        .collect();

    fn collect_obsolete(dir: &Path, manifest_paths: &HashSet<String>, out: &mut Vec<PathBuf>) {
        if let Ok(entries) = fs::read_dir(dir) {
            for entry in entries.flatten() {
                let path = entry.path();
                if path.is_file() {
                    let key = path.to_string_lossy().to_lowercase();
                    if !manifest_paths.contains(&key) {
                        let name = path.file_name().and_then(|n| n.to_str()).unwrap_or("");
                        if name != "version.json" && name != "config.ini" {
                            out.push(path);
                        }
                    }
                } else if path.is_dir() {
                    collect_obsolete(&path, manifest_paths, out);
                }
            }
        }
    }
    let mut to_remove = Vec::new();
    collect_obsolete(install_dir, &manifest_paths, &mut to_remove);

    for path in &to_remove {
        if let Err(e) = fs::remove_file(path) {
            log(&format!("Advertencia: no se pudo eliminar obsoleto {}: {}", path.display(), e));
        }
    }
    if !to_remove.is_empty() {
        log(&format!("Eliminados {} archivos obsoletos", to_remove.len()));
    }
    Ok(())
}

/// Copia archivos desde el directorio local según el manifest.
/// Copia TODO lo que difiere (SHA256 distinto o no existe). Nunca omite por error.
fn apply_manifest_from_local(
    app: &tauri::AppHandle,
    manifest: &VersionManifest,
    source: &Path,
    install_dir: &Path,
) -> Result<(), String> {
    let total = manifest.files.len();
    let sep = std::path::MAIN_SEPARATOR_STR;
    let to_copy: Vec<_> = manifest.files.iter()
        .filter(|entry| {
            let dest_path = install_dir.join(entry.path.replace('/', sep));
            let exists = fs::metadata(&dest_path).is_ok();
            if !exists { return true; }
            compute_sha256(&dest_path).as_deref() != Some(entry.sha256.as_str())
        })
        .collect();

    for (i, entry) in to_copy.iter().enumerate() {
        let src_path = source.join(entry.path.replace('/', sep));
        let dest_path = install_dir.join(entry.path.replace('/', sep));

        let _ = app.emit("download-progress", DownloadProgress {
            current: i,
            total: to_copy.len(),
            file: entry.path.clone(),
            bytes_downloaded: 0,
            total_bytes: 0,
            speed_mbps: 0.0,
            eta_seconds: 0,
        });
        if !src_path.exists() {
            return Err(format!("Archivo no encontrado: {}", src_path.display()));
        }
        if let Some(parent) = dest_path.parent() {
            fs::create_dir_all(parent).map_err(|e| e.to_string())?;
        }
        fs::copy(&src_path, &dest_path)
            .map_err(|e| format!("Error copiando {}: {}", entry.path, e))?;
        let _ = app.emit("download-progress", DownloadProgress {
            current: i + 1,
            total: to_copy.len(),
            file: entry.path.clone(),
            bytes_downloaded: 0,
            total_bytes: 0,
            speed_mbps: 0.0,
            eta_seconds: 0,
        });
    }
    purge_obsolete_files(manifest, install_dir)?;
    if to_copy.is_empty() {
        log("Cliente ya actualizado, no hay archivos que copiar.");
    } else {
        log(&format!("Copiados {} archivos (de {} en el manifest)", to_copy.len(), total));
    }
    Ok(())
}

/// Descarga archivos desde GitHub según el manifest.
/// Descarga TODO lo que falta o difiere (SHA256). Reporta error si alguna descarga falla.
fn apply_manifest_from_github(
    app: &tauri::AppHandle,
    manifest: &VersionManifest,
    install_dir: &Path,
) -> Result<(), String> {
    let base = client_base_url();
    let sep = std::path::MAIN_SEPARATOR_STR;

    let mut files_to_download: Vec<_> = manifest.files.par_iter().filter(|entry| {
        let dest_path = install_dir.join(entry.path.replace('/', sep));
        let exists = fs::metadata(&dest_path).is_ok();
        if !exists { return true; }
        compute_sha256(&dest_path).as_deref() != Some(entry.sha256.as_str())
    }).collect();

    if files_to_download.is_empty() {
        log("Cliente ya actualizado, no hay archivos que descargar.");
        purge_obsolete_files(manifest, install_dir)?;
        return Ok(());
    }

    // Ordenar de mayor a menor: los archivos pesados arrancan primero y no son
    // "stragglers" al final que bloquean la finalización del resto.
    files_to_download.sort_unstable_by(|a, b| b.size.cmp(&a.size));

    log(&format!("Descargando {} archivos (de {} en el manifest)", files_to_download.len(), manifest.files.len()));

    let total_files = files_to_download.len();
    let total_bytes: u64 = files_to_download.iter().map(|f| f.size).sum();
    let downloaded_files = Arc::new(AtomicUsize::new(0));
    let downloaded_bytes = Arc::new(AtomicU64::new(0));
    let last_emit = Arc::new(Mutex::new(Instant::now()));
    let start_time = Instant::now();
    let failed: Arc<Mutex<Vec<String>>> = Arc::new(Mutex::new(Vec::new()));

    // 32 hilos: descarga I/O-bound → más concurrencia = más throughput.
    // El cliente compartido (DOWNLOAD_CLIENT) reutiliza conexiones TCP entre hilos.
    let pool = rayon::ThreadPoolBuilder::new().num_threads(32).build().map_err(|e| e.to_string())?;
    let app_shared = Arc::new(app.clone());
    let base_shared = Arc::new(base);
    let install_dir_shared = Arc::new(install_dir.to_path_buf());

    pool.install(|| {
        files_to_download.into_par_iter().for_each(|entry| {
            let dest_path = install_dir_shared.join(entry.path.replace('/', sep));
            let url = format!("{}/{}", base_shared, entry.path.replace('\\', "/"));

            const MAX_RETRIES: u32 = 3;
            let mut last_err = String::new();
            for attempt in 0..MAX_RETRIES {
                match download_file(&url, &dest_path) {
                    Ok(()) => {
                        let current_files = downloaded_files.fetch_add(1, Ordering::SeqCst) + 1;
                        let current_bytes = downloaded_bytes.fetch_add(entry.size, Ordering::SeqCst) + entry.size;
                        let elapsed = start_time.elapsed().as_secs_f64();
                        let speed = if elapsed > 0.0 { (current_bytes as f64 / 1024.0 / 1024.0) / elapsed } else { 0.0 };
                        let eta = if speed > 0.0 {
                            ((total_bytes.saturating_sub(current_bytes) as f64) / 1024.0 / 1024.0 / speed) as u64
                        } else { 0 };
                        let mut last = last_emit.lock().unwrap();
                        if last.elapsed() > Duration::from_millis(100) || current_files == total_files {
                            *last = Instant::now();
                            let _ = app_shared.emit("download-progress", DownloadProgress {
                                current: current_files,
                                total: total_files,
                                file: entry.path.clone(),
                                bytes_downloaded: current_bytes,
                                total_bytes,
                                speed_mbps: speed,
                                eta_seconds: eta,
                            });
                        }
                        return;
                    }
                    Err(e) => {
                        last_err = e.clone();
                        log(&format!("Intento {} para {}: {}", attempt + 1, entry.path, e));
                        if attempt < MAX_RETRIES - 1 {
                            std::thread::sleep(Duration::from_secs(2));
                        }
                    }
                }
            }
            failed.lock().unwrap().push(format!("{}: {}", entry.path, last_err));
        });
    });

    let failures = failed.lock().unwrap().clone();
    if !failures.is_empty() {
        let msg = format!("Fallaron {} descargas:\n{}", failures.len(), failures.join("\n"));
        log(&msg);
        return Err(msg);
    }

    purge_obsolete_files(manifest, install_dir)?;
    Ok(())
}

/// Lanza Main.exe con IP:puerto por command line (connect /uIP /pPuerto).
/// Escribe game_config.ini con resolución, audio, etc. (sin IP/puerto).
#[tauri::command]
pub fn launch_game() -> Result<(), String> {
    let client_path = client_dir();
    let main_exe = client_path.join("Main.exe");

    if !main_exe.exists() {
        return Err("Main.exe no encontrado. Actualiza el cliente primero.".to_string());
    }

    write_game_config_ini()?;

    let (ip, port) = parse_game_server_addr(GAME_SERVER_ADDR)?;

    #[cfg(target_os = "windows")]
    {
        std::process::Command::new(&main_exe)
            .current_dir(&client_path)
            .arg("connect")
            .arg(format!("/u{}", ip))
            .arg(format!("/p{}", port))
            .spawn()
            .map_err(|e| e.to_string())?;
    }

    #[cfg(not(target_os = "windows"))]
    {
        return Err("Solo Windows es compatible.".to_string());
    }

    Ok(())
}

fn parse_game_server_addr(addr: &str) -> Result<(String, u16), String> {
    let parts: Vec<&str> = addr.splitn(2, ':').collect();
    if parts.len() != 2 {
        return Err(format!("Formato inválido de servidor: {}", addr));
    }
    let port: u16 = parts[1].parse().map_err(|_| format!("Puerto inválido: {}", parts[1]))?;
    Ok((parts[0].to_string(), port))
}
