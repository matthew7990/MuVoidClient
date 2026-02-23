namespace OzViewer;

public partial class MainForm : Form
{
    private string? _currentFolder = null;
    private string? _layoutPath = null;
    private Image? _currentImage = null;
    private string? _currentImagePath = null;

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
        _tabControl.TabPages.Add("Editor HUD");  // Primero: lo que más importa
        _tabControl.TabPages.Add("Archivos");

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

        _tabControl.TabPages[1].Controls.Add(_filesSplit);  // Archivos en pestaña 1

        var editorSplit = new SplitContainer { Dock = DockStyle.Fill };
        
        // --- Nueva sub-pestañas para categorías ---
        var subTabControl = new TabControl { Dock = DockStyle.Fill };
        var categories = new[] { "HUD", "Inventario", "Personaje", "Amigos", "Carga" };
        var panelsByTab = new Dictionary<TabPage, HudEditorPanel>();

        foreach (var cat in categories)
        {
            var page = new TabPage(cat);
            var panel = new HudEditorPanel 
            { 
                Dock = DockStyle.Fill, 
                BackColor = Color.FromArgb(25, 25, 35),
                CurrentCategory = cat
            };
            page.Controls.Add(panel);
            subTabControl.TabPages.Add(page);
            panelsByTab[page] = panel;
        }

        _hudEditor = panelsByTab[subTabControl.TabPages[0]]; // Default
        subTabControl.SelectedIndexChanged += (_, _) =>
        {
            var oldDoc = _hudEditor.Document;
            var oldFolder = _hudEditor.BaseFolder;
            
            _hudEditor = panelsByTab[subTabControl.SelectedTab!];
            _hudEditor.Document = oldDoc;
            _hudEditor.BaseFolder = oldFolder;
            
            _propertiesPanel.CurrentCategory = _hudEditor.CurrentCategory;
            _propertiesPanel.SelectedElement = null;
            _propertiesPanel.RefreshElementList();
            _hudEditor.Invalidate();
        };
        // ------------------------------------------

        _propertiesPanel = new HudPropertiesPanel { Dock = DockStyle.Fill };

        // Documento compartido inicial
        var sharedDoc = HudDocument.CreateDefault();

        // Configuración inicial compartida
        foreach (var pnl in panelsByTab.Values)
        {
            pnl.Document = sharedDoc;
            pnl.SelectionChanged += () => {
                if (_hudEditor == pnl) _propertiesPanel.SelectedElement = pnl.SelectedElement;
            };
            pnl.DragMoved += (el) => {
                if (_hudEditor == pnl) _propertiesPanel.UpdatePositionOnly(el);
            };
            pnl.HoveredElementChanged += (el) => {
                if (_hudEditor == pnl) _editorStatusLabel.Text = el != null ? $"Sobre: {el.ResolvePath} ({el.X:F0},{el.Y:F0})" : "";
            };
        }

        _propertiesPanel.Document = sharedDoc;
        _propertiesPanel.CurrentCategory = _hudEditor.CurrentCategory;
        _propertiesPanel.ElementSelected += (el) =>
        {
            _hudEditor.SelectedElement = el;
            _propertiesPanel.SelectedElement = el;
        };
        _propertiesPanel.ElementChanged += () =>
        {
            _hudEditor.Invalidate();
        };

        editorSplit.Panel1.Controls.Add(subTabControl);
        editorSplit.Panel2.Controls.Add(_propertiesPanel);

        _tabControl.TabPages[0].Controls.Add(editorSplit);  // Editor HUD en pestaña 0

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
                // Aplicar MinSize/SplitterDistance después del layout (evita error cuando aún no hay dimensiones)
                void ApplyLayout()
                {
                    try
                    {
                        if (_filesSplit.Width > 0 && _filesSplit.Height > 0)
                        {
                            _filesSplit.Panel1MinSize = 120;
                            _filesSplit.Panel2MinSize = 150;
                            var dim = _filesSplit.Orientation == Orientation.Horizontal ? _filesSplit.Height : _filesSplit.Width;
                            if (dim > 150)
                                _filesSplit.SplitterDistance = Math.Clamp(220, 120, dim - 150);
                        }
                        if (_tabControl.TabPages[0].Controls[0] is SplitContainer es && es.Width > 200)
                        {
                            es.Panel2MinSize = 180;
                            es.Panel2.Width = 200;
                        }
                    }
                    catch (Exception ex2) { Log.Error("Layout", ex2); }
                }
                ApplyLayout();
                BeginInvoke(ApplyLayout);
                _propertiesPanel.RefreshElementList();
                // Cargar Interface automáticamente si existe (Source\src\bin\Data\Interface)
                var ifacePath = Paths.DefaultInterfaceFolder;
                if (Directory.Exists(ifacePath))
                {
                    LoadFolder(ifacePath, showHudTab: true);
                }
                else
                {
                    _hudEditor.BaseFolder = null;
                    _statusLabel.Text = $"Interface no encontrada. Esperada: {ifacePath}";
                }
                Log.Write("Shown OK");
            }
            catch (Exception ex) { Log.Error("Shown", ex); }
        };

        Log.Write("ResumeLayout");
        ResumeLayout(false);
    }

    private TabControl _tabControl = null!;
    private SplitContainer _filesSplit = null!;
    private ListBox _listBox = null!;
    private PictureBox _pictureBox = null!;
    private Label _statusLabel = null!;
    private HudEditorPanel _hudEditor = null!;
    private HudPropertiesPanel _propertiesPanel = null!;
    private ToolStripStatusLabel _editorStatusLabel = null!;

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
            _hudEditor.BaseFolder = dlg.SelectedPath;
        }
    }

    private void OpenFile()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Imágenes (*.ozj;*.ozt;*.jpg)|*.ozj;*.ozt;*.jpg|Todos (*.*)|*.*",
            Title = "Abrir archivo"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            LoadImage(dlg.FileName);
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

        foreach (var f in files)
            _listBox.Items.Add(f);

        if (_listBox.Items.Count > 0)
            _listBox.SelectedIndex = 0;

        _statusLabel.Text = $"{_listBox.Items.Count} archivos en {path}";
        _hudEditor.BaseFolder = path;
        _hudEditor.Invalidate();
        _propertiesPanel.RefreshElementList();

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
            catch { /* ignorar si falla */ }
        }

        if (showHudTab)
            _tabControl.SelectedIndex = 0;  // Editor HUD
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "HUD Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                _hudEditor.Document.Save(dlg.FileName);
                _layoutPath = dlg.FileName;
                _statusLabel.Text = $"Layout guardado: {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "HUD Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ResetLayout()
    {
        UpdateAllTabDocuments(HudDocument.CreateDefault());
        _statusLabel.Text = "Layout restablecido";
    }

    private void UpdateAllTabDocuments(HudDocument doc)
    {
        _hudEditor.Document = doc;
        _propertiesPanel.Document = doc;
        
        // El subTabControl es el Control 0 del Panel 1 del editorSplit
        // Buscamos el subTabControl para actualizar todos sus paneles
        if (_tabControl.TabPages[0].Controls[0] is SplitContainer es && es.Panel1.Controls[0] is TabControl stc)
        {
            foreach (TabPage page in stc.TabPages)
            {
                if (page.Controls[0] is HudEditorPanel pnl)
                {
                    pnl.Document = doc;
                    pnl.Invalidate();
                }
            }
        }

        _propertiesPanel.RefreshElementList();
        _hudEditor.Invalidate();
    }

    private void ExportCppHeader()
    {
        var defaultPath = Path.Combine(Paths.WorkspaceRoot, "Source", "src", "source", "HudLayout.h");
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
                var content = HudCppExporter.ExportHeader(_hudEditor.Document);
                File.WriteAllText(dlg.FileName, content);
                _statusLabel.Text = $"Exportado: {Path.GetFileName(dlg.FileName)}";
                MessageBox.Show($"Header generado.\n\nIncluye en el código:\n#include \"HudLayout.h\"\n\nUsa las constantes:\nHudLayout::NEWUI_MENU01_X, HudLayout::NEWUI_MENU01_Y, etc.", "HUD Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "HUD Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            if (!string.IsNullOrEmpty(name))
                LoadImage(Path.Combine(_currentFolder, name.Replace('/', Path.DirectorySeparatorChar)));
        }
    }

    public void LoadImage(string path)
    {
        try
        {
            _currentImage?.Dispose();
            _currentImage = null;
            _currentImagePath = path;

            var img = OzImageLoader.Load(path);
            if (img != null)
            {
                _currentImage = img;
                var old = _pictureBox.Image;
                _pictureBox.Image = img;
                old?.Dispose();
                _statusLabel.Text = $"{Path.GetFileName(path)} — {img.Width}×{img.Height}";
            }
            else
            {
                _pictureBox.Image?.Dispose();
                _pictureBox.Image = null;
                _statusLabel.Text = $"No se pudo cargar: {Path.GetFileName(path)}";
            }
        }
        catch (Exception ex)
        {
            _pictureBox.Image?.Dispose();
            _pictureBox.Image = null;
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private void SaveAsOzj()
    {
        if (_currentImage == null)
        {
            MessageBox.Show("No hay imagen cargada.", "HUD Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Filter = "OZJ (*.ozj)|*.ozj|Todos (*.*)|*.*",
            FileName = _currentImagePath != null ? Path.GetFileNameWithoutExtension(_currentImagePath) + ".ozj" : "image.ozj",
            Title = "Guardar como OZJ"
        };
        if (dlg.ShowDialog() == DialogResult.OK && OzImageLoader.SaveOzj(_currentImage, dlg.FileName))
        {
            _statusLabel.Text = $"Guardado: {Path.GetFileName(dlg.FileName)}";
            _hudEditor.ClearCache();
            _hudEditor.Invalidate();
        }
    }

    private void SaveAsOzt()
    {
        if (_currentImage == null)
        {
            MessageBox.Show("No hay imagen cargada.", "HUD Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Filter = "OZT (*.ozt)|*.ozt|Todos (*.*)|*.*",
            FileName = _currentImagePath != null ? Path.GetFileNameWithoutExtension(_currentImagePath) + ".ozt" : "image.ozt",
            Title = "Guardar como OZT"
        };
        if (dlg.ShowDialog() == DialogResult.OK && OzImageLoader.SaveOzt(_currentImage, dlg.FileName))
        {
            _statusLabel.Text = $"Guardado: {Path.GetFileName(dlg.FileName)}";
            _hudEditor.ClearCache();
            _hudEditor.Invalidate();
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Delete) return;
        if (_tabControl.SelectedIndex != 0) return;
        var el = _hudEditor.SelectedElement;
        if (el == null || _propertiesPanel.Document == null) return;
        var idx = _propertiesPanel.Document.Elements.IndexOf(el);
        if (idx < 0) return;
        _propertiesPanel.Document.Elements.RemoveAt(idx);
        _hudEditor.SelectedElement = _propertiesPanel.Document.Elements.Count > 0 ? _propertiesPanel.Document.Elements[Math.Min(idx, _propertiesPanel.Document.Elements.Count - 1)] : null;
        _propertiesPanel.SelectedElement = _hudEditor.SelectedElement;
        _propertiesPanel.RefreshElementList();
        _hudEditor.Invalidate();
        e.Handled = true;
    }

    private void MainForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            e.Effect = DragDropEffects.Copy;
    }

    private void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        var path = files[0];
        if (Directory.Exists(path))
        {
            LoadFolder(path);
            _hudEditor.BaseFolder = path;
            _tabControl.SelectedIndex = 0;  // Editor HUD
        }
        else if (File.Exists(path))
            LoadImage(path);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _currentImage?.Dispose();
        _hudEditor.ClearCache();
        base.OnFormClosing(e);
    }
}
