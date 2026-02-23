namespace OzViewer;

public class HudPropertiesPanel : UserControl
{
    private HudElement? _element;
    private TextBox? _txtX, _txtY, _txtW, _txtH, _txtFile;
    private ListBox? _listElements;
    private CheckBox? _chkVisible;
    private bool _updatingFromElement;

    public HudDocument? Document { get; set; }
    public event Action? ElementChanged;
    public HudElement? SelectedElement
    {
        get => _element;
        set { _element = value; RefreshFromElement(); }
    }
    public event Action<HudElement>? ElementSelected;

    public HudPropertiesPanel()
    {
        SuspendLayout();
        BackColor = Color.FromArgb(45, 45, 55);
        ForeColor = Color.White;
        Padding = new Padding(8);
        MinimumSize = new Size(200, 0);

        var lblTitle = new Label { Text = "Elementos HUD", Font = new Font(Font.FontFamily, 10, FontStyle.Bold), AutoSize = true };
        lblTitle.Location = new Point(8, 8);
        Controls.Add(lblTitle);

        var lblHint = new Label { Text = "Clic en la lista o en el canvas. Edita X, Y, Ancho, Alto.", Font = new Font(Font.FontFamily, 7), ForeColor = Color.Gray, AutoSize = true };
        lblHint.Location = new Point(8, 24);
        Controls.Add(lblHint);

        _listElements = new ListBox
        {
            Location = new Point(8, 42),
            Width = 180,
            Height = 120,
            Font = new Font("Consolas", 8f),
            BackColor = Color.FromArgb(35, 35, 45),
            ForeColor = Color.White
        };
        _listElements.SelectedIndexChanged += (_, _) =>
        {
            if (_updatingFromElement) return;
            if (Document != null && _listElements.SelectedIndex >= 0 && _listElements.SelectedIndex < Document.Elements.Count)
                ElementSelected?.Invoke(Document.Elements[_listElements.SelectedIndex]);
        };
        Controls.Add(_listElements);

        int y = 167;
        void AddRow(string label, out TextBox txt)
        {
            var lbl = new Label { Text = label, Location = new Point(8, y), AutoSize = true };
            txt = new TextBox
            {
                Location = new Point(70, y - 2),
                Width = 100,
                BackColor = Color.FromArgb(35, 35, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            txt.TextChanged += (_, _) => { if (!_updatingFromElement) ApplyToElement(); };
            Controls.Add(lbl);
            Controls.Add(txt);
            y += 28;
        }

        AddRow("X:", out _txtX!);
        AddRow("Y:", out _txtY!);
        AddRow("Ancho:", out _txtW!);
        AddRow("Alto:", out _txtH!);

        _chkVisible = new CheckBox
        {
            Text = "Visible",
            Location = new Point(8, y),
            AutoSize = true,
            ForeColor = Color.White,
            Checked = true
        };
        _chkVisible.CheckedChanged += (_, _) => { if (!_updatingFromElement) ApplyVisibleToElement(); };
        Controls.Add(_chkVisible);
        y += 26;

        var btnDelete = new Button
        {
            Text = "Eliminar elemento",
            Location = new Point(8, y),
            Size = new Size(172, 24),
            BackColor = Color.FromArgb(80, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnDelete.Click += (_, _) => DeleteSelected();
        Controls.Add(btnDelete);
        y += 32;

        var lblFile = new Label { Text = "Archivo:", Location = new Point(8, y), AutoSize = true };
        _txtFile = new TextBox
        {
            Location = new Point(8, y + 18),
            Width = 172,
            BackColor = Color.FromArgb(35, 35, 45),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtFile.TextChanged += (_, _) => { if (!_updatingFromElement) ApplyToElement(); };
        Controls.Add(lblFile);
        Controls.Add(_txtFile);

        ResumeLayout(false);
    }

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
        _listElements?.Items.Clear();
        if (Document == null) return;
        foreach (var el in Document.Elements)
        {
            var name = string.IsNullOrWhiteSpace(el.Label) ? Path.GetFileName(el.FileName) : el.Label;
            _listElements!.Items.Add($"{name} ({el.X:F0},{el.Y:F0})");
        }
    }

    public void RefreshFromElement()
    {
        _updatingFromElement = true;
        try
        {
            if (_element == null)
            {
                _txtX!.Text = _txtY!.Text = _txtW!.Text = _txtH!.Text = _txtFile!.Text = "";
                _txtX.Enabled = _txtY.Enabled = _txtW.Enabled = _txtH.Enabled = _txtFile.Enabled = false;
                if (_chkVisible != null) { _chkVisible.Enabled = false; _chkVisible.Checked = true; }
            }
            else
            {
                _txtX!.Text = _element.X.ToString("F0");
                _txtY!.Text = _element.Y.ToString("F0");
                _txtW!.Text = _element.Width.ToString("F0");
                _txtH!.Text = _element.Height.ToString("F0");
                _txtFile!.Text = _element.ResolvePath;
                _txtX.Enabled = _txtY.Enabled = _txtW.Enabled = _txtH.Enabled = _txtFile.Enabled = true;
                if (_chkVisible != null) { _chkVisible.Enabled = true; _chkVisible.Checked = _element.Visible; }

                if (Document != null && _listElements != null)
                {
                    var idx = Document.Elements.IndexOf(_element);
                    if (idx >= 0 && _listElements.SelectedIndex != idx)
                        _listElements.SelectedIndex = idx;
                }
            }
        }
        finally { _updatingFromElement = false; }
    }

    private void ApplyVisibleToElement()
    {
        if (_element != null && _chkVisible != null)
        {
            _element.Visible = _chkVisible.Checked;
            ElementChanged?.Invoke();
        }
    }

    private void DeleteSelected()
    {
        if (_element == null || Document == null) return;
        var idx = Document.Elements.IndexOf(_element);
        if (idx < 0) return;
        Document.Elements.RemoveAt(idx);
        SelectedElement = Document.Elements.Count > 0 ? Document.Elements[Math.Min(idx, Document.Elements.Count - 1)] : null;
        RefreshElementList();
        ElementChanged?.Invoke();
    }

    private void ApplyToElement()
    {
        if (_element == null) return;
        if (float.TryParse(_txtX?.Text, out var x)) _element.X = x;
        if (float.TryParse(_txtY?.Text, out var y)) _element.Y = y;
        if (float.TryParse(_txtW?.Text, out var w) && w > 0) _element.Width = w;
        if (float.TryParse(_txtH?.Text, out var h) && h > 0) _element.Height = h;
        if (_txtFile != null && !string.IsNullOrWhiteSpace(_txtFile.Text))
        {
            var t = _txtFile.Text.Trim();
            if (t.Contains('/') || t.Contains('\\'))
                _element.AltPath = t;
            else
            {
                _element.FileName = t.Contains('.') ? t : t + ".jpg";
                _element.AltPath = null;
            }
        }
        ElementChanged?.Invoke();
    }
}
