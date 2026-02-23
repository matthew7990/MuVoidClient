namespace OzViewer;

public class HudPropertiesPanel : UserControl
{
    private HudElement? _element;
    private TextBox? _txtX, _txtY, _txtW, _txtH, _txtFile, _txtLabel, _txtTextValue, _txtFontSize, _txtColor;
    private ListBox? _listElements;
    private CheckBox? _chkVisible, _chkIsText, _chkFontBold;
    private ComboBox? _cmbFontName;
    private bool _updatingFromElement;

    public HudDocument? Document { get; set; }
    public string CurrentCategory { get; set; } = "HUD";
    public event Action? ElementChanged;
    public event Action? ElementSelectedInList;

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
        MinimumSize = new Size(220, 0);
        AutoScroll = true;

        var lblTitle = new Label { Text = "Propiedades", Font = new Font(Font.FontFamily, 10, FontStyle.Bold), AutoSize = true, Location = new Point(8, 8) };
        Controls.Add(lblTitle);

        _listElements = new ListBox
        {
            Location = new Point(8, 30),
            Size = new Size(185, 100),
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.FromArgb(35, 35, 45),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _listElements.SelectedIndexChanged += (_, _) =>
        {
            if (_updatingFromElement) return;
            if (Document != null && _listElements.SelectedIndex >= 0)
            {
                var filtered = Document.Elements.Where(e => e.Category == CurrentCategory).ToList();
                if (_listElements.SelectedIndex < filtered.Count)
                    ElementSelected?.Invoke(filtered[_listElements.SelectedIndex]);
            }
        };
        Controls.Add(_listElements);

        int y = 140;

        void AddRow(string label, out TextBox txt, int width = 120)
        {
            var lbl = new Label { Text = label, Location = new Point(8, y), AutoSize = true };
            txt = new TextBox
            {
                Location = new Point(80, y - 2),
                Width = width,
                BackColor = Color.FromArgb(35, 35, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            txt.TextChanged += (_, _) => { if (!_updatingFromElement) ApplyToElement(); };
            Controls.Add(lbl);
            Controls.Add(txt);
            y += 28;
        }

        AddRow("Etiqueta:", out _txtLabel!);
        AddRow("X:", out _txtX!, 60);
        AddRow("Y:", out _txtY!, 60);
        AddRow("Ancho:", out _txtW!, 60);
        AddRow("Alto:", out _txtH!, 60);

        _chkVisible = new CheckBox { Text = "Visible", Location = new Point(8, y), AutoSize = true, ForeColor = Color.White };
        _chkVisible.CheckedChanged += (_, _) => { if (!_updatingFromElement) ApplyVisibleToElement(); };
        Controls.Add(_chkVisible);
        y += 26;

        _chkIsText = new CheckBox { Text = "Es Texto", Location = new Point(8, y), AutoSize = true, ForeColor = Color.Cyan };
        _chkIsText.CheckedChanged += (_, _) => { if (!_updatingFromElement) { if (_element != null) _element.IsText = _chkIsText.Checked; RefreshFromElement(); ApplyToElement(); } };
        Controls.Add(_chkIsText);
        y += 30;

        // --- Campos de Imagen ---
        var lblFile = new Label { Text = "Imagen:", Location = new Point(8, y), AutoSize = true };
        _txtFile = new TextBox { Location = new Point(8, y + 18), Width = 185, BackColor = Color.FromArgb(35, 35, 45), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        _txtFile.TextChanged += (_, _) => { if (!_updatingFromElement) ApplyToElement(); };
        Controls.Add(lblFile);
        Controls.Add(_txtFile);
        y += 45;

        // --- Campos de Texto ---
        AddRow("Valor:", out _txtTextValue!);
        AddRow("Fuente:", out TextBox _, 0); y -= 28; // Dummy to use AddRow logic
        _cmbFontName = new ComboBox { Location = new Point(80, y - 2), Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(35, 35, 45), ForeColor = Color.White };
        _cmbFontName.Items.AddRange(new[] { "Tahoma", "Arial", "Verdana", "Consolas", "Impact" });
        _cmbFontName.SelectedIndexChanged += (_, _) => { if (!_updatingFromElement) ApplyToElement(); };
        Controls.Add(_cmbFontName);
        y += 28;

        AddRow("Tamaño:", out _txtFontSize!, 40);
        AddRow("Color Hex:", out _txtColor!, 80);

        _chkFontBold = new CheckBox { Text = "Negrita", Location = new Point(8, y), AutoSize = true, ForeColor = Color.White };
        _chkFontBold.CheckedChanged += (_, _) => { if (!_updatingFromElement) ApplyToElement(); };
        Controls.Add(_chkFontBold);
        y += 30;

        var btnAddText = new Button { Text = "Añadir Texto", Location = new Point(8, y), Size = new Size(90, 24), BackColor = Color.FromArgb(50, 80, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnAddText.Click += (_, _) => AddNewElement(true);
        Controls.Add(btnAddText);

        var btnAddImg = new Button { Text = "Añadir Imagen", Location = new Point(103, y), Size = new Size(95, 24), BackColor = Color.FromArgb(50, 50, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnAddImg.Click += (_, _) => AddNewElement(false);
        Controls.Add(btnAddImg);
        y += 32;

        var btnDelete = new Button { Text = "Eliminar seleccionado", Location = new Point(8, y), Size = new Size(190, 24), BackColor = Color.FromArgb(80, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnDelete.Click += (_, _) => DeleteSelected();
        Controls.Add(btnDelete);
        y += 40;

        ResumeLayout(false);
    }

    private void AddNewElement(bool isText)
    {
        if (Document == null) return;
        var el = isText ? HudElement.CreateText("Nuevo Texto", "Text", 50, 50, CurrentCategory) : new HudElement("Nueva Imagen", "empty.jpg", 50, 50, 32, 32, null, CurrentCategory);
        Document.Elements.Add(el);
        RefreshElementList();
        ElementSelected?.Invoke(el);
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
        foreach (var el in Document.Elements.Where(e => e.Category == CurrentCategory))
        {
            var name = string.IsNullOrWhiteSpace(el.Label) ? (el.IsText ? "Texto" : Path.GetFileName(el.FileName)) : el.Label;
            _listElements!.Items.Add($"{name} ({el.X:F0},{el.Y:F0})");
        }
    }

    public void RefreshFromElement()
    {
        _updatingFromElement = true;
        try
        {
            var hasEl = _element != null;
            _txtX!.Enabled = _txtY!.Enabled = _txtW!.Enabled = _txtH!.Enabled = _txtLabel!.Enabled = _chkVisible!.Enabled = _chkIsText!.Enabled = hasEl;

            if (_element == null)
            {
                _txtX.Text = _txtY.Text = _txtW.Text = _txtH.Text = _txtFile!.Text = _txtLabel.Text = _txtTextValue!.Text = _txtFontSize!.Text = _txtColor!.Text = "";
                return;
            }

            _txtLabel.Text = _element.Label;
            _txtX.Text = _element.X.ToString("F0");
            _txtY.Text = _element.Y.ToString("F0");
            _txtW.Text = _element.Width.ToString("F0");
            _txtH.Text = _element.Height.ToString("F0");
            _chkVisible.Checked = _element.Visible;
            _chkIsText.Checked = _element.IsText;

            // Visibility based on type
            _txtFile!.Enabled = !_element.IsText;
            _txtFile.Text = _element.FileName;
            
            _txtTextValue!.Enabled = _element.IsText;
            _txtTextValue.Text = _element.TextValue;
            _cmbFontName!.Enabled = _element.IsText;
            _cmbFontName.SelectedItem = _cmbFontName.Items.Contains(_element.FontName) ? _element.FontName : "Tahoma";
            _txtFontSize!.Enabled = _element.IsText;
            _txtFontSize.Text = _element.FontSize.ToString("F1");
            _txtColor!.Enabled = _element.IsText;
            _txtColor.Text = _element.TextColorHex;
            _chkFontBold!.Enabled = _element.IsText;
            _chkFontBold.Checked = _element.FontBold;

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
        Document.Elements.Remove(_element);
        SelectedElement = null;
        RefreshElementList();
        ElementChanged?.Invoke();
    }

    private void ApplyToElement()
    {
        if (_element == null) return;
        _element.Label = _txtLabel?.Text ?? "";
        if (float.TryParse(_txtX?.Text, out var x)) _element.X = x;
        if (float.TryParse(_txtY?.Text, out var y)) _element.Y = y;
        if (float.TryParse(_txtW?.Text, out var w)) _element.Width = w;
        if (float.TryParse(_txtH?.Text, out var h)) _element.Height = h;
        
        if (!_element.IsText)
            _element.FileName = _txtFile?.Text ?? "";
        else
        {
            _element.TextValue = _txtTextValue?.Text ?? "";
            _element.FontName = _cmbFontName?.SelectedItem?.ToString() ?? "Tahoma";
            if (float.TryParse(_txtFontSize?.Text, out var fs)) _element.FontSize = fs;
            _element.TextColorHex = _txtColor?.Text ?? "#FFFFFF";
            _element.FontBold = _chkFontBold?.Checked ?? false;
        }

        ElementChanged?.Invoke();
    }
}
