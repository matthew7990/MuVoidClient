using System.Text.Json;
using System.Text.Json.Serialization;

namespace OzViewer;

public class HudDocument
{
    public int GameWidth { get; set; } = 640;
    public int GameHeight { get; set; } = 480;
    public List<HudElement> Elements { get; set; } = new();

    public static HudDocument CreateDefault()
    {
        var doc = new HudDocument();

        // ── HUD Inferior (Marcos y Barras) ──────────────────────────────────────────
        doc.Add("Marco izq (menu01)",     "newui_menu01.jpg",          0,   429, 256,  51, category: "HUD");
        doc.Add("Marco centro (menu02)",  "newui_menu02.jpg",        256,   429, 128,  51, category: "HUD");
        doc.Add("Marco der (menu03)",     "newui_menu03.jpg",         384,   429, 256,  51, "partCharge1/newui_menu03.jpg", category: "HUD");
        doc.Add("Skill list overlay",     "newui_menu02-03.jpg",     222,   429, 160,  40, category: "HUD");

        doc.Add("Vida bar",               "newui_menu_red.jpg",       158,   432,  45,  39, category: "HUD");
        doc.Add("Vida veneno",            "newui_menu_green.jpg",     158,   432,  45,  39, category: "HUD");
        doc.Add("Maná bar",               "newui_menu_blue.jpg",      437,   432,  45,  39, category: "HUD");
        doc.Add("AG bar",                 "newui_menu_ag.jpg",        420,   431,  16,  39, category: "HUD");
        doc.Add("SD bar",                 "newui_menu_sd.jpg",        204,   431,  16,  39, category: "HUD");
        doc.Add("EXP bar",                "newui_exbar.jpg",            2,   473, 629,   4, category: "HUD");
        doc.Add("EXP Master bar",         "Exbar_Master.jpg",           2,   473, 629,   4, category: "HUD");

        // ── Botones del menú inferior ─────────────────────────────────────────────
        doc.Add("Btn CShop",              "newui_menu_Bt05.jpg",      489,   429,  30,  41, "partCharge1/newui_menu_Bt05.jpg", category: "HUD");
        doc.Add("Btn Personaje",          "newui_menu_Bt01.jpg",      519,   429,  30,  41, "partCharge1/newui_menu_Bt01.jpg", category: "HUD");
        doc.Add("Btn Inventario",         "newui_menu_Bt02.jpg",      549,   429,  30,  41, "partCharge1/newui_menu_Bt02.jpg", category: "HUD");
        doc.Add("Btn Amigos",             "newui_menu_Bt03.jpg",      579,   429,  30,  41, "partCharge1/newui_menu_Bt03.jpg", category: "HUD");
        doc.Add("Btn Ventana",            "newui_menu_Bt04.jpg",      609,   429,  30,  41, "partCharge1/newui_menu_Bt04.jpg", category: "HUD");

        // ── Skill slots y Hotkeys ────────────────────────────────────────────────
        doc.Add("Skill slot 1",           "newui_skillbox.jpg",       222,   431,  32,  38, category: "HUD");
        doc.Add("Skill slot 2",           "newui_skillbox.jpg",       254,   431,  32,  38, category: "HUD");
        doc.Add("Skill slot 3",           "newui_skillbox.jpg",       286,   431,  32,  38, category: "HUD");
        doc.Add("Skill slot 4",           "newui_skillbox.jpg",       318,   431,  32,  38, category: "HUD");
        doc.Add("Skill slot 5",           "newui_skillbox.jpg",       350,   431,  32,  38, category: "HUD");
        doc.Add("Skill actual box",       "newui_skillbox2.jpg",      385,   431,  32,  38, category: "HUD");

        // Elementos de texto de ejemplo
        doc.Elements.Add(HudElement.CreateText("Texto: HP", "350/350", 158, 415, "HUD"));
        doc.Elements.Add(HudElement.CreateText("Texto: MP", "200/200", 437, 415, "HUD"));

        doc.Add("Item hotkey Q",          "newui_skillbox.jpg",        10,   443,  20,  20, category: "HUD");
        doc.Add("Item hotkey W",          "newui_skillbox.jpg",        48,   443,  20,  20, category: "HUD");
        doc.Add("Item hotkey E",          "newui_skillbox.jpg",        86,   443,  20,  20, category: "HUD");
        doc.Add("Item hotkey R",          "newui_skillbox.jpg",       124,   443,  20,  20, category: "HUD");

        // ── Chat ──────────────────────────────────────────────────────────────────
        doc.Add("Chat: Drag Button",      "newui_scrollbar_stretch.jpg", 0,   300,  30,  15, category: "Chat");
        doc.Add("Chat: Input Background", "newui_chat_back.tga",        0,   430, 450,  50, category: "Chat");
        doc.Add("Chat: Input Active",     "newui_chat_on.tga",          10,  440, 430,  30, category: "Chat");

        // ── Ventanas (Inventario, Personaje, etc.) ────────────────────────────────
        // Inventario
        doc.Add("Inv: Fondo",             "newui_msgbox_back.jpg",    450,     0, 190, 429, category: "Inventario");
        doc.Add("Inv: Titulo",            "newui_item_back04.tga",    450,     0, 190,  64, category: "Inventario");
        doc.Add("Inv: Btn Salir",         "newui_exit_00.tga",        463,   391,  36,  29, category: "Inventario");

        // Personaje
        doc.Add("Cha: Fondo",             "newui_msgbox_back.jpg",    450,     0, 190, 429, category: "Personaje");
        doc.Add("Cha: Stats Box",         "newui_cha_textbox02.tga",  462,   120, 170,  19, category: "Personaje");
        doc.Add("Cha: Btn Salir",         "newui_exit_00.tga",        463,   392,  36,  29, category: "Personaje");

        // Guild
        doc.Add("Guild: Fondo",           "newui_msgbox_back.jpg",    450,     0, 190, 429, category: "Guild");
        doc.Add("Guild: Titulo",          "newui_guild_back01.tga",   450,     0, 190,  64, category: "Guild");

        // Party
        doc.Add("Party: Fondo",           "newui_msgbox_back.jpg",    450,     0, 190, 429, null, "Party");
        doc.Add("Party: Slot 1",          "newui_party_bar.tga",      10,    10, 150,  30, null, "Party");

        // ── Global Fonts ──────────────────────────────────────────────────────────
        doc.Elements.Add(HudElement.CreateText("Font: Standard", "Text", 0, 0, "Fuentes"));
        doc.Elements.Last().FontSize = 12;
        doc.Elements.Add(HudElement.CreateText("Font: Bold", "Text", 0, 0, "Fuentes"));
        doc.Elements.Last().FontSize = 12;
        doc.Elements.Last().FontBold = true;
        doc.Elements.Add(HudElement.CreateText("Font: Big", "Text", 0, 0, "Fuentes"));
        doc.Elements.Last().FontSize = 24;
        doc.Elements.Last().FontBold = true;
        doc.Elements.Add(HudElement.CreateText("Font: Fixed", "Text", 0, 0, "Fuentes"));
        doc.Elements.Last().FontSize = 14;

        // ── Pantalla de carga (labels compatibles con HudLayout.h) ─────────────────
        doc.Add("Carga: Loading Part 1",  "LSBg01.OZJ",   0,   0, 400, 512, null, "Carga");
        doc.Add("Carga: Loading Part 2",  "LSBg02.OZJ", 400,   0, 400, 512, null, "Carga");
        doc.Add("Carga: Loading Part 3",  "LSBg03.OZJ",   0, 512, 400,  88, null, "Carga");
        doc.Add("Carga: Loading Part 4",  "LSBg04.OZJ", 400, 512, 400,  88, null, "Carga");

        // ── Pantalla de inicio (WebzenScene) ──────────────────────────────────────
        doc.Add("Inicio: Strip Top Left",  "New_lo_back_01.jpg",   0,   0, 400,  69, null, "Inicio");
        doc.Add("Inicio: Strip Top Right", "New_lo_back_02.jpg", 400,   0, 400,  69, null, "Inicio");
        doc.Add("Inicio: Strip Bot Left",  "lo_back_s5_03.jpg",    0, 500, 400, 100, null, "Inicio");
        doc.Add("Inicio: Strip Bot Right", "lo_back_s5_04.jpg",  400, 500, 400, 100, null, "Inicio");
        doc.Add("Inicio: Logo",            "lo_121518.tga",       544,  60, 256, 206, null, "Inicio");
        doc.Add("Inicio: Pattern",         "lo_lo.jpg",             0,   0, 800, 600, null, "Inicio");

        // ── NPC Shop / Warehouse ──────────────────────────────────────────────────
        doc.Add("Shop: Fondo",            "newui_msgbox_back.jpg",      0,     0, 190, 429, null, "Tienda");
        doc.Add("Shop: Slot",             "newui_item_back01.ozt",     10,    20,  50,  50, null, "Tienda");

        // ── Mix (Chaos Machine) ───────────────────────────────────────────────────
        doc.Add("Mix: Fondo",             "newui_item_back01.ozt",    100,   100, 190, 429, null, "Mix");

        for (int i = 0; i < doc.Elements.Count; i++)
            doc.Elements[i].ZOrder = i;

        return doc;
    }

    public void ClassifyElements()
    {
        foreach (var el in Elements)
        {
            if (string.IsNullOrEmpty(el.Label)) continue;

            var lower = el.Label.ToLowerInvariant();
            if (lower.Contains("inv:") || lower.Contains("inventario") || lower.Contains("inventory"))
                el.Category = "Inventario";
            else if (lower.Contains("cha:") || lower.Contains("personaje") || lower.Contains("character"))
                el.Category = "Personaje";
            else if (lower.Contains("amigo") || lower.Contains("friend"))
                el.Category = "Amigos";
            else if (lower.StartsWith("inicio:") || lower.Contains("webzen") ||
                     lower.Contains("lo_back") || lower.Contains("new_lo"))
                el.Category = "Inicio";
            else if (lower.Contains("loading") || lower.Contains("carga") || lower.Contains("lsbg"))
                el.Category = "Carga";
            else
                el.Category = "HUD";
        }
    }


    private void Add(string label, string fileName, float x, float y, float w, float h, string? altPath = null, string category = "HUD")
        => Elements.Add(new HudElement(label, fileName, x, y, w, h, altPath, category));

    public static HudDocument Load(string path)
    {
        var json = File.ReadAllText(path);
        var doc = JsonSerializer.Deserialize<HudDocument>(json) ?? CreateDefault();

        // Sincronizar con defaults para asegurar que Carga/Inicio existan
        var def = CreateDefault();
        foreach (var defEl in def.Elements)
        {
            bool shouldMerge = defEl.Label.Contains(":") ||
                               defEl.Category == "Carga" || defEl.Category == "Inicio";
            if (shouldMerge && !doc.Elements.Any(e => e.Label == defEl.Label))
                doc.Elements.Add(defEl.Clone());
        }

        doc.ClassifyElements();
        return doc;
    }

    public void Save(string path)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(this, opts));
    }

    public static string ResolveImagePath(string baseFolder, HudElement el)
    {
        var names = new List<string> { el.ResolvePath };
        if (!string.IsNullOrEmpty(el.AltPath)) names.Add(el.AltPath);
        
        foreach (var name in names)
        {
            var fullPath = Path.Combine(baseFolder, name);
            var dir = Path.GetDirectoryName(fullPath) ?? baseFolder;
            var fname = Path.GetFileNameWithoutExtension(name);
            foreach (var ext in new[] { ".ozj", ".ozt", ".jpg", ".jpeg", ".tga" })
            {
                var p = Path.Combine(dir, fname + ext);
                if (File.Exists(p)) return p;
            }
        }
        return Path.Combine(baseFolder, el.ResolvePath);
    }
}
