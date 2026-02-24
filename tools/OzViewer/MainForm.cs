namespace OzViewer;

public partial class MainForm : Form
{
    private string? _currentFolder = null;
    private string? _layoutPath    = null;
    private Image?  _currentImage  = null;
    private string? _currentImagePath = null;

    // ── Toolbar controls (refs para sincronizar con el panel activo) ──
    private ToolStripButton _btnGrid   = null!;
    private ToolStripButton _btnSnap   = null!;
    private ToolStripButton _btnPreview = null!;
    private ToolStripComboBox _cbxGridStep   = null!;
    private ToolStripComboBox _cbxResolution = null!;
    private ToolStripLabel    _lblZoom       = null!;

    public MainForm()
    {
        Log.Write("MainForm ctor inicio");
        InitializeComponent();
        Log.Write("MainForm ctor fin");
        AllowDrop  = true;
        KeyPreview = true;
        KeyDown   += MainForm_KeyDown;
        DragEnter += MainForm_DragEnter;
        DragDrop  += MainForm_DragDrop;
    }

    private void InitializeComponent()
    {
        Log.Write("InitializeComponent inicio");
        SuspendLayout();

        Text            = "MuVoid — HUD Editor";
        Size            = new Size(1280, 860);
        StartPosition   = FormStartPosition.CenterScreen;
        MinimumSize     = new Size(700, 500);
        BackColor       = Color.FromArgb(28, 28, 38);

        // ── Menú ──────────────────────────────────────────────────────
        var menuStrip = new MenuStrip();
        menuStrip.BackColor = Color.FromArgb(35, 35, 48);
        menuStrip.ForeColor = Color.White;
        menuStrip.Renderer  = new DarkMenuRenderer();

        var fileMenu = new ToolStripMenuItem("Archivo");
        fileMenu.DropDownItems.Add("Abrir carpeta Interface...",  null, (_, _) => OpenFolder());
        fileMenu.DropDownItems.Add("Abrir archivo...",            null, (_, _) => OpenFile());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Cargar layout...",            null, (_, _) => LoadLayout());
        fileMenu.DropDownItems.Add("Guardar layout",              null, (_, _) => SaveLayoutQuick());
        fileMenu.DropDownItems.Add("Guardar layout como...",      null, (_, _) => SaveLayout());
        fileMenu.DropDownItems.Add("Restablecer layout por defecto", null, (_, _) => ResetLayout());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Exportar posiciones a C++ header...", null, (_, _) => ExportCppHeader());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Guardar imagen como OZJ...",  null, (_, _) => SaveAsOzj());
        fileMenu.DropDownItems.Add("Guardar imagen como OZT...",  null, (_, _) => SaveAsOzt());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Salir", null, (_, _) => Close());
        menuStrip.Items.Add(fileMenu);

        var viewMenu = new ToolStripMenuItem("Vista");
        viewMenu.DropDownItems.Add("Editor HUD", null, (_, _) => _tabControl.SelectedIndex = 0);
        viewMenu.DropDownItems.Add("Archivos",   null, (_, _) => _tabControl.SelectedIndex = 1);
        menuStrip.Items.Add(viewMenu);

        MainMenuStrip = menuStrip;

        // ── Toolbar ───────────────────────────────────────────────────
        var toolStrip = BuildToolStrip();

        // ── Tabs ──────────────────────────────────────────────────────
        _tabControl = new TabControl { Dock = DockStyle.Fill };
        _tabControl.TabPages.Add("Editor HUD");
        _tabControl.TabPages.Add("Archivos");

        // ── Pestaña Archivos ──────────────────────────────────────────
        _filesSplit = new SplitContainer { Dock = DockStyle.Fill };
        _listBox = new ListBox
        {
            Dock      = DockStyle.Fill,
            Font      = new Font("Consolas", 9f),
            BackColor = Color.FromArgb(32, 32, 44),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        _listBox.SelectedIndexChanged += ListBox_SelectedIndexChanged;
        _listBox.DoubleClick          += (_, _) => LoadSelected();
        _filesSplit.Panel1.Controls.Add(_listBox);

        _pictureBox = new PictureBox
        {
            Dock      = DockStyle.Fill,
            SizeMode  = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(28, 28, 40),
            BorderStyle = BorderStyle.None
        };
        _statusLabel = new Label
        {
            Dock      = DockStyle.Bottom,
            Height    = 24,
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            BackColor = Color.FromArgb(20, 20, 30),
            ForeColor = Color.FromArgb(160, 160, 185)
        };
        _filesSplit.Panel2.Controls.Add(_pictureBox);
        _filesSplit.Panel2.Controls.Add(_statusLabel);
        _filesSplit.Panel2.Controls.SetChildIndex(_statusLabel, 0);
        _tabControl.TabPages[1].Controls.Add(_filesSplit);

        // ── Pestaña Editor HUD con sub-pestañas por categoría ─────────
        var editorSplit    = new SplitContainer { Dock = DockStyle.Fill };
        var subTabControl  = new TabControl { Dock = DockStyle.Fill };
        var panelsByTab    = new Dictionary<TabPage, HudEditorPanel>();
        var categories     = new[] { "HUD", "Inventario", "Personaje", "Amigos", "Carga", "Inicio" };

        foreach (var cat in categories)
        {
            var page  = new TabPage(cat);
            var panel = new HudEditorPanel
            {
                Dock            = DockStyle.Fill,
                BackColor       = Color.FromArgb(20, 20, 28),
                CurrentCategory = cat
            };
            page.Controls.Add(panel);
            subTabControl.TabPages.Add(page);
            panelsByTab[page] = panel;
        }

        _hudEditor = panelsByTab[subTabControl.TabPages[0]];

        subTabControl.SelectedIndexChanged += (_, _) =>
        {
            var oldDoc    = _hudEditor.Document;
            var oldFolder = _hudEditor.BaseFolder;

            _hudEditor = panelsByTab[subTabControl.SelectedTab!];
            _hudEditor.Document    = oldDoc;
            _hudEditor.BaseFolder  = oldFolder;

            _propertiesPanel.CurrentCategory = _hudEditor.CurrentCategory;
            _propertiesPanel.SelectedElement = null;
            _propertiesPanel.RefreshElementList();
            _hudEditor.Invalidate();

            SyncToolbarFromPanel();
        };

        _propertiesPanel = new HudPropertiesPanel { Dock = DockStyle.Fill };

        var sharedDoc = HudDocument.CreateDefault();
        foreach (var pnl in panelsByTab.Values)
        {
            pnl.Document = sharedDoc;
            pnl.SelectionChanged += () =>
            {
                if (_hudEditor == pnl)
                    _propertiesPanel.SelectedElement = pnl.SelectedElement;
            };
            pnl.DragMoved += el =>
            {
                if (_hudEditor == pnl) _propertiesPanel.UpdatePositionOnly(el);
            };
            pnl.HoveredElementChanged += el =>
            {
                if (_hudEditor == pnl)
                    _editorStatusLabel.Text = el != null
                        ? $"Sobre: {el.ResolvePath}  ({el.X:F0}, {el.Y:F0})  {el.Width:F0}×{el.Height:F0}"
                        : "";
            };
            pnl.ZoomChanged += () =>
            {
                if (_hudEditor == pnl)
                    _lblZoom.Text = $"{_hudEditor.Zoom * 100:F0}%";
            };
        }

        _propertiesPanel.Document        = sharedDoc;
        _propertiesPanel.CurrentCategory = _hudEditor.CurrentCategory;
        _propertiesPanel.ElementSelected += el =>
        {
            _hudEditor.SelectedElement  = el;
            _propertiesPanel.SelectedElement = el;
        };
        _propertiesPanel.ElementChanged += () => _hudEditor.Invalidate();

        editorSplit.Panel1.Controls.Add(subTabControl);
        editorSplit.Panel2.Controls.Add(_propertiesPanel);
        _tabControl.TabPages[0].Controls.Add(editorSplit);

        // ── Status strip ──────────────────────────────────────────────
        _editorStatusLabel = new ToolStripStatusLabel
            { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        var statusStrip = new StatusStrip
        {
            BackColor   = Color.FromArgb(20, 20, 30),
            ForeColor   = Color.FromArgb(140, 140, 165),
            SizingGrip  = false,
            Items       = { _editorStatusLabel }
        };

        Controls.Add(_tabControl);
        Controls.Add(statusStrip);
        Controls.Add(toolStrip);
        Controls.Add(menuStrip);

        // ── Shown ─────────────────────────────────────────────────────
        Shown += (_, _) =>
        {
            try
            {
                void ApplyLayout()
                {
                    try
                    {
                        if (_filesSplit.Width > 0 && _filesSplit.Height > 0)
                        {
                            _filesSplit.Panel1MinSize = 120;
                            _filesSplit.Panel2MinSize = 150;
                            var dim = _filesSplit.Orientation == Orientation.Horizontal
                                ? _filesSplit.Height : _filesSplit.Width;
                            if (dim > 150)
                                _filesSplit.SplitterDistance = Math.Clamp(220, 120, dim - 150);
                        }
                        if (_tabControl.TabPages[0].Controls[0] is SplitContainer es && es.Width > 200)
                        {
                            es.Panel2MinSize = 210;
                            if (es.Width - es.SplitterWidth > 210 + 400)
                                es.SplitterDistance = es.Width - es.SplitterWidth - 220;
                        }
                    }
                    catch (Exception ex2) { Log.Error("Layout", ex2); }
                }
                ApplyLayout();
                BeginInvoke(ApplyLayout);
                _propertiesPanel.RefreshElementList();

                var ifacePath = Paths.DefaultInterfaceFolder;
                if (Directory.Exists(ifacePath))
                    LoadFolder(ifacePath, showHudTab: true);
                else
                {
                    _hudEditor.BaseFolder = null;
                    _statusLabel.Text = $"Interface no encontrada: {ifacePath}";
                }
                Log.Write("Shown OK");
            }
            catch (Exception ex) { Log.Error("Shown", ex); }
        };

        Log.Write("ResumeLayout");
        ResumeLayout(false);
    }

    // ── Construcción del toolbar ──────────────────────────────────────
    private ToolStrip BuildToolStrip()
    {
        var ts = new ToolStrip
        {
            BackColor  = Color.FromArgb(38, 38, 52),
            ForeColor  = Color.White,
            GripStyle  = ToolStripGripStyle.Hidden,
            Renderer   = new DarkToolStripRenderer()
        };

        // Grid
        _btnGrid = new ToolStripButton("⊞ Grid")
        {
            CheckOnClick = true,
            ToolTipText  = "Mostrar cuadrícula (tecla G)",
            ForeColor    = Color.FromArgb(180, 180, 210)
        };
        _btnGrid.CheckedChanged += (_, _) =>
        {
            _hudEditor.ShowGrid = _btnGrid.Checked;
            _hudEditor.Invalidate();
        };
        ts.Items.Add(_btnGrid);

        // Snap
        _btnSnap = new ToolStripButton("⌖ Snap")
        {
            CheckOnClick = true,
            ToolTipText  = "Alinear al grid (tecla S)",
            ForeColor    = Color.FromArgb(180, 180, 210)
        };
        _btnSnap.CheckedChanged += (_, _) =>
        {
            _hudEditor.SnapToGrid = _btnSnap.Checked;
            _hudEditor.Invalidate();
        };
        ts.Items.Add(_btnSnap);

        // Grid step
        ts.Items.Add(new ToolStripLabel("  Paso:")
            { ForeColor = Color.FromArgb(150, 150, 175) });
        _cbxGridStep = new ToolStripComboBox
        {
            Width         = 54,
            DropDownStyle = ComboBoxStyle.DropDownList,
            ToolTipText   = "Tamaño de celda del grid"
        };
        _cbxGridStep.Items.AddRange(new object[] { "1", "2", "4", "8", "16", "32" });
        _cbxGridStep.SelectedIndex = 3; // 8 px por defecto
        _cbxGridStep.SelectedIndexChanged += (_, _) =>
        {
            if (float.TryParse(_cbxGridStep.SelectedItem?.ToString(), out var step))
            {
                _hudEditor.GridStep = step;
                _hudEditor.Invalidate();
            }
        };
        ts.Items.Add(_cbxGridStep);

        ts.Items.Add(new ToolStripSeparator());

        // Preview de resolución
        _btnPreview = new ToolStripButton("◈ Res. Preview")
        {
            CheckOnClick = true,
            ToolTipText  = "Previsualizar cómo escala el HUD en la resolución seleccionada",
            ForeColor    = Color.FromArgb(200, 160, 100)
        };
        _btnPreview.CheckedChanged += (_, _) =>
        {
            _hudEditor.ShowResolutionPreview = _btnPreview.Checked;
            ApplyResolutionPreview();
            _hudEditor.Invalidate();
        };
        ts.Items.Add(_btnPreview);

        _cbxResolution = new ToolStripComboBox
        {
            Width         = 130,
            DropDownStyle = ComboBoxStyle.DropDownList,
            ToolTipText   = "Resolución objetivo para el preview"
        };
        _cbxResolution.Items.AddRange(new object[]
        {
            "640×480", "800×600", "1024×768",
            "1280×720 (HD)", "1280×1024",
            "1920×1080 (FHD)", "2560×1440 (QHD)", "3840×2160 (4K)"
        });
        _cbxResolution.SelectedIndex = 5; // 1920×1080 por defecto
        _cbxResolution.SelectedIndexChanged += (_, _) =>
        {
            ApplyResolutionPreview();
            _hudEditor.Invalidate();
        };
        ts.Items.Add(_cbxResolution);

        ts.Items.Add(new ToolStripSeparator());

        // Zoom
        ts.Items.Add(new ToolStripLabel("  Zoom:")
            { ForeColor = Color.FromArgb(150, 150, 175) });
        _lblZoom = new ToolStripLabel("100%")
        {
            Width     = 40,
            ForeColor = Color.FromArgb(200, 220, 255),
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        ts.Items.Add(_lblZoom);

        var btnZoomReset = new ToolStripButton("↺ Reset")
        {
            ToolTipText = "Restablecer zoom (tecla 0)",
            ForeColor   = Color.FromArgb(160, 180, 210)
        };
        btnZoomReset.Click += (_, _) => _hudEditor.ResetView();
        ts.Items.Add(btnZoomReset);

        ts.Items.Add(new ToolStripSeparator());

        // Guardar rápido
        var btnSave = new ToolStripButton("💾 Guardar")
        {
            ToolTipText = "Guardar layout (Ctrl+S)",
            ForeColor   = Color.FromArgb(100, 220, 130)
        };
        btnSave.Click += (_, _) => SaveLayoutQuick();
        ts.Items.Add(btnSave);

        return ts;
    }

    private void ApplyResolutionPreview()
    {
        var txt = _cbxResolution.SelectedItem?.ToString() ?? "1920×1080 (FHD)";
        // Extraer WxH del texto "1920×1080 (FHD)" → 1920, 1080
        var parts = txt.Split('×', ' ');
        if (parts.Length >= 2 &&
            int.TryParse(parts[0].Trim(), out var w) &&
            int.TryParse(parts[1].Trim(), out var h))
        {
            _hudEditor.PreviewWidth  = w;
            _hudEditor.PreviewHeight = h;
        }
    }

    private void SyncToolbarFromPanel()
    {
        _btnGrid.Checked    = _hudEditor.ShowGrid;
        _btnSnap.Checked    = _hudEditor.SnapToGrid;
        _btnPreview.Checked = _hudEditor.ShowResolutionPreview;
        _lblZoom.Text       = $"{_hudEditor.Zoom * 100:F0}%";

        var stepStr = _hudEditor.GridStep.ToString("F0");
        var idx = _cbxGridStep.Items.IndexOf(stepStr);
        if (idx >= 0) _cbxGridStep.SelectedIndex = idx;
    }

    // ── Campos privados ───────────────────────────────────────────────
    private TabControl _tabControl = null!;
    private SplitContainer _filesSplit = null!;
    private ListBox _listBox = null!;
    private PictureBox _pictureBox = null!;
    private Label _statusLabel = null!;
    private HudEditorPanel _hudEditor = null!;
    private HudPropertiesPanel _propertiesPanel = null!;
    private ToolStripStatusLabel _editorStatusLabel = null!;

    // ── Acciones de archivo ───────────────────────────────────────────
    private void OpenFolder()
    {
        var defaultPath = Paths.DefaultInterfaceFolder;
        using var dlg = new FolderBrowserDialog
        {
            Description          = "Seleccionar carpeta Interface",
            UseDescriptionForTitle = true,
            SelectedPath         = Directory.Exists(defaultPath) ? defaultPath : ""
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
            Title  = "Abrir archivo"
        };
        if (dlg.ShowDialog() == DialogResult.OK) LoadImage(dlg.FileName);
    }

    public void LoadFolder(string path, bool showHudTab = false)
    {
        _currentFolder = path;
        _listBox.Items.Clear();
        var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(f => new[] { ".ozj", ".ozt", ".jpg", ".jpeg", ".tga" }
                        .Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(f => f[(path.Length + 1)..].Replace('\\', '/'))
            .OrderBy(Path.GetFileName)
            .ToArray();

        foreach (var f in files) _listBox.Items.Add(f);
        if (_listBox.Items.Count > 0) _listBox.SelectedIndex = 0;

        _statusLabel.Text     = $"{_listBox.Items.Count} archivos en {path}";
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
                _statusLabel.Text += "  |  Layout cargado";
            }
            catch { /* ignorar */ }
        }

        if (showHudTab) _tabControl.SelectedIndex = 0;
    }

    private void LoadLayout()
    {
        var defaultPath = Path.Combine(Paths.DefaultInterfaceFolder, "hud_layout.json");
        using var dlg = new OpenFileDialog
        {
            Filter           = "Layout HUD (*.json)|*.json|Todos (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(defaultPath) ?? "",
            Title            = "Cargar layout"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            var doc = HudDocument.Load(dlg.FileName);
            UpdateAllTabDocuments(doc);
            _layoutPath = dlg.FileName;
            _statusLabel.Text = $"Layout cargado: {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "HUD Editor",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveLayoutQuick()
    {
        if (_layoutPath != null)
        {
            try
            {
                _hudEditor.Document.Save(_layoutPath);
                _statusLabel.Text = $"Guardado: {Path.GetFileName(_layoutPath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error guardando: {ex.Message}", "HUD Editor",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            SaveLayout();
        }
    }

    private void SaveLayout()
    {
        var defaultPath = Path.Combine(Paths.DefaultInterfaceFolder, "hud_layout.json");
        using var dlg = new SaveFileDialog
        {
            Filter           = "Layout HUD (*.json)|*.json|Todos (*.*)|*.*",
            FileName         = _layoutPath ?? defaultPath,
            InitialDirectory = Path.GetDirectoryName(defaultPath) ?? "",
            Title            = "Guardar layout"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            _hudEditor.Document.Save(dlg.FileName);
            _layoutPath = dlg.FileName;
            _statusLabel.Text = $"Layout guardado: {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "HUD Editor",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ResetLayout()
    {
        UpdateAllTabDocuments(HudDocument.CreateDefault());
        _statusLabel.Text = "Layout restablecido a valores por defecto";
    }

    private void UpdateAllTabDocuments(HudDocument doc)
    {
        _hudEditor.Document       = doc;
        _propertiesPanel.Document = doc;

        if (_tabControl.TabPages[0].Controls[0] is SplitContainer es &&
            es.Panel1.Controls[0] is TabControl stc)
        {
            foreach (TabPage page in stc.TabPages)
                if (page.Controls[0] is HudEditorPanel pnl)
                {
                    pnl.Document = doc;
                    pnl.Invalidate();
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
            Filter           = "Header C++ (*.h)|*.h|Todos (*.*)|*.*",
            FileName         = "HudLayout.h",
            InitialDirectory = Path.GetDirectoryName(defaultPath) ?? "",
            Title            = "Exportar posiciones a C++"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dlg.FileName, HudCppExporter.ExportHeader(_hudEditor.Document));
            _statusLabel.Text = $"Exportado: {Path.GetFileName(dlg.FileName)}";
            MessageBox.Show(
                "Header generado.\n\nIncluí en el código:\n#include \"HudLayout.h\"\n\n" +
                "Usá las constantes:\nHudLayout::NEWUI_MENU01_X, etc.",
                "HUD Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "HUD Editor",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Visor de imágenes ─────────────────────────────────────────────
    private void ListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_listBox.SelectedIndex < 0 || string.IsNullOrEmpty(_currentFolder)) return;
        var name = _listBox.SelectedItem?.ToString();
        if (!string.IsNullOrEmpty(name))
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
            _currentImagePath = path;
            var img = OzImageLoader.Load(path);
            if (img != null)
            {
                _currentImage = img;
                var old = _pictureBox.Image;
                _pictureBox.Image = img;
                old?.Dispose();
                _statusLabel.Text = $"{Path.GetFileName(path)}  —  {img.Width}×{img.Height}";
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

    // ── Guardar OZJ / OZT ────────────────────────────────────────────
    private void SaveAsOzj()
    {
        if (_currentImage == null)
        {
            MessageBox.Show("No hay imagen cargada.", "HUD Editor",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Filter   = "OZJ (*.ozj)|*.ozj|Todos (*.*)|*.*",
            FileName = _currentImagePath != null
                ? Path.GetFileNameWithoutExtension(_currentImagePath) + ".ozj"
                : "image.ozj",
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
            MessageBox.Show("No hay imagen cargada.", "HUD Editor",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Filter   = "OZT (*.ozt)|*.ozt|Todos (*.*)|*.*",
            FileName = _currentImagePath != null
                ? Path.GetFileNameWithoutExtension(_currentImagePath) + ".ozt"
                : "image.ozt",
            Title = "Guardar como OZT"
        };
        if (dlg.ShowDialog() == DialogResult.OK && OzImageLoader.SaveOzt(_currentImage, dlg.FileName))
        {
            _statusLabel.Text = $"Guardado: {Path.GetFileName(dlg.FileName)}";
            _hudEditor.ClearCache();
            _hudEditor.Invalidate();
        }
    }

    // ── Keyboard ──────────────────────────────────────────────────────
    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.S)
        {
            SaveLayoutQuick();
            e.Handled = true;
            return;
        }

        if (e.KeyCode != Keys.Delete) return;
        if (_tabControl.SelectedIndex != 0) return;
        var el = _hudEditor.SelectedElement;
        if (el == null || _propertiesPanel.Document == null) return;
        var idx = _propertiesPanel.Document.Elements.IndexOf(el);
        if (idx < 0) return;
        _propertiesPanel.Document.Elements.RemoveAt(idx);
        _hudEditor.SelectedElement = _propertiesPanel.Document.Elements.Count > 0
            ? _propertiesPanel.Document.Elements[Math.Min(idx, _propertiesPanel.Document.Elements.Count - 1)]
            : null;
        _propertiesPanel.SelectedElement = _hudEditor.SelectedElement;
        _propertiesPanel.RefreshElementList();
        _hudEditor.Invalidate();
        e.Handled = true;
    }

    // ── Drag & Drop ───────────────────────────────────────────────────
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
            _tabControl.SelectedIndex = 0;
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

// ── Renderizadores oscuros para menus y toolbar ───────────────────────
internal class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }
}

internal class DarkToolStripRenderer : ToolStripProfessionalRenderer
{
    public DarkToolStripRenderer() : base(new DarkColorTable()) { }
}

internal class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuBorder                    => Color.FromArgb(55, 55, 72);
    public override Color MenuItemSelected             => Color.FromArgb(60, 60, 80);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(55, 55, 75);
    public override Color MenuItemSelectedGradientEnd  => Color.FromArgb(55, 55, 75);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(45, 45, 65);
    public override Color MenuItemPressedGradientEnd   => Color.FromArgb(45, 45, 65);
    public override Color ToolStripDropDownBackground  => Color.FromArgb(38, 38, 52);
    public override Color ImageMarginGradientBegin     => Color.FromArgb(42, 42, 56);
    public override Color ImageMarginGradientMiddle    => Color.FromArgb(42, 42, 56);
    public override Color ImageMarginGradientEnd       => Color.FromArgb(42, 42, 56);
    public override Color ToolStripBorder              => Color.FromArgb(50, 50, 68);
    public override Color ToolStripContentPanelGradientBegin => Color.FromArgb(38, 38, 52);
    public override Color ToolStripContentPanelGradientEnd   => Color.FromArgb(38, 38, 52);
    public override Color ButtonCheckedGradientBegin   => Color.FromArgb(70, 60, 110);
    public override Color ButtonCheckedGradientEnd     => Color.FromArgb(70, 60, 110);
    public override Color ButtonCheckedHighlight       => Color.FromArgb(80, 70, 130);
    public override Color ButtonPressedGradientBegin   => Color.FromArgb(60, 50, 90);
    public override Color ButtonPressedGradientEnd     => Color.FromArgb(60, 50, 90);
    public override Color ButtonSelectedGradientBegin  => Color.FromArgb(55, 55, 78);
    public override Color ButtonSelectedGradientEnd    => Color.FromArgb(55, 55, 78);
    public override Color SeparatorDark                => Color.FromArgb(55, 55, 72);
    public override Color SeparatorLight               => Color.FromArgb(65, 65, 85);
}
