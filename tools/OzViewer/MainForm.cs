namespace OzViewer;

public partial class MainForm : Form
{
    private string? _currentFolder = null;
    private string? _layoutPath = null;
    private Image? _currentImage = null;
    private string? _currentImagePath = null;
    private HudDocument _sharedDoc = null!;
    private HudEditorPanel _hudEditor = null!;
    private HudPropertiesPanel _propertiesPanel = null!;
    private TabControl _tabControl = null!;
    private TabControl _subTabControl = null!;
    private ListBox _listBox = null!;
    private PictureBox _pictureBox = null!;
    private Label _statusLabel = null!;
    private ToolStripStatusLabel _editorStatusLabel = null!;
    private SplitContainer _filesSplit = null!;
    private Dictionary<TabPage, HudEditorPanel> _panelsByTab = new();

    public MainForm()
    {
        Log.Write("MainForm ctor inicio");
        InitializeComponent();
        Log.Write("MainForm ctor fin");
        AllowDrop = true;
        KeyPreview = true;
        KeyDown += MainForm_KeyDown;
        DragEnter += MainForm_DragEnter;
        DragDrop += MainForm_DragDrop;
    }

    private void InitializeComponent()
    {
        Log.Write("InitializeComponent inicio");
        SuspendLayout();

        Text = "MuVoid - HUD Editor";
        Size = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 500);

        var menuStrip = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("Archivo");
        fileMenu.DropDownItems.Add("Abrir carpeta Interface...", null, (_, _) => OpenFolder());
        fileMenu.DropDownItems.Add("Abrir archivo...", null, (_, _) => OpenFile());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Cargar layout...", null, (_, _) => LoadLayout());
        fileMenu.DropDownItems.Add("Guardar layout...", null, (_, _) => SaveLayout());
        fileMenu.DropDownItems.Add("Restablecer layout por defecto", null, (_, _) => ResetLayout());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Exportar posiciones a C++ header...", null, (_, _) => ExportCppHeader());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Guardar imagen como OZJ...", null, (_, _) => SaveAsOzj());
        fileMenu.DropDownItems.Add("Guardar imagen como OZT...", null, (_, _) => SaveAsOzt());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Salir", null, (_, _) => Close());
        menuStrip.Items.Add(fileMenu);

        var viewMenu = new ToolStripMenuItem("Vista");
        viewMenu.DropDownItems.Add("Editor HUD", null, (_, _) => _tabControl.SelectedIndex = 0);
        viewMenu.DropDownItems.Add("Archivos", null, (_, _) => _tabControl.SelectedIndex = 1);
        menuStrip.Items.Add(viewMenu);

        MainMenuStrip = menuStrip;

        _tabControl = new TabControl { Dock = DockStyle.Fill };
        _tabControl.TabPages.Add("Editor HUD");
        _tabControl.TabPages.Add("Archivos");

        // --- Pestaña Archivos ---
        _filesSplit = new SplitContainer { Dock = DockStyle.Fill };
        _listBox = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) };
        _listBox.SelectedIndexChanged += ListBox_SelectedIndexChanged;
        _listBox.DoubleClick += (_, _) => LoadSelected();
        _filesSplit.Panel1.Controls.Add(_listBox);

        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(40, 40, 40),
            BorderStyle = BorderStyle.FixedSingle
        };
        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom, Height = 24, AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0)
        };
        _filesSplit.Panel2.Controls.Add(_pictureBox);
        _filesSplit.Panel2.Controls.Add(_statusLabel);
        _filesSplit.Panel2.Controls.SetChildIndex(_statusLabel, 0);
        _tabControl.TabPages[1].Controls.Add(_filesSplit);

        // --- Pestaña Editor ---
        var editorSplit = new SplitContainer { Dock = DockStyle.Fill };
        _subTabControl = new TabControl { Dock = DockStyle.Fill };
        _subTabControl.SelectedIndexChanged += SubTabControl_SelectedIndexChanged;

        _propertiesPanel = new HudPropertiesPanel { Dock = DockStyle.Fill };
        _sharedDoc = HudDocument.CreateDefault();
        _propertiesPanel.Document = _sharedDoc;

        RebuildTabs();

        _propertiesPanel.ElementSelected += (el) =>
        {
            _hudEditor.SelectedElement = el;
            _propertiesPanel.SelectedElement = el;
        };
        _propertiesPanel.ElementChanged += () => { _hudEditor.Invalidate(); };

        editorSplit.Panel1.Controls.Add(_subTabControl);
        editorSplit.Panel2.Controls.Add(_propertiesPanel);
        _tabControl.TabPages[0].Controls.Add(editorSplit);

        _editorStatusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        var statusStrip = new StatusStrip { Items = { _editorStatusLabel } };
        statusStrip.SizingGrip = false;

        Controls.Add(_tabControl);
        Controls.Add(statusStrip);
        Controls.Add(menuStrip);

        Shown += (_, _) =>
        {
            try
            {
                ApplyLayout();
                _propertiesPanel.RefreshElementList();
                var ifacePath = Paths.DefaultInterfaceFolder;
                if (Directory.Exists(ifacePath)) LoadFolder(ifacePath, showHudTab: true);
                else
                {
                    _hudEditor.BaseFolder = null;
                    _statusLabel.Text = $"Interface no encontrada: {ifacePath}";
                }
            }
            catch (Exception ex) { Log.Error("Shown", ex); }
        };

        ResumeLayout(false);
    }

    private void ApplyLayout()
    {
        try
        {
            if (_filesSplit.Width > 0 && _filesSplit.Height > 0)
            {
                _filesSplit.Panel1MinSize = 120;
                _filesSplit.Panel2MinSize = 150;
                var dim = _filesSplit.Orientation == Orientation.Horizontal ? _filesSplit.Height : _filesSplit.Width;
                if (dim > 150) _filesSplit.SplitterDistance = Math.Clamp(220, 120, dim - 150);
            }
            if (_tabControl.TabPages[0].Controls[0] is SplitContainer es && es.Width > 200)
            {
                es.Panel2MinSize = 180;
                es.Panel2.Width = 200;
            }
        }
        catch (Exception ex) { Log.Error("Layout", ex); }
    }

    private void RebuildTabs()
    {
        _subTabControl.SelectedIndexChanged -= SubTabControl_SelectedIndexChanged;
        _subTabControl.TabPages.Clear();
        _panelsByTab.Clear();

        var categories = _sharedDoc.Elements.Select(e => e.Category).Distinct().OrderBy(c => c == "HUD" ? 0 : 1).ThenBy(c => c).ToList();
        if (!categories.Contains("HUD")) categories.Insert(0, "HUD");

        foreach (var cat in categories)
        {
            var page = new TabPage(cat);
            var panel = new HudEditorPanel 
            { 
                Dock = DockStyle.Fill, 
                BackColor = Color.FromArgb(25, 25, 35),
                CurrentCategory = cat,
                Document = _sharedDoc,
                BaseFolder = _hudEditor?.BaseFolder
            };
            
            panel.SelectionChanged += () => { if (_hudEditor == panel) _propertiesPanel.SelectedElement = panel.SelectedElement; };
            panel.DragMoved += (el) => { if (_hudEditor == panel) _propertiesPanel.UpdatePositionOnly(el); };
            panel.HoveredElementChanged += (el) => { if (_hudEditor == panel) _editorStatusLabel.Text = el != null ? $"Sobre: {el.ResolvePath} ({el.X:F0},{el.Y:F0})" : ""; };

            page.Controls.Add(panel);
            _subTabControl.TabPages.Add(page);
            _panelsByTab[page] = panel;
        }

        _hudEditor = _panelsByTab[_subTabControl.TabPages[0]];
        _propertiesPanel.CurrentCategory = _hudEditor.CurrentCategory;
        _subTabControl.SelectedIndexChanged += SubTabControl_SelectedIndexChanged;
    }

    private void SubTabControl_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_subTabControl.SelectedTab == null) return;
        _hudEditor = _panelsByTab[_subTabControl.SelectedTab];
        _propertiesPanel.CurrentCategory = _hudEditor.CurrentCategory;
        _propertiesPanel.SelectedElement = null;
        _propertiesPanel.RefreshElementList();
        _hudEditor.Invalidate();
    }

    private void OpenFolder()
    {
        var defaultPath = Paths.DefaultInterfaceFolder;
        using var dlg = new FolderBrowserDialog
        {
            Description = "Seleccionar carpeta Interface",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(defaultPath) ? defaultPath : ""
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            LoadFolder(dlg.SelectedPath, showHudTab: true);
        }
    }

    private void OpenFile()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Imágenes (*.ozj;*.ozt;*.jpg)|*.ozj;*.ozt;*.jpg|Todos (*.*)|*.*",
            Title = "Abrir archivo"
        };
        if (dlg.ShowDialog() == DialogResult.OK) LoadImage(dlg.FileName);
    }

    public void LoadFolder(string path, bool showHudTab = false)
    {
        _currentFolder = path;
        _listBox.Items.Clear();
        var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(f => new[] { ".ozj", ".ozt", ".jpg", ".jpeg", ".tga" }.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(f => f[(path.Length + 1)..].Replace('\\', '/'))
            .OrderBy(Path.GetFileName)
            .ToArray();

        foreach (var f in files) _listBox.Items.Add(f);
        if (_listBox.Items.Count > 0) _listBox.SelectedIndex = 0;

        _statusLabel.Text = $"{_listBox.Items.Count} archivos en {path}";
        _hudEditor.BaseFolder = path;
        foreach (var pnl in _panelsByTab.Values) pnl.BaseFolder = path;

        var layoutFile = Path.Combine(path, "hud_layout.json");
        if (File.Exists(layoutFile))
        {
            try
            {
                var doc = HudDocument.Load(layoutFile);
                UpdateAllTabDocuments(doc);
                _layoutPath = layoutFile;
                _statusLabel.Text += " | Layout cargado";
            }
            catch { }
        }
        if (showHudTab) _tabControl.SelectedIndex = 0;
    }

    private void LoadLayout()
    {
        var defaultPath = Path.Combine(Paths.DefaultInterfaceFolder, "hud_layout.json");
        using var dlg = new OpenFileDialog
        {
            Filter = "Layout HUD (*.json)|*.json|Todos (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(defaultPath) ?? "",
            Title = "Cargar layout"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var doc = HudDocument.Load(dlg.FileName);
                UpdateAllTabDocuments(doc);
                _layoutPath = dlg.FileName;
                _statusLabel.Text = $"Layout cargado: {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "HUD Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    private void SaveLayout()
    {
        var defaultPath = Path.Combine(Paths.DefaultInterfaceFolder, "hud_layout.json");
        using var dlg = new SaveFileDialog
        {
            Filter = "Layout HUD (*.json)|*.json|Todos (*.*)|*.*",
            FileName = _layoutPath ?? defaultPath,
            InitialDirectory = Path.GetDirectoryName(defaultPath) ?? "",
            Title = "Guardar layout"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                _sharedDoc.Save(dlg.FileName);
                _layoutPath = dlg.FileName;
                _statusLabel.Text = $"Layout guardado: {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "HUD Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    private void ResetLayout()
    {
        UpdateAllTabDocuments(HudDocument.CreateDefault());
        _statusLabel.Text = "Layout restablecido";
    }

    private void UpdateAllTabDocuments(HudDocument doc)
    {
        _sharedDoc = doc;
        _propertiesPanel.Document = doc;
        RebuildTabs();
        _hudEditor.Invalidate();
    }

    private void ExportCppHeader()
    {
        var defaultPath = Path.Combine(Paths.WorkspaceRoot, "MuMain", "src", "source", "HudLayout.h");
        using var dlg = new SaveFileDialog
        {
            Filter = "Header C++ (*.h)|*.h|Todos (*.*)|*.*",
            FileName = "HudLayout.h",
            InitialDirectory = Path.GetDirectoryName(defaultPath) ?? "",
            Title = "Exportar posiciones a C++"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var content = HudCppExporter.ExportHeader(_sharedDoc);
                File.WriteAllText(dlg.FileName, content);
                _statusLabel.Text = $"Exportado: {Path.GetFileName(dlg.FileName)}";
                MessageBox.Show("Header generado.", "HUD Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "HUD Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    private void ListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_listBox.SelectedIndex < 0 || string.IsNullOrEmpty(_currentFolder)) return;
        var name = _listBox.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(name)) return;
        LoadImage(Path.Combine(_currentFolder, name.Replace('/', Path.DirectorySeparatorChar)));
    }

    private void LoadSelected()
    {
        if (_listBox.SelectedIndex >= 0 && !string.IsNullOrEmpty(_currentFolder))
        {
            var name = _listBox.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(name)) LoadImage(Path.Combine(_currentFolder, name.Replace('/', Path.DirectorySeparatorChar)));
        }
    }

    public void LoadImage(string path)
    {
        try
        {
            _currentImage?.Dispose();
            _currentImage = OzImageLoader.Load(path);
            _currentImagePath = path;
            if (_currentImage != null)
            {
                var old = _pictureBox.Image;
                _pictureBox.Image = _currentImage;
                old?.Dispose();
                _statusLabel.Text = $"{Path.GetFileName(path)} — {_currentImage.Width}×{_currentImage.Height}";
            }
            else
            {
                _pictureBox.Image?.Dispose();
                _pictureBox.Image = null;
                _statusLabel.Text = $"No se pudo cargar: {Path.GetFileName(path)}";
            }
        }
        catch (Exception ex) { _statusLabel.Text = $"Error: {ex.Message}"; }
    }

    private void SaveAsOzj()
    {
        if (_currentImage == null) return;
        using var dlg = new SaveFileDialog { Filter = "OZJ (*.ozj)|*.ozj", FileName = _currentImagePath != null ? Path.GetFileNameWithoutExtension(_currentImagePath) + ".ozj" : "image.ozj" };
        if (dlg.ShowDialog() == DialogResult.OK && OzImageLoader.SaveOzj(_currentImage, dlg.FileName))
        {
            _statusLabel.Text = $"Guardado: {Path.GetFileName(dlg.FileName)}";
            _hudEditor.ClearCache();
            _hudEditor.Invalidate();
        }
    }

    private void SaveAsOzt()
    {
        if (_currentImage == null) return;
        using var dlg = new SaveFileDialog { Filter = "OZT (*.ozt)|*.ozt", FileName = _currentImagePath != null ? Path.GetFileNameWithoutExtension(_currentImagePath) + ".ozt" : "image.ozt" };
        if (dlg.ShowDialog() == DialogResult.OK && OzImageLoader.SaveOzt(_currentImage, dlg.FileName))
        {
            _statusLabel.Text = $"Guardado: {Path.GetFileName(dlg.FileName)}";
            _hudEditor.ClearCache();
            _hudEditor.Invalidate();
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete && _tabControl.SelectedIndex == 0 && _hudEditor.SelectedElement != null)
        {
            _sharedDoc.Elements.Remove(_hudEditor.SelectedElement);
            _hudEditor.SelectedElement = null;
            _propertiesPanel.SelectedElement = null;
            _propertiesPanel.RefreshElementList();
            _hudEditor.Invalidate();
            e.Handled = true;
        }
    }

    private void MainForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) e.Effect = DragDropEffects.Copy;
    }

    private void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        var path = files[0];
        if (Directory.Exists(path)) LoadFolder(path, true);
        else if (File.Exists(path)) LoadImage(path);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _currentImage?.Dispose();
        _hudEditor.ClearCache();
        base.OnFormClosing(e);
    }
}
