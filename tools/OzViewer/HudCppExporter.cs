using System.Text;
using System.Text.RegularExpressions;

namespace OzViewer;

public static class HudCppExporter
{
    public static string ExportHeader(HudDocument doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#pragma once");
        sb.AppendLine();
        sb.AppendLine("// OzViewer - Generated HUD Layout");
        sb.AppendLine("namespace HudLayout");
        sb.AppendLine("{");

        var groups = doc.Elements.GroupBy(e => e.Category).OrderBy(g => g.Key == "HUD" ? 0 : 1).ThenBy(g => g.Key);

        foreach (var group in groups)
        {
            sb.AppendLine($"  // --- {group.Key} ---");
            foreach (var el in group)
            {
                var name = ToCppName(el);
                
                sb.AppendLine($"  constexpr float {name}_X = {el.X}f;");
                sb.AppendLine($"  constexpr float {name}_Y = {el.Y}f;");
                sb.AppendLine($"  constexpr float {name}_W = {el.Width}f;");
                sb.AppendLine($"  constexpr float {name}_H = {el.Height}f;");

                if (el.IsText)
                {
                    sb.AppendLine($"  constexpr float {name}_FONT_SIZE = {el.FontSize}f;");
                    sb.AppendLine($"  constexpr bool  {name}_FONT_BOLD = {(el.FontBold ? "true" : "false")};");
                    
                    // Convert Hex to RGBA
                    if (el.TextColorHex.StartsWith("#") && el.TextColorHex.Length >= 7)
                    {
                        var r = Convert.ToInt32(el.TextColorHex.Substring(1, 2), 16);
                        var g = Convert.ToInt32(el.TextColorHex.Substring(3, 2), 16);
                        var b = Convert.ToInt32(el.TextColorHex.Substring(5, 2), 16);
                        sb.AppendLine($"  // Color: {el.TextColorHex}");
                        sb.AppendLine($"  constexpr unsigned char {name}_COLOR_R = {r};");
                        sb.AppendLine($"  constexpr unsigned char {name}_COLOR_G = {g};");
                        sb.AppendLine($"  constexpr unsigned char {name}_COLOR_B = {b};");
                    }
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string ToCppName(HudElement el)
    {
        var raw = string.IsNullOrWhiteSpace(el.Label) ? Path.GetFileNameWithoutExtension(el.FileName) : el.Label;
        if (string.IsNullOrWhiteSpace(raw)) raw = "ELEMENT";

        // Remove accents and special chars
        var name = Regex.Replace(raw.ToUpper(), @"[^A-Z0-9_]", "_");
        name = Regex.Replace(name, @"_+", "_").Trim('_');

        if (char.IsDigit(name[0])) name = "_" + name;

        return name;
    }
}
