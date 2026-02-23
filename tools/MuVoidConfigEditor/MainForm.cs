namespace MuVoidConfigEditor;

public class MainForm : Form
{
    private string? _currentPath;
    private List<ServerListEntry> _entries = new();
    private DataGridView? _grid;
    private Label? _statusLabel;

    public MainForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "MuVoid - Editor de Configuración";
        Size = new Size(700, 500);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(500, 350);

        var menuStrip = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("Archivo");
        fileMenu.DropDownItems.Add("Abrir ServerList.bmd...", null, (_, _) => OpenFile());
        fileMenu.DropDownItems.Add("Guardar", null, (_, _) => SaveFile());
        fileMenu.DropDownItems.Add("Guardar como...", null, (_, _) => SaveAs());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Salir", null, (_, _) => Close());
        menuStrip.Items.Add(fileMenu);
        MainMenuStrip = menuStrip;

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ReadOnly = false,
            RowHeadersVisible = true
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Index",
            HeaderText = "Índice",
            Width = 60,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "Nombre del servidor",
            Width = 200
        });
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "Pos",
            HeaderText = "Posición",
            Width = 100
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Sequence",
            HeaderText = "Secuencia",
            Width = 80
        });

        var posCol = (DataGridViewComboBoxColumn)_grid.Columns["Pos"];
        posCol.Items.AddRange("Izquierda", "Derecha", "Centro");

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0)
        };

        Controls.Add(_grid);
        Controls.Add(_statusLabel);
        Controls.Add(menuStrip);

        Load += (_, _) =>
        {
            var path = Paths.DefaultServerListPath;
            if (File.Exists(path))
            {
                LoadFile(path);
            }
            else
            {
                _statusLabel!.Text = $"ServerList.bmd no encontrado. Archivo → Abrir para seleccionar. Esperado: {path}";
            }
        };
    }

    private void OpenFile()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "ServerList (*.bmd)|*.bmd|Todos (*.*)|*.*",
            Title = "Abrir ServerList.bmd",
            InitialDirectory = Paths.DefaultDataLocal
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            LoadFile(dlg.FileName);
    }

    private void LoadFile(string path)
    {
        try
        {
            _entries = ServerListLoader.Load(path);
            _currentPath = path;
            RefreshGrid();
            _statusLabel!.Text = $"{_entries.Count} servidor(es) cargados desde {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshGrid()
    {
        _grid!.Rows.Clear();
        foreach (var e in _entries)
        {
            var posStr = e.Pos switch { 0 => "Izquierda", 1 => "Derecha", 2 => "Centro", _ => "?" };
            _grid.Rows.Add(e.Index, e.Name, posStr, e.Sequence);
        }
    }

    private void SaveFile()
    {
        if (string.IsNullOrEmpty(_currentPath))
        {
            SaveAs();
            return;
        }
        SaveTo(_currentPath);
    }

    private void SaveAs()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "ServerList (*.bmd)|*.bmd|Todos (*.*)|*.*",
            Title = "Guardar ServerList.bmd",
            FileName = _currentPath ?? Paths.DefaultServerListPath,
            InitialDirectory = Paths.DefaultDataLocal
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            SaveTo(dlg.FileName);
    }

    private void SaveTo(string path)
    {
        try
        {
            SyncGridToEntries();
            ServerListLoader.Save(path, _entries);
            _currentPath = path;
            _statusLabel!.Text = $"Guardado: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SyncGridToEntries()
    {
        _grid!.EndEdit();
        for (int i = 0; i < _grid.Rows.Count && i < _entries.Count; i++)
        {
            var row = _grid.Rows[i];
            var e = _entries[i];
            if (row.Cells["Name"].Value is string name)
                e.Name = name ?? "";
            if (row.Cells["Pos"].Value is string posStr)
            {
                e.Pos = posStr switch
                {
                    "Izquierda" => 0,
                    "Derecha" => 1,
                    "Centro" => 2,
                    _ => e.Pos
                };
            }
            if (row.Cells["Sequence"].Value is string seqStr && byte.TryParse(seqStr, out var seq))
                e.Sequence = seq;
        }
    }
}
