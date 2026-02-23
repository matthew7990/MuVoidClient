namespace OzViewer;

public class HudElement
{
    public string FileName { get; set; } = "";
    public string? AltPath { get; set; }
    public string Label { get; set; } = "";   // nombre descriptivo visible en el editor
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public int ZOrder { get; set; }
    public bool Visible { get; set; } = true;
    public string Category { get; set; } = "HUD";

    public string ResolvePath => AltPath ?? FileName;

    public HudElement() { }

    public HudElement(string label, string fileName, float x, float y, float w, float h, string? altPath = null, string category = "HUD")
    {
        Label = label;
        FileName = fileName;
        X = x; Y = y; Width = w; Height = h;
        AltPath = altPath;
        Category = category;
    }

    public HudElement Clone() => new()
    {
        Label = Label,
        FileName = FileName,
        AltPath = AltPath,
        X = X, Y = Y, Width = Width, Height = Height,
        ZOrder = ZOrder, Visible = Visible,
        Category = Category
    };

    public bool Contains(float px, float py) =>
        px >= X && px <= X + Width && py >= Y && py <= Y + Height;
}
