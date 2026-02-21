import { invoke } from '@tauri-apps/api/core';
import { listen } from '@tauri-apps/api/event';
import { getCurrentWindow } from '@tauri-apps/api/window';

// ── Window controls ───────────────────────────────────────────────────────────
const appWindow = getCurrentWindow();
document.getElementById('btn-minimize').onclick = () => appWindow.minimize();
document.getElementById('btn-close').onclick = () => appWindow.close();

// ── DOM refs ──────────────────────────────────────────────────────────────────
const serverDot = document.getElementById('server-dot');
const serverLabel = document.getElementById('server-label');
const playerCount = document.getElementById('player-count');

const newsItems = document.getElementById('news-items');
const versionTag = document.getElementById('version-tag');

const clientPathEl = document.getElementById('client-path');
const selectFolderBtn = document.getElementById('select-folder-btn');
const openFolderBtn = document.getElementById('open-folder-btn');

const progressWrap = document.getElementById('progress-wrap');
const progressFill = document.getElementById('progress-fill');

const statusMsg = document.getElementById('status-msg');
const retryBtn = document.getElementById('retry-btn');
const playBtn = document.getElementById('play-btn');
const logConsole = document.getElementById('log-console');

const discordBtn = document.getElementById('discord-btn');
const webBtn = document.getElementById('web-btn');
const settingsBtn = document.getElementById('settings-btn');
const settingsModal = document.getElementById('settings-modal');
const settingsCloseBtn = document.getElementById('settings-close-btn');
const settingsSaveBtn = document.getElementById('settings-save-btn');

// ── Helpers ───────────────────────────────────────────────────────────────────
function setStatus(text, type = 'info') {
  statusMsg.textContent = text;
  statusMsg.className = type;
}
function clearStatus() {
  statusMsg.className = '';
  statusMsg.textContent = '';
}

function showProgress(indeterminate = true, pct = 0) {
  progressWrap.classList.add('visible');
  if (indeterminate) {
    progressFill.classList.add('indeterminate');
    progressFill.style.width = '';
  } else {
    progressFill.classList.remove('indeterminate');
    progressFill.style.width = pct + '%';
  }
}
function hideProgress() {
  progressWrap.classList.remove('visible');
  progressFill.classList.remove('indeterminate');
  progressFill.style.width = '0%';
}

function addLog(msg) {
  const time = new Date().toLocaleTimeString();
  const line = `[${time}] ${msg}\n`;
  logConsole.innerText += line;
  logConsole.scrollTop = logConsole.scrollHeight;
}

function setPlaying(label = 'JUGAR') {
  playBtn.disabled = false;
  playBtn.classList.remove('updating');
  playBtn.textContent = label;
}
function setUpdating(label = 'ACTUALIZANDO...') {
  playBtn.disabled = true;
  playBtn.classList.add('updating');
  playBtn.textContent = label;
}

function showRetry(show = true) {
  retryBtn.classList.toggle('visible', show);
}

// ── Changelog rendering ───────────────────────────────────────────────────────
function renderChangelog(changelog, version) {
  if (version) {
    versionTag.textContent = 'v' + version;
  }

  if (!changelog || changelog.length === 0) {
    newsItems.innerHTML = `
      <div class="news-item">
        <div class="news-text"><strong>Bienvenido a MuVoid</strong>
          <span>El launcher descarga y mantiene el cliente actualizado automáticamente.</span>
        </div>
      </div>
      <div class="news-item">
        <div class="news-text"><strong>Sin instalación necesaria</strong>
          <span>Solo descarga el launcher y presiona Jugar.</span>
        </div>
      </div>`;
    return;
  }

  newsItems.innerHTML = changelog
    .map(item => `
      <div class="news-item">
        <div class="news-text">${escapeHtml(item)}</div>
      </div>`)
    .join('');
}

function escapeHtml(str) {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

// ── Server status ─────────────────────────────────────────────────────────────
async function refreshServerStatus() {
  try {
    const online = await invoke('check_server_online');
    serverDot.className = 'dot ' + (online ? 'online' : 'offline');
    serverLabel.textContent = online ? 'Servidor Online' : 'Servidor Offline';
  } catch {
    serverDot.className = 'dot offline';
    serverLabel.textContent = 'Sin conexión';
  }
}

// ── Carpeta de instalación (la que el usuario seleccionó, no la fuente) ───────
async function loadClientPath() {
  try {
    const path = await invoke('get_client_path');
    clientPathEl.value = path;
  } catch {
    clientPathEl.value = '—';
  }
}

// ── Main init ─────────────────────────────────────────────────────────────────
async function init() {
  // Listen to download progress events from Rust
  await listen('download-progress', (event) => {
    const { current, total, file, bytes_downloaded, total_bytes, speed_mbps, eta_seconds } = event.payload;
    if (total > 0) {
      showProgress(false, Math.round((current / total) * 100));
    }

    let msg = 'Actualizando: ' + file;
    if (speed_mbps > 0) {
      const mb = (bytes_downloaded / 1024 / 1024).toFixed(1);
      const totalMb = (total_bytes / 1024 / 1024).toFixed(1);
      msg = `Descargando: ${mb}MB / ${totalMb}MB (${speed_mbps.toFixed(1)} MB/s) - ETA: ${eta_seconds}s`;
    }
    setStatus(msg, 'info');
  });

  // Listen for logs from Rust (SETUP FIRST)
  await listen('launcher-log', (event) => {
    if (logConsole.innerText === 'Esperando órdenes...') logConsole.innerText = '';
    addLog(event.payload);
  });

  addLog('Iniciando Launcher...');

  // Load client install path
  await loadClientPath();

  // Check server status (non-blocking)
  refreshServerStatus();
  // Refresh every 60 s
  setInterval(refreshServerStatus, 60_000);

  // Check launcher self-update (MANDATORY)
  try {
    const hasUpdate = await invoke('check_launcher_update');
    if (hasUpdate) {
      setStatus('Actualizando Launcher...', 'info');
      setUpdating('ACTUALIZANDO LAUNCHER...');
      // Iniciar descarga del launcher automáticamente
      await invoke('start_launcher_update');
      return; // El launcher se cerrará solo
    }
  } catch (e) {
    console.warn('check_launcher_update:', e);
  }

  // Fetch client info (changelog)
  let manifestVersion = null;
  try {
    const info = await invoke('get_client_info');
    manifestVersion = info.version;
    renderChangelog(info.changelog, info.version);
  } catch (e) {
    renderChangelog([], null);
    setStatus('Error al obtener info: ' + (e?.message ?? e), 'error');
  }

  // Update button label based on installation status
  const installed = await invoke('is_client_installed');
  if (!installed) {
    setPlaying('INICIAR DESCARGA');
  } else {
    // Verificar si hay update pendiente
    setPlaying('VERIFICANDO...');
    addLog('Comprobando actualizaciones del cliente...');
    await runClientUpdate(true); // silent check
  }
}

async function runClientUpdate(silent = false) {
  if (!silent) {
    clearStatus();
    showRetry(false);
    setUpdating('ACTUALIZANDO...');
    showProgress(true);
  } else {
    // Mostrar barra indeterminada para que el usuario vea que no está colgado
    showProgress(true);
  }

  try {
    await invoke('check_and_update_client');
    hideProgress();
    clearStatus();
    await loadClientPath(); // Refrescar ruta por si cambió
    setPlaying('JUGAR');
  } catch (e) {
    hideProgress();
    if (e === "LAUNCHER_UPDATE_REQUIRED") {
      setStatus('Actualizando Launcher...', 'info');
      await invoke('start_launcher_update');
      return;
    }
    if (!silent) {
      setStatus('Error: ' + (e?.message ?? e), 'error');
      showRetry(true);
      setPlaying('REINTENTAR');
    } else {
      const installed = await invoke('is_client_installed');
      setPlaying(installed ? 'JUGAR' : 'INICIAR DESCARGA');
    }
  }
}

// ── Button handlers ───────────────────────────────────────────────────────────
playBtn.onclick = async () => {
  const text = playBtn.textContent;
  if (text === 'INICIAR DESCARGA' || text === 'ACTUALIZAR' || text === 'REINTENTAR') {
    // Si es la primera descarga, preguntar dónde guardar
    if (text === 'INICIAR DESCARGA') {
      const path = await invoke('select_install_directory');
      if (!path) return; // Usuario canceló
      clientPathEl.value = path;
    }
    runClientUpdate();
    return;
  }

  try {
    await invoke('launch_game');
  } catch (e) {
    setStatus('Error al iniciar: ' + (e?.message ?? e), 'error');
  }
};

retryBtn.onclick = () => runClientUpdate();

selectFolderBtn.onclick = async () => {
  try {
    const path = await invoke('select_install_directory');
    if (path) {
      clientPathEl.value = path;
      addLog('Carpeta de instalación cambiada a: ' + path);
    }
  } catch (e) {
    setStatus('Error al seleccionar carpeta: ' + (e?.message ?? e), 'error');
  }
};

openFolderBtn.onclick = async () => {
  try { await invoke('open_client_folder'); } catch { /* ignore */ }
};

discordBtn.onclick = () =>
  invoke('open_url', { url: 'https://discord.gg/muvoid' }).catch(() => { });

webBtn.onclick = () =>
  invoke('open_url', { url: 'https://muvoid.com' }).catch(() => { });

// ── Settings modal ───────────────────────────────────────────────────────────
async function openSettings() {
  try {
    const s = await invoke('get_game_settings');
    document.getElementById('cfg-language').value = s.language || 'SPN';
    document.getElementById('cfg-width').value = s.window_width || 1024;
    document.getElementById('cfg-height').value = s.window_height || 768;
    document.getElementById('cfg-windowed').checked = s.windowed !== false;
    document.getElementById('cfg-sound').checked = s.sound_enabled !== false;
    document.getElementById('cfg-music').checked = s.music_enabled === true;
    document.getElementById('cfg-volume').value = s.volume_level ?? 5;
    document.getElementById('cfg-volume-val').textContent = s.volume_level ?? 5;
  } catch (e) {
    setStatus('Error al cargar configuración: ' + (e?.message ?? e), 'error');
  }
  settingsModal.classList.add('visible');
}

function closeSettings() {
  settingsModal.classList.remove('visible');
}

settingsBtn.onclick = openSettings;
settingsCloseBtn.onclick = closeSettings;
settingsModal.onclick = (e) => { if (e.target === settingsModal) closeSettings(); };

document.getElementById('cfg-volume').oninput = (e) => {
  document.getElementById('cfg-volume-val').textContent = e.target.value;
};

settingsSaveBtn.onclick = async () => {
  try {
    await invoke('save_game_settings', {
      settings: {
        window_width: parseInt(document.getElementById('cfg-width').value) || 1024,
        window_height: parseInt(document.getElementById('cfg-height').value) || 768,
        windowed: document.getElementById('cfg-windowed').checked,
        sound_enabled: document.getElementById('cfg-sound').checked,
        music_enabled: document.getElementById('cfg-music').checked,
        volume_level: parseInt(document.getElementById('cfg-volume').value) || 5,
        language: document.getElementById('cfg-language').value || 'SPN',
      },
    });
    addLog('Configuración guardada');
    closeSettings();
  } catch (e) {
    setStatus('Error al guardar: ' + (e?.message ?? e), 'error');
  }
};

const clearLogsBtn = document.getElementById('clear-logs-btn');
if (clearLogsBtn) {
  clearLogsBtn.onclick = () => {
    logConsole.innerText = '';
  };
}

// ── Start ─────────────────────────────────────────────────────────────────────
init();
