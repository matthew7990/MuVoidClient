import { invoke } from '@tauri-apps/api/core';
import { listen } from '@tauri-apps/api/event';
import { getCurrentWindow } from '@tauri-apps/api/window';

// ── Window controls ───────────────────────────────────────────────────────────
const appWindow = getCurrentWindow();
document.getElementById('btn-minimize').onclick = () => appWindow.minimize();
document.getElementById('btn-close').onclick    = () => appWindow.close();

// ── DOM refs ──────────────────────────────────────────────────────────────────
const launcherBanner   = document.getElementById('launcher-banner');
const launcherBannerMsg = document.getElementById('launcher-banner-msg');
const launcherUpdateBtn = document.getElementById('launcher-update-btn');

const serverDot    = document.getElementById('server-dot');
const serverLabel  = document.getElementById('server-label');
const playerCount  = document.getElementById('player-count');

const newsItems    = document.getElementById('news-items');
const versionTag   = document.getElementById('version-tag');

const clientPathEl = document.getElementById('client-path');
const openFolderBtn = document.getElementById('open-folder-btn');

const progressWrap = document.getElementById('progress-wrap');
const progressFill = document.getElementById('progress-fill');

const statusMsg    = document.getElementById('status-msg');
const retryBtn     = document.getElementById('retry-btn');
const playBtn      = document.getElementById('play-btn');

const discordBtn   = document.getElementById('discord-btn');
const webBtn       = document.getElementById('web-btn');

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

function setPlaying(enabled) {
  playBtn.disabled = !enabled;
  playBtn.classList.remove('updating');
  playBtn.textContent = 'JUGAR';
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
    serverDot.className   = 'dot ' + (online ? 'online' : 'offline');
    serverLabel.textContent = online ? 'Servidor Online' : 'Servidor Offline';
  } catch {
    serverDot.className   = 'dot offline';
    serverLabel.textContent = 'Sin conexión';
  }
}

// ── Client source path (directorio compilado detectado) ──────────────────────
async function loadClientPath() {
  try {
    const path = await invoke('get_client_source_path');
    clientPathEl.value = path;
  } catch {
    clientPathEl.value = '—';
  }
}

// ── Main init ─────────────────────────────────────────────────────────────────
async function init() {
  // Load client install path
  await loadClientPath();

  // Check server status (non-blocking)
  refreshServerStatus();
  // Refresh every 60 s
  setInterval(refreshServerStatus, 60_000);

  // Check launcher self-update
  try {
    const hasUpdate = await invoke('check_launcher_update');
    if (hasUpdate) {
      launcherBanner.classList.add('visible');
      launcherBannerMsg.textContent = 'Una nueva versión del launcher está disponible.';
    }
  } catch (e) {
    console.warn('check_launcher_update:', e);
  }

  // Fetch client info (changelog)
  try {
    const info = await invoke('get_client_info');
    renderChangelog(info.changelog, info.version);
  } catch (e) {
    renderChangelog([], null);
    setStatus('Cliente compilado no detectado: ' + (e?.message ?? e), 'error');
    showRetry(true);
  }

  // Listen to download progress events from Rust
  await listen('download-progress', (event) => {
    const { current, total, file } = event.payload;
    if (total > 0) {
      showProgress(false, Math.round((current / total) * 100));
    }
    if (file) {
      setStatus('Descargando: ' + file, 'info');
    }
  });

  // Try to update client files
  await runClientUpdate();
}

async function runClientUpdate() {
  clearStatus();
  showRetry(false);
  setUpdating('VERIFICANDO...');
  showProgress(true);

  try {
    await invoke('check_and_update_client');
    hideProgress();
    clearStatus();
    setPlaying(true);
  } catch (e) {
    hideProgress();
    setStatus('Error al verificar cliente: ' + (e?.message ?? e), 'error');
    showRetry(true);
    setPlaying(false);
    playBtn.disabled = true;
    playBtn.classList.remove('updating');
    playBtn.textContent = 'JUGAR';
  }
}

// ── Button handlers ───────────────────────────────────────────────────────────
playBtn.onclick = async () => {
  try {
    await invoke('launch_game');
  } catch (e) {
    setStatus('Error al iniciar el juego: ' + (e?.message ?? e), 'error');
  }
};

retryBtn.onclick = () => runClientUpdate();

openFolderBtn.onclick = async () => {
  try { await invoke('open_client_folder'); } catch { /* ignore */ }
};

discordBtn.onclick = () =>
  invoke('open_url', { url: 'https://discord.gg/muvoid' }).catch(() => {});

webBtn.onclick = () =>
  invoke('open_url', { url: 'https://muvoid.com' }).catch(() => {});

launcherUpdateBtn.onclick = async () => {
  launcherUpdateBtn.disabled = true;
  launcherUpdateBtn.textContent = 'ACTUALIZANDO...';
  try {
    await invoke('start_launcher_update');
  } catch (e) {
    setStatus('Error al actualizar launcher: ' + (e?.message ?? e), 'error');
    launcherUpdateBtn.disabled = false;
    launcherUpdateBtn.textContent = 'ACTUALIZAR';
  }
};

// ── Start ─────────────────────────────────────────────────────────────────────
init();
