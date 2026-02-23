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

    public HudDocument Document
    {
        get => _document;
        set { _document = value; _selected = null; Invalidate(); }
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

    public event Action? SelectionChanged;
    public event Action<HudElement?>? HoveredElementChanged;
    public event Action<HudElement>? DragMoved;

    public void ClearCache()
    {
        foreach (var img in _cache.Values)
            img?.Dispose();
        _cache.Clear();
    }

    private (float scale, float offX, float offY) GetTransform()
    {
        var scale = Math.Min((float)ClientSize.Width / _document.GameWidth, (float)ClientSize.Height / _document.GameHeight);
        var offX = (ClientSize.Width - _document.GameWidth * scale) / 2;
        var offY = (ClientSize.Height - _document.GameHeight * scale) / 2;
        return (scale, offX, offY);
    }

    private (float x, float y) ScreenToGame(int sx, int sy)
    {
        var (scale, offX, offY) = GetTransform();
        return ((sx - offX) / scale, (sy - offY) / scale);
    }

    private HudElement? HitTest(float gx, float gy)
    {
        foreach (var el in _document.Elements.Where(e => e.Visible && e.Category == CurrentCategory).OrderByDescending(e => e.ZOrder))
        {
            if (el.Contains(gx, gy))
                return el;
        }
        return null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.FromArgb(25, 25, 35));
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

        if (string.IsNullOrEmpty(_baseFolder) || !Directory.Exists(_baseFolder))
        {
            var ifacePath = Paths.DefaultInterfaceFolder;
            e.Graphics.DrawString("Interface no cargada.", Font, Brushes.Orange, 20, 20);
            e.Graphics.DrawString($"Ruta esperada: {ifacePath}", new Font(Font.FontFamily, 8), Brushes.Gray, 20, 42);
            e.Graphics.DrawString("Archivo → Abrir carpeta Interface (o arrastra la carpeta aquí)", Font, Brushes.Gray, 20, 62);
            return;
        }

        var (scale, offX, offY) = GetTransform();
        e.Graphics.TranslateTransform(offX, offY);
        e.Graphics.ScaleTransform(scale, scale);

        foreach (var el in _document.Elements.Where(e => e.Visible && e.Category == CurrentCategory).OrderBy(e => e.ZOrder))
        {
            var path = HudDocument.ResolveImagePath(_baseFolder, el);
            if (!File.Exists(path)) continue;

            if (!_cache.TryGetValue(path, out var img))
            {
                img = OzImageLoader.Load(path);
                _cache[path] = img;
            }

            if (img != null)
            {
                e.Graphics.DrawImage(img, el.X, el.Y, el.Width, el.Height);
                if (el == _selected)
                {
                    using var pen = new Pen(Color.Cyan, 2);
                    e.Graphics.DrawRectangle(pen, el.X, el.Y, el.Width, el.Height);
                    var lbl = string.IsNullOrWhiteSpace(el.Label) ? el.FileName : el.Label;
                    using var sf = new StringFormat { Alignment = StringAlignment.Center };
                    e.Graphics.DrawString(lbl, new Font("Arial", 6), Brushes.Cyan,
                        new RectangleF(el.X, el.Y - 10, el.Width, 10), sf);
                }
                else if (el == _hovered)
                {
                    using var pen = new Pen(Color.Yellow, 1);
                    e.Graphics.DrawRectangle(pen, el.X, el.Y, el.Width, el.Height);
                }
            }
        }

        // Instrucciones de edición (barra inferior siempre visible)
        e.Graphics.ResetTransform();
        var hint = "Clic = seleccionar  |  Arrastra = mover  |  Panel derecho: X,Y,Ancho,Alto,Visible,Eliminar  |  Archivo → Guardar layout";
        using (var sf = new StringFormat { Alignment = StringAlignment.Center })
        {
            var hintRect = new RectangleF(0, ClientSize.Height - 24, ClientSize.Width, 22);
            using (var brush = new SolidBrush(Color.FromArgb(200, 20, 20, 30)))
                e.Graphics.FillRectangle(brush, hintRect);
            e.Graphics.DrawString(hint, new Font(Font.FontFamily, 8), Brushes.LightGray, hintRect, sf);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        var (gx, gy) = ScreenToGame(e.X, e.Y);
        var hit = HitTest(gx, gy);
        SelectedElement = hit;
        if (hit != null)
        {
            _isDragging = true;
            _dragStartX = e.X;
            _dragStartY = e.Y;
            _elemStartX = hit.X;
            _elemStartY = hit.Y;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var (gx, gy) = ScreenToGame(e.X, e.Y);
        var hit = _isDragging ? _selected : HitTest(gx, gy);
        if (hit != _hovered)
        {
            _hovered = hit;
            HoveredElementChanged?.Invoke(_hovered);
        }
        if (_isDragging && _selected != null)
        {
            var (scale, _, _) = GetTransform();
            var dx = (e.X - _dragStartX) / scale;
            var dy = (e.Y - _dragStartY) / scale;
            _selected.X = Math.Max(0, _elemStartX + dx);
            _selected.Y = Math.Max(0, _elemStartY + dy);
            _selected.X = Math.Min(_document.GameWidth - _selected.Width, _selected.X);
            _selected.Y = Math.Min(_document.GameHeight - _selected.Height, _selected.Y);
            Invalidate();
            // Solo notificar posición, sin redibujar propiedades completas durante el arrastre
            DragMoved?.Invoke(_selected);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
            _isDragging = false;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isDragging = false;
        if (_hovered != null) { _hovered = null; HoveredElementChanged?.Invoke(null); }
    }

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
