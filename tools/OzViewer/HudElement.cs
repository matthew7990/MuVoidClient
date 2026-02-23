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

    // --- Propiedades de Texto ---
    public bool IsText { get; set; } = false;
    public string TextValue { get; set; } = "Preview Text";
    public string FontName { get; set; } = "Tahoma";
    public float FontSize { get; set; } = 10;
    public bool FontBold { get; set; } = false;
    public string TextColorHex { get; set; } = "#FFFFFF";

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

    public static HudElement CreateText(string label, string text, float x, float y, string category = "HUD")
    {
        return new HudElement {
            Label = label,
            TextValue = text,
            X = x, Y = y,
            IsText = true,
            Category = category,
            Width = 100, Height = 20
        };
    }

    public HudElement Clone() => new()
    {
        Label = Label,
        FileName = FileName,
        AltPath = AltPath,
        X = X, Y = Y, Width = Width, Height = Height,
        ZOrder = ZOrder, Visible = Visible,
        Category = Category,
        IsText = IsText,
        TextValue = TextValue,
        FontName = FontName,
        FontSize = FontSize,
        FontBold = FontBold,
        TextColorHex = TextColorHex
    };

    public bool Contains(float px, float py) =>
        px >= X && px <= X + Width && py >= Y && py <= Y + Height;
}
