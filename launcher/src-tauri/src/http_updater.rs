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

/// Busca el directorio donde hay archivos listos (version.json).
/// Orden: config > instalación > MuVoidClient-Release (dev) > mismo dir que exe.
fn find_client_source_dir() -> Option<PathBuf> {
    let config = load_config();
    if let Some(ref src) = config.client_source {
        let p = PathBuf::from(src);
        if p.join("version.json").exists() {
            return Some(p);
        }
    }

    let official = client_dir();
    if official.join("version.json").exists() {
        return Some(official);
    }

    // Dev: compile-client.bat copia a ../MuVoidClient-Release/MuVoidClient
    let release_sibling = exe_dir().parent().map(|p| p.join("MuVoidClient-Release").join("MuVoidClient"));
    if let Some(ref p) = release_sibling {
        if p.join("version.json").exists() {
            return Some(p.clone());
        }
    }

    // Dev: exe en launcher/src-tauri/target/release, Release está en repo hermano
    let release_from_target = exe_dir()
        .ancestors()
        .nth(5)
        .map(|p| p.join("MuVoidClient-Release").join("MuVoidClient"));
    if let Some(ref p) = release_from_target {
        if p.join("version.json").exists() {
            return Some(p.clone());
        }
    }

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

// ── Network helpers (usadas solo para el self-update del launcher) ────────────

fn get_http_client() -> reqwest::blocking::Client {
    reqwest::blocking::Client::builder()
        .timeout(Duration::from_secs(10))
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
    if let Some(parent) = dest.parent() {
        fs::create_dir_all(parent).map_err(|e| e.to_string())?;
    }
    
    let client = get_http_client();
    let resp = client.get(url).send().map_err(|e| {
        log(&format!("Error descargando {}: {}", url, e));
        e.to_string()
    })?;

    if !resp.status().is_success() {
        return Err(format!("HTTP {}: {}", resp.status(), url));
    }
    let bytes = resp.bytes().map_err(|e| e.to_string())?;
    fs::write(dest, &bytes).map_err(|e| e.to_string())
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
    // 1. Local (desarrollo): directorio compilado
    if let Some(source) = find_client_source_dir() {
        let data = fs::read_to_string(source.join("version.json"))
            .map_err(|e| format!("Error leyendo version.json: {}", e))?;
        let data = strip_bom(&data);
        let manifest: VersionManifest =
            serde_json::from_str(data).map_err(|e| format!("version.json inválido: {}", e))?;
        return Ok(ClientInfo {
            version: manifest.version,
            changelog: manifest.changelog,
        });
    }

    // 2. Instalado: carpeta de instalación
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
    let ini = format!(
        "[LOGIN]\r\nVersion=1.03.34\r\nTestVersion=1.03.34\r\nRememberMe=0\r\nLanguage={}\r\nEncryptedUsername=\r\nEncryptedPassword=\r\n\
[PARTITION]\r\nVersion=357\r\n\
[Window]\r\nWidth={}\r\nHeight={}\r\nWindowed={}\r\n\
[Graphics]\r\nColorDepth=0\r\nRenderTextType=0\r\n\
[Audio]\r\nSoundEnabled={}\r\nMusicEnabled={}\r\nVolumeLevel={}\r\n",
        lang,
        g.window_width,
        g.window_height,
        if g.windowed { "1" } else { "0" },
        if g.sound_enabled { "1" } else { "0" },
        if g.music_enabled { "1" } else { "0" },
        g.volume_level.min(10)
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

/// Verifica si existe una nueva versión del launcher en GitHub.
#[tauri::command]
pub fn check_launcher_update() -> Result<bool, String> {
    let current = env!("CARGO_PKG_VERSION");
    log(&format!("Checking for launcher update... Current v{}", current));
    let url = format!("{}/launcher_version.json", launcher_base_url());
    let manifest: VersionManifest = fetch_json(&url)?;
    let has_update = manifest.version != current;
    log(&format!("Update check: New version v{}? {}", manifest.version, has_update));
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
#[tauri::command]
pub fn check_and_update_client(app: tauri::AppHandle) -> Result<(), String> {
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

    if let Some(source) = find_client_source_dir() {
        // Modo desarrollo: copiar desde directorio compilado local
        from_github = false;
        let data = fs::read_to_string(source.join("version.json"))
            .map_err(|e| format!("Error leyendo version.json: {}", e))?;
        let data = strip_bom(&data);
        manifest = serde_json::from_str(data).map_err(|e| format!("version.json inválido: {}", e))?;

        apply_manifest_from_local(&app, &manifest, &source, &install_dir)?;
    } else {
        // Modo distribución: descargar desde GitHub (rama client)
        from_github = true;
        let url = format!("{}/version.json", client_base_url());
        manifest = fetch_json(&url)?;

        apply_manifest_from_github(&app, &manifest, &install_dir)?;
    }

    // Escribir version.json instalado para trackear la versión de forma atómica
    save_installed_manifest(&install_dir, &manifest)?;

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
            true
        };

        if need_copy {
            let _ = app.emit("download-progress", DownloadProgress {
                current: i,
                total,
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
        }
        let _ = app.emit("download-progress", DownloadProgress {
            current: i + 1,
            total,
            file: entry.path.clone(),
            bytes_downloaded: 0,
            total_bytes: 0,
            speed_mbps: 0.0,
            eta_seconds: 0,
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
    let sep = std::path::MAIN_SEPARATOR_STR;

    // Calcular archivos a descargar en paralelo para evitar congelar el hilo principal
    let files_to_download: Vec<_> = manifest.files.par_iter().filter(|entry| {
        let dest_path = install_dir.join(entry.path.replace('/', sep));
        
        let exists = fs::metadata(&dest_path).is_ok();
        if !exists { return true; }

        if version_ok {
            compute_sha256(&dest_path).as_deref() != Some(entry.sha256.as_str())
        } else {
            true
        }
    }).collect();

    if files_to_download.is_empty() {
        return Ok(());
    }

    let total_files = files_to_download.len();
    let total_bytes: u64 = files_to_download.iter().map(|f| f.size).sum();
    let downloaded_files = Arc::new(AtomicUsize::new(0));
    let downloaded_bytes = Arc::new(AtomicU64::new(0));
    let last_emit = Arc::new(Mutex::new(Instant::now()));
    let start_time = Instant::now();

    // Pool de hilos moderado para evitar saturación del sistema, pero manteniendo alta concurrencia
    let pool = rayon::ThreadPoolBuilder::new().num_threads(32).build().map_err(|e| e.to_string())?;
    
    let app_shared = Arc::new(app.clone());
    let base_shared = Arc::new(base);
    let install_dir_shared = Arc::new(install_dir.to_path_buf());

    pool.install(|| {
        files_to_download.into_par_iter().for_each(|entry| {
            let dest_path = install_dir_shared.join(entry.path.replace('/', sep));
            let url = format!("{}/{}", base_shared, entry.path.replace('\\', "/"));

            if let Ok(_) = download_file(&url, &dest_path) {
                let current_files = downloaded_files.fetch_add(1, Ordering::SeqCst) + 1;
                let current_bytes = downloaded_bytes.fetch_add(entry.size, Ordering::SeqCst) + entry.size;
                
                let elapsed = start_time.elapsed().as_secs_f64();
                let speed = if elapsed > 0.0 { (current_bytes as f64 / 1024.0 / 1024.0) / elapsed } else { 0.0 };
                let eta = if speed > 0.0 {
                    ((total_bytes - current_bytes) as f64 / 1024.0 / 1024.0 / speed) as u64
                } else {
                    0
                };

                // Throttling: Solo emitir si han pasado al menos 100ms para no saturar el JS bridge
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
            }
        });
    });

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
