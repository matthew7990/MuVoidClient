using System.Drawing.Drawing2D;

namespace OzViewer;

public class HudEditorPanel : Panel
{
    public HudEditorPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        UpdateStyles();
    }

    private HudDocument _document = HudDocument.CreateDefault();
    private string? _baseFolder;
    private readonly Dictionary<string, Image?> _cache = new();

    private HudElement? _selected;
    private HudElement? _hovered;
    private int _dragStartX, _dragStartY;
    private float _elemStartX, _elemStartY;
    private bool _isDragging;

    // ── Zoom & Pan ────────────────────────────────────────────────────
    private float _zoom = 1.0f;
    private float _panX = 0f, _panY = 0f;
    private bool _isPanning;
    private int _panStartX, _panStartY;
    private float _panStartOffX, _panStartOffY;

    // ── Grid & Snap ───────────────────────────────────────────────────
    public bool ShowGrid   { get; set; } = false;
    public bool SnapToGrid { get; set; } = false;
    public float GridStep  { get; set; } = 8f;

    // ── Preview de resolución ─────────────────────────────────────────
    public bool ShowResolutionPreview { get; set; } = false;
    public int PreviewWidth  { get; set; } = 1920;
    public int PreviewHeight { get; set; } = 1080;

    // ── Propiedades públicas ──────────────────────────────────────────
    public HudDocument Document
    {
        get => _document;
        set { _document = value; _selected = null; ResetView(); Invalidate(); }
    }

    public string CurrentCategory { get; set; } = "HUD";

    public string? BaseFolder
    {
        get => _baseFolder;
        set { _baseFolder = value; ClearCache(); Invalidate(); }
    }

    public HudElement? SelectedElement
    {
        get => _selected;
        set { _selected = value; SelectionChanged?.Invoke(); Invalidate(); }
    }

    public float Zoom => _zoom;

    public event Action? SelectionChanged;
    public event Action<HudElement?>? HoveredElementChanged;
    public event Action<HudElement>? DragMoved;
    public event Action? ZoomChanged;

    // ── Caché de imágenes ─────────────────────────────────────────────
    public void ClearCache()
    {
        foreach (var img in _cache.Values) img?.Dispose();
        _cache.Clear();
    }

    // ── Transformaciones ─────────────────────────────────────────────
    public void ResetView()
    {
        _zoom = 1.0f;
        _panX = 0f;
        _panY = 0f;
        Invalidate();
        ZoomChanged?.Invoke();
    }

    /// <summary>
    /// Canvas lógico según la categoría activa.
    /// Carga e Inicio usan 800×600 (base real del engine para esas pantallas).
    /// HUD/Inv/Personaje/Amigos usan 640×480.
    /// </summary>
    private (int w, int h) EffectiveCanvas =>
        CurrentCategory is "Carga" or "Inicio"
            ? (800, 600)
            : (_document.GameWidth, _document.GameHeight);

    private (float scale, float offX, float offY) GetBaseTransform()
    {
        var (cw, ch) = EffectiveCanvas;
        var scale = Math.Min(
            (float)ClientSize.Width  / cw,
            (float)ClientSize.Height / ch);
        var offX = (ClientSize.Width  - cw * scale) / 2f;
        var offY = (ClientSize.Height - ch * scale) / 2f;
        return (scale, offX, offY);
    }

    private (float scale, float offX, float offY) GetTransform()
    {
        var (bs, bx, by) = GetBaseTransform();
        return (bs * _zoom, bx + _panX, by + _panY);
    }

    private (float x, float y) ScreenToGame(int sx, int sy)
    {
        var (s, ox, oy) = GetTransform();
        return ((sx - ox) / s, (sy - oy) / s);
    }

    private float Snap(float v) =>
        SnapToGrid && GridStep > 0 ? MathF.Round(v / GridStep) * GridStep : v;

    private HudElement? HitTest(float gx, float gy)
    {
        foreach (var el in _document.Elements
                     .Where(e => e.Visible && e.Category == CurrentCategory)
                     .OrderByDescending(e => e.ZOrder))
        {
            if (el.Contains(gx, gy)) return el;
        }
        return null;
    }

    // ── Renderizado ───────────────────────────────────────────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(20, 20, 28));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode   = PixelOffsetMode.Half;

        if (string.IsNullOrEmpty(_baseFolder) || !Directory.Exists(_baseFolder))
        {
            DrawNoFolder(g);
            return;
        }

        var (scale, offX, offY) = GetTransform();
        var (ecW, ecH) = EffectiveCanvas;

        // Sombra y fondo del canvas de juego
        var cw = ecW * scale;
        var ch = ecH * scale;
        g.FillRectangle(new SolidBrush(Color.FromArgb(12, 12, 18)),
            new RectangleF(offX + 4, offY + 4, cw, ch));
        g.FillRectangle(new SolidBrush(Color.FromArgb(32, 32, 44)),
            new RectangleF(offX, offY, cw, ch));
        g.DrawRectangle(new Pen(Color.FromArgb(70, 70, 100), 1f),
            offX, offY, cw, ch);

        g.TranslateTransform(offX, offY);
        g.ScaleTransform(scale, scale);

        // Elementos
        foreach (var el in _document.Elements
                     .Where(e => e.Visible && e.Category == CurrentCategory)
                     .OrderBy(e => e.ZOrder))
        {
            if (!TryGetImage(el, out var img) || img == null) continue;

            g.DrawImage(img, el.X, el.Y, el.Width, el.Height);

            if (el == _selected)      DrawSelectionOverlay(g, el, scale);
            else if (el == _hovered)  DrawHoverOverlay(g, el, scale);
        }

        // Cuadrícula (encima de elementos, debajo de selección)
        if (ShowGrid && GridStep > 0) DrawGrid(g, scale);

        // Overlay de preview de resolución
        if (ShowResolutionPreview) DrawResolutionPreview(g, scale);

        g.ResetTransform();

        // HUD de estado (se dibuja sobre todo)
        DrawHudOverlays(g, scale, offX, offY);
    }

    private void DrawNoFolder(Graphics g)
    {
        var fBig   = new Font("Segoe UI", 11);
        var fSmall = new Font("Segoe UI", 8.5f);
        g.DrawString("Interface no cargada.", fBig, Brushes.Orange, 20, 20);
        g.DrawString($"Ruta esperada: {Paths.DefaultInterfaceFolder}",
            fSmall, Brushes.DimGray, 20, 46);
        g.DrawString("Archivo → Abrir carpeta Interface  (o arrastrá la carpeta aquí)",
            fSmall, new SolidBrush(Color.FromArgb(160, 160, 180)), 20, 64);
    }

    private bool TryGetImage(HudElement el, out Image? img)
    {
        var path = HudDocument.ResolveImagePath(_baseFolder!, el);
        if (!File.Exists(path)) { img = null; return false; }
        if (!_cache.TryGetValue(path, out img))
        {
            img = OzImageLoader.Load(path);
            _cache[path] = img;
        }
        return true;
    }

    private static void DrawSelectionOverlay(Graphics g, HudElement el, float scale)
    {
        float pw = 1.5f / scale;
        using var pen = new Pen(Color.FromArgb(0, 210, 255), pw);
        g.DrawRectangle(pen, el.X, el.Y, el.Width, el.Height);

        // Handles en las 4 esquinas
        float hs = 4f / scale;
        using var hBrush = new SolidBrush(Color.FromArgb(0, 210, 255));
        foreach (float hx in new[] { el.X, el.X + el.Width })
            foreach (float hy in new[] { el.Y, el.Y + el.Height })
                g.FillRectangle(hBrush, hx - hs / 2, hy - hs / 2, hs, hs);

        // Etiqueta encima
        var lbl = string.IsNullOrWhiteSpace(el.Label) ? el.FileName : el.Label;
        float fs  = Math.Max(5f, 7.5f / scale);
        using var sf = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString(lbl, new Font("Segoe UI", fs), Brushes.Cyan,
            new RectangleF(el.X, el.Y - 11f / scale, el.Width, 11f / scale), sf);
    }

    private static void DrawHoverOverlay(Graphics g, HudElement el, float scale)
    {
        using var pen = new Pen(Color.FromArgb(255, 200, 50), 1f / scale);
        g.DrawRectangle(pen, el.X, el.Y, el.Width, el.Height);
    }

    private void DrawGrid(Graphics g, float scale)
    {
        var (ecW, ecH) = EffectiveCanvas;
        using var pen = new Pen(Color.FromArgb(30, 255, 255, 255), 0.5f / scale);
        for (float gx = 0; gx <= ecW; gx += GridStep)
            g.DrawLine(pen, gx, 0, gx, ecH);
        for (float gy = 0; gy <= ecH; gy += GridStep)
            g.DrawLine(pen, 0, gy, ecW, gy);
    }

    private void DrawResolutionPreview(Graphics g, float scale)
    {
        var (ecW, ecH) = EffectiveCanvas;
        float sX = (float)PreviewWidth  / ecW;
        float sY = (float)PreviewHeight / ecH;

        // Borde del canvas con color de acento naranja
        using var pen = new Pen(Color.FromArgb(140, 255, 120, 0), 1.5f / scale)
        { DashStyle = DashStyle.Dash };
        g.DrawRectangle(pen, 0, 0, ecW, ecH);

        float fs = Math.Max(5f, 8f / scale);
        string info = $"Preview {PreviewWidth}×{PreviewHeight}   ScaleX={sX:F2}×  ScaleY={sY:F2}×";
        g.DrawString(info, new Font("Segoe UI", fs, FontStyle.Bold),
            new SolidBrush(Color.FromArgb(220, 255, 140, 0)), 3f, 3f);

        // Si el escalado es no-uniforme, advertir
        if (Math.Abs(sX - sY) > 0.01f)
        {
            string warn = "⚠ Escala no uniforme: los elementos se deformarán en el juego";
            g.DrawString(warn, new Font("Segoe UI", Math.Max(5f, 7f / scale)),
                new SolidBrush(Color.FromArgb(200, 255, 80, 80)),
                3f, 3f + 14f / scale);
        }
    }

    private void DrawHudOverlays(Graphics g, float scale, float offX, float offY)
    {
        // Barra de estado inferior
        const string hint =
            "Clic=seleccionar  ·  Drag=mover  ·  G=grid  ·  S=snap  ·  Ctrl+Rueda=zoom  " +
            "·  Botón medio/Space+Drag=pan  ·  Flechas=mover 1px  ·  0=reset zoom  ·  Del=eliminar";
        var hintRect = new RectangleF(0, ClientSize.Height - 22, ClientSize.Width, 22);
        using var bgBrush = new SolidBrush(Color.FromArgb(210, 16, 16, 24));
        g.FillRectangle(bgBrush, hintRect);
        using var sf = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString(hint, new Font("Segoe UI", 7.5f),
            new SolidBrush(Color.FromArgb(170, 170, 195)), hintRect, sf);

        // Indicador de zoom (esquina superior derecha del canvas)
        string zoomTxt = $"  {_zoom * 100:F0}%  ";
        var zRect = new RectangleF(ClientSize.Width - 68, 4, 64, 18);
        using var zBg = new SolidBrush(Color.FromArgb(190, 28, 28, 42));
        g.FillRectangle(zBg, zRect);
        g.DrawString(zoomTxt, new Font("Segoe UI", 7.5f),
            new SolidBrush(Color.FromArgb(180, 200, 220)), zRect.X + 2, zRect.Y + 2);

        // Indicadores de estado (grid/snap)
        if (ShowGrid || SnapToGrid)
        {
            string flags = (ShowGrid ? "Grid " : "") + (SnapToGrid ? $"Snap({GridStep}px)" : "");
            g.DrawString(flags, new Font("Segoe UI", 7.5f),
                new SolidBrush(Color.FromArgb(160, 100, 220, 120)), 6, 6);
        }
    }

    // ── Eventos de ratón ──────────────────────────────────────────────
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        bool wantPan = e.Button == MouseButtons.Middle ||
                       (e.Button == MouseButtons.Left && ModifierKeys.HasFlag(Keys.Space));
        if (wantPan)
        {
            _isPanning = true;
            _panStartX   = e.X; _panStartY   = e.Y;
            _panStartOffX = _panX; _panStartOffY = _panY;
            Cursor = Cursors.SizeAll;
            return;
        }

        if (e.Button != MouseButtons.Left) return;

        var (gx, gy) = ScreenToGame(e.X, e.Y);
        var hit = HitTest(gx, gy);
        SelectedElement = hit;
        if (hit != null)
        {
            _isDragging  = true;
            _dragStartX  = e.X; _dragStartY  = e.Y;
            _elemStartX  = hit.X; _elemStartY = hit.Y;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isPanning)
        {
            _panX = _panStartOffX + (e.X - _panStartX);
            _panY = _panStartOffY + (e.Y - _panStartY);
            Invalidate();
            return;
        }

        var (gx, gy) = ScreenToGame(e.X, e.Y);
        var hit = _isDragging ? _selected : HitTest(gx, gy);
        if (hit != _hovered)
        {
            _hovered = hit;
            HoveredElementChanged?.Invoke(_hovered);
        }

        if (_isDragging && _selected != null)
        {
            var (s, _, _) = GetTransform();
            var (ecW, ecH) = EffectiveCanvas;
            var newX = Snap(Math.Clamp(_elemStartX + (e.X - _dragStartX) / s,
                                       0, ecW - _selected.Width));
            var newY = Snap(Math.Clamp(_elemStartY + (e.Y - _dragStartY) / s,
                                       0, ecH - _selected.Height));
            _selected.X = newX;
            _selected.Y = newY;
            Invalidate();
            DragMoved?.Invoke(_selected);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
        {
            _isDragging = false;
            _isPanning  = false;
            Cursor = Cursors.Default;
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isDragging = false;
        _isPanning  = false;
        Cursor = Cursors.Default;
        if (_hovered != null) { _hovered = null; HoveredElementChanged?.Invoke(null); }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!ModifierKeys.HasFlag(Keys.Control)) return;

        var (bs, bx, by) = GetBaseTransform();
        var (oldScale, oldOx, oldOy) = GetTransform();

        float factor  = e.Delta > 0 ? 1.15f : 1f / 1.15f;
        float newZoom = Math.Clamp(_zoom * factor, 0.15f, 10f);
        float newScale = bs * newZoom;

        // Mantener el punto bajo el cursor fijo
        float gameX = (e.X - oldOx) / oldScale;
        float gameY = (e.Y - oldOy) / oldScale;
        _panX = e.X - bx - gameX * newScale;
        _panY = e.Y - by - gameY * newScale;
        _zoom = newZoom;

        Invalidate();
        ZoomChanged?.Invoke();
    }

    // ── Eventos de teclado ────────────────────────────────────────────
    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Delete or Keys.G or Keys.S
        or Keys.Left or Keys.Right or Keys.Up or Keys.Down
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.G:
                ShowGrid = !ShowGrid;
                Invalidate();
                break;
            case Keys.S:
                SnapToGrid = !SnapToGrid;
                Invalidate();
                break;
            case Keys.D0:
            case Keys.NumPad0:
                ResetView();
                break;
            case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
                MoveSelectedByKey(e.KeyCode);
                e.Handled = true;
                break;
        }
    }

    private void MoveSelectedByKey(Keys key)
    {
        if (_selected == null) return;
        float step = SnapToGrid ? GridStep : 1f;
        var (ecW, ecH) = EffectiveCanvas;
        switch (key)
        {
            case Keys.Left:
                _selected.X = Math.Max(0, _selected.X - step); break;
            case Keys.Right:
                _selected.X = Math.Min(ecW - _selected.Width, _selected.X + step); break;
            case Keys.Up:
                _selected.Y = Math.Max(0, _selected.Y - step); break;
            case Keys.Down:
                _selected.Y = Math.Min(ecH - _selected.Height, _selected.Y + step); break;
        }
        DragMoved?.Invoke(_selected);
        Invalidate();
    }

    // ── API pública ───────────────────────────────────────────────────
    public void UpdateElement(HudElement el, float x, float y, float w, float h)
    {
        el.X = x; el.Y = y; el.Width = w; el.Height = h;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ClearCache();
        base.Dispose(disposing);
    }
}
