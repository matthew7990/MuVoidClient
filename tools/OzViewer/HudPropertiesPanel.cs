namespace OzViewer;

public class HudPropertiesPanel : UserControl
{
    private HudElement? _element;
    private TextBox? _txtX, _txtY, _txtW, _txtH, _txtFile;
    private ListBox? _listElements;
    private CheckBox? _chkVisible;
    private bool _updatingFromElement;

    public HudDocument? Document { get; set; }
    public string CurrentCategory { get; set; } = "HUD";

    public event Action? ElementChanged;
    public event Action<HudElement>? ElementSelected;

    public HudElement? SelectedElement
    {
        get => _element;
        set { _element = value; RefreshFromElement(); }
    }

    // ─────────────────────────────────────────────────────────────────
    public HudPropertiesPanel()
    {
        SuspendLayout();
        BackColor   = Color.FromArgb(38, 38, 50);
        ForeColor   = Color.White;
        Padding     = new Padding(8);
        MinimumSize = new Size(210, 0);

        int y = 8;

        // ── Título ────────────────────────────────────────────────────
        AddLabel("Elementos HUD", 8, y, bold: true, fontSize: 10f);
        y += 22;
        AddLabel("Clic en lista o canvas. Edita con el panel.", 8, y, color: Color.FromArgb(110, 110, 140), fontSize: 7.5f);
        y += 18;

        // ── Lista de elementos ────────────────────────────────────────
        _listElements = new ListBox
        {
            Location    = new Point(8, y),
            Width       = 188,
            Height      = 130,
            Font        = new Font("Consolas", 8f),
            BackColor   = Color.FromArgb(28, 28, 40),
            ForeColor   = Color.White,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false
        };
        _listElements.SelectedIndexChanged += OnListSelectionChanged;
        Controls.Add(_listElements);
        y += 136;

        // ── Separador ─────────────────────────────────────────────────
        var sep = new Panel
        {
            Location  = new Point(8, y),
            Size      = new Size(188, 1),
            BackColor = Color.FromArgb(60, 60, 80)
        };
        Controls.Add(sep);
        y += 8;

        // ── Propiedades de posición / tamaño ──────────────────────────
        AddLabel("Posición y tamaño", 8, y, color: Color.FromArgb(140, 140, 175), fontSize: 8f);
        y += 18;

        AddFieldWithNudge("X:",      ref _txtX!, y); y += 28;
        AddFieldWithNudge("Y:",      ref _txtY!, y); y += 28;
        AddFieldWithNudge("Ancho:",  ref _txtW!, y); y += 28;
        AddFieldWithNudge("Alto:",   ref _txtH!, y); y += 28;

        // ── Visibilidad ───────────────────────────────────────────────
        _chkVisible = new CheckBox
        {
            Text      = "Visible",
            Location  = new Point(8, y),
            AutoSize  = true,
            ForeColor = Color.FromArgb(180, 210, 180),
            Checked   = true
        };
        _chkVisible.CheckedChanged += (_, _) => { if (!_updatingFromElement) ApplyVisibleToElement(); };
        Controls.Add(_chkVisible);
        y += 28;

        // ── Eliminar ──────────────────────────────────────────────────
        var btnDelete = new Button
        {
            Text      = "Eliminar elemento",
            Location  = new Point(8, y),
            Size      = new Size(188, 26),
            BackColor = Color.FromArgb(80, 42, 42),
            ForeColor = Color.FromArgb(255, 160, 160),
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand
        };
        btnDelete.FlatAppearance.BorderColor = Color.FromArgb(110, 55, 55);
        btnDelete.Click += (_, _) => DeleteSelected();
        Controls.Add(btnDelete);
        y += 34;

        // ── Archivo ───────────────────────────────────────────────────
        var sep2 = new Panel
        {
            Location  = new Point(8, y),
            Size      = new Size(188, 1),
            BackColor = Color.FromArgb(60, 60, 80)
        };
        Controls.Add(sep2);
        y += 8;

        AddLabel("Archivo (FileName / AltPath):", 8, y, color: Color.FromArgb(140, 140, 175), fontSize: 8f);
        y += 18;
        _txtFile = new TextBox
        {
            Location    = new Point(8, y),
            Width       = 188,
            BackColor   = Color.FromArgb(28, 28, 40),
            ForeColor   = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = new Font("Consolas", 8f)
        };
        _txtFile.TextChanged += (_, _) => { if (!_updatingFromElement) ApplyToElement(); };
        Controls.Add(_txtFile);

        ResumeLayout(false);
    }

    // ─── Helpers de construcción de UI ────────────────────────────────
    private void AddLabel(string text, int x, int y,
        bool bold = false, float fontSize = 8.5f, Color? color = null)
    {
        var lbl = new Label
        {
            Text      = text,
            Location  = new Point(x, y),
            AutoSize  = true,
            ForeColor = color ?? Color.White,
            Font      = new Font("Segoe UI", fontSize, bold ? FontStyle.Bold : FontStyle.Regular)
        };
        Controls.Add(lbl);
    }

    private void AddFieldWithNudge(string label, ref TextBox field, int y)
    {
        var lbl = new Label
        {
            Text      = label,
            Location  = new Point(8, y + 3),
            Width     = 46,
            ForeColor = Color.FromArgb(180, 180, 205),
            Font      = new Font("Segoe UI", 8.5f)
        };
        Controls.Add(lbl);

        TextBox txt = new TextBox
        {
            Location    = new Point(54, y),
            Width       = 80,
            BackColor   = Color.FromArgb(28, 28, 40),
            ForeColor   = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = new Font("Consolas", 9f)
        };
        txt.TextChanged += (_, _) => { if (!_updatingFromElement) ApplyToElement(); };
        Controls.Add(txt);
        field = txt;

        // Botones ▲ ▼ para nudge ±1
        var btnUp = new Button
        {
            Text      = "▲",
            Location  = new Point(138, y),
            Size      = new Size(26, 13),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 50, 68),
            ForeColor = Color.FromArgb(160, 180, 220),
            Font      = new Font("Segoe UI", 6f),
            Cursor    = Cursors.Hand,
            TabStop   = false
        };
        btnUp.FlatAppearance.BorderSize  = 0;
        btnUp.Click += (_, _) => NudgeField(txt, +1);
        Controls.Add(btnUp);

        var btnDn = new Button
        {
            Text      = "▼",
            Location  = new Point(138, y + 13),
            Size      = new Size(26, 13),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 50, 68),
            ForeColor = Color.FromArgb(160, 180, 220),
            Font      = new Font("Segoe UI", 6f),
            Cursor    = Cursors.Hand,
            TabStop   = false
        };
        btnDn.FlatAppearance.BorderSize = 0;
        btnDn.Click += (_, _) => NudgeField(txt, -1);
        Controls.Add(btnDn);
    }

    private void NudgeField(TextBox txt, float delta)
    {
        if (float.TryParse(txt.Text, out var v))
            txt.Text = (v + delta).ToString("F0");
    }

    // ─── Eventos ──────────────────────────────────────────────────────
    private void OnListSelectionChanged(object? sender, EventArgs e)
    {
        if (_updatingFromElement || Document == null || _listElements == null) return;
        if (_listElements.SelectedIndex < 0) return;
        var filtered = Document.Elements.Where(el => el.Category == CurrentCategory).ToList();
        if (_listElements.SelectedIndex < filtered.Count)
            ElementSelected?.Invoke(filtered[_listElements.SelectedIndex]);
    }

    // ─── API pública ──────────────────────────────────────────────────
    public void UpdatePositionOnly(HudElement el)
    {
        if (_element != el) return;
        _updatingFromElement = true;
        try
        {
            _txtX!.Text = el.X.ToString("F0");
            _txtY!.Text = el.Y.ToString("F0");
        }
        finally { _updatingFromElement = false; }
    }

    public void RefreshElementList()
    {
        if (_listElements == null) return;
        _listElements.Items.Clear();
        if (Document == null) return;
        foreach (var el in Document.Elements.Where(e => e.Category == CurrentCategory))
        {
            var name = string.IsNullOrWhiteSpace(el.Label) ? Path.GetFileName(el.FileName) : el.Label;
            _listElements.Items.Add($"{name}  ({el.X:F0},{el.Y:F0})");
        }
    }

    public void RefreshFromElement()
    {
        _updatingFromElement = true;
        try
        {
            bool hasEl = _element != null;
            _txtX!.Enabled   = hasEl;
            _txtY!.Enabled   = hasEl;
            _txtW!.Enabled   = hasEl;
            _txtH!.Enabled   = hasEl;
            _txtFile!.Enabled = hasEl;
            if (_chkVisible != null) _chkVisible.Enabled = hasEl;

            if (!hasEl)
            {
                _txtX!.Text = _txtY!.Text = _txtW!.Text = _txtH!.Text = _txtFile!.Text = "";
                if (_chkVisible != null) _chkVisible.Checked = true;
                return;
            }

            _txtX!.Text    = _element!.X.ToString("F0");
            _txtY!.Text    = _element.Y.ToString("F0");
            _txtW!.Text    = _element.Width.ToString("F0");
            _txtH!.Text    = _element.Height.ToString("F0");
            _txtFile!.Text = _element.ResolvePath;
            if (_chkVisible != null) _chkVisible.Checked = _element.Visible;

            if (Document != null && _listElements != null)
            {
                var filtered = Document.Elements.Where(e => e.Category == CurrentCategory).ToList();
                var idx = filtered.IndexOf(_element);
                if (idx >= 0 && _listElements.SelectedIndex != idx)
                    _listElements.SelectedIndex = idx;
            }
        }
        finally { _updatingFromElement = false; }
    }

    // ─── Lógica interna ───────────────────────────────────────────────
    private void ApplyVisibleToElement()
    {
        if (_element == null || _chkVisible == null) return;
        _element.Visible = _chkVisible.Checked;
        ElementChanged?.Invoke();
    }

    private void DeleteSelected()
    {
        if (_element == null || Document == null) return;
        var idx = Document.Elements.IndexOf(_element);
        if (idx < 0) return;
        Document.Elements.RemoveAt(idx);
        SelectedElement = Document.Elements.Count > 0
            ? Document.Elements[Math.Min(idx, Document.Elements.Count - 1)]
            : null;
        RefreshElementList();
        ElementChanged?.Invoke();
    }

    private void ApplyToElement()
    {
        if (_element == null) return;
        if (float.TryParse(_txtX?.Text, out var x)) _element.X      = x;
        if (float.TryParse(_txtY?.Text, out var y)) _element.Y      = y;
        if (float.TryParse(_txtW?.Text, out var w) && w > 0) _element.Width  = w;
        if (float.TryParse(_txtH?.Text, out var h) && h > 0) _element.Height = h;

        if (_txtFile != null && !string.IsNullOrWhiteSpace(_txtFile.Text))
        {
            var t = _txtFile.Text.Trim();
            if (t.Contains('/') || t.Contains('\\'))
            {
                _element.AltPath = t;
            }
            else
            {
                _element.FileName = t.Contains('.') ? t : t + ".jpg";
                _element.AltPath  = null;
            }
        }
        ElementChanged?.Invoke();

        // Actualizar la entrada de la lista sin perder la selección
        if (_listElements != null && Document != null)
        {
            var selIdx = _listElements.SelectedIndex;
            RefreshElementList();
            if (selIdx >= 0 && selIdx < _listElements.Items.Count)
                _listElements.SelectedIndex = selIdx;
        }
    }
}
