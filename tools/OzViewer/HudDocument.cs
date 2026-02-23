using System.Text.Json;
using System.Text.Json.Serialization;

namespace OzViewer;

public class HudDocument
{
    public int GameWidth { get; set; } = 640;
    public int GameHeight { get; set; } = 480;
    public List<HudElement> Elements { get; set; } = new();

    /// <summary>
    /// Posiciones exactas extraídas de NewUIMainFrameWindow.cpp / RenderFrame / RenderLifeMana /
    /// RenderGuageAG / RenderGuageSD / RenderExperience / SetButtonInfo / CNewUISkillList
    /// Resolución base: 640×480
    /// </summary>
    public static HudDocument CreateDefault()
    {
        var doc = new HudDocument();

        // ── Fondo barra inferior ──────────────────────────────────────────────────
        // RenderFrame(): tres partes del panel inferior
        doc.Add("Marco izq (menu01)",     "newui_menu01.jpg",          0,   429, 256,  51);
        doc.Add("Marco centro (menu02)",  "newui_menu02.jpg",        256,   429, 128,  51);
        doc.Add("Marco der (menu03)",     "newui_menu03.jpg",         384,   429, 256,  51, "partCharge1/newui_menu03.jpg");
        // Solo visible cuando la lista de skills está abierta (m_bSkillList == true)
        doc.Add("Skill list overlay",     "newui_menu02-03.jpg",     222,   429, 160,  40);

        // ── Barra de Vida (Life) ──────────────────────────────────────────────────
        // RenderLifeMana(): x=158, y=480-48=432, w=45, h=39
        doc.Add("Vida - rojo (HP bar)",   "newui_menu_red.jpg",       158,   432,  45,  39);
        doc.Add("Vida - verde (veneno)",  "newui_menu_green.jpg",     158,   432,  45,  39);

        // ── Barra de Maná (Mana) ─────────────────────────────────────────────────
        // RenderLifeMana(): x=256+128+53=437, y=432, w=45, h=39
        doc.Add("Maná - azul (MP bar)",   "newui_menu_blue.jpg",      437,   432,  45,  39);

        // ── Barra de AG (Skill Mana) ─────────────────────────────────────────────
        // RenderGuageAG(): x=256+128+36=420, y=480-49=431, w=16, h=39
        doc.Add("AG bar",                 "newui_menu_ag.jpg",        420,   431,  16,  39);

        // ── Barra de SD (Shield) ─────────────────────────────────────────────────
        // RenderGuageSD(): x=204, y=480-49=431, w=16, h=39
        doc.Add("SD bar (Shield)",        "newui_menu_sd.jpg",        204,   431,  16,  39);

        // ── Barra de Experiencia ─────────────────────────────────────────────────
        // RenderExperience(): x=2, y=473, w=629, h=4
        doc.Add("EXP bar",                "newui_exbar.jpg",            2,   473, 629,   4);
        doc.Add("EXP Master bar",         "Exbar_Master.jpg",           2,   473, 629,   4);

        // ── Botones del menú inferior ─────────────────────────────────────────────
        // SetButtonInfo(): base x=489, y=480-51=429, 30×41 c/u, izq→der
        doc.Add("Btn CShop (x_Next=489)", "newui_menu_Bt05.jpg",      489,   429,  30,  41, "partCharge1/newui_menu_Bt05.jpg");
        doc.Add("Btn Info Personaje",     "newui_menu_Bt01.jpg",      519,   429,  30,  41, "partCharge1/newui_menu_Bt01.jpg");
        doc.Add("Btn Inventario",         "newui_menu_Bt02.jpg",      549,   429,  30,  41, "partCharge1/newui_menu_Bt02.jpg");
        doc.Add("Btn Amigos",             "newui_menu_Bt03.jpg",      579,   429,  30,  41, "partCharge1/newui_menu_Bt03.jpg");
        doc.Add("Btn Ventana",            "newui_menu_Bt04.jpg",      609,   429,  30,  41, "partCharge1/newui_menu_Bt04.jpg");

        // ── Skill slots (RenderCurrentSkillAndHotSkillList) ─────────────────────
        // x arranca en 190, se incrementa 32 por iteración → slots 222,254,286,318,350
        // cada slot 32×38, y=431
        doc.Add("Skill slot 1 (hotkey)",  "newui_skillbox.jpg",       222,   431,  32,  38);
        doc.Add("Skill slot 2",           "newui_skillbox.jpg",       254,   431,  32,  38);
        doc.Add("Skill slot 3",           "newui_skillbox.jpg",       286,   431,  32,  38);
        doc.Add("Skill slot 4",           "newui_skillbox.jpg",       318,   431,  32,  38);
        doc.Add("Skill slot 5",           "newui_skillbox.jpg",       350,   431,  32,  38);

        // Botón skill actual (CheckMouseIn x=385,y=431,w=32,h=38) + icono en x=392,y=437,20×28
        doc.Add("Skill actual (caja)",    "newui_skillbox2.jpg",      385,   431,  32,  38);

        // ── Item hotkeys (RenderItems): x=10+(i*38), y=443, 20×20 ────────────────
        doc.Add("Item hotkey Q",          "newui_skillbox.jpg",        10,   443,  20,  20);
        doc.Add("Item hotkey W",          "newui_skillbox.jpg",        48,   443,  20,  20);
        doc.Add("Item hotkey E",          "newui_skillbox.jpg",        86,   443,  20,  20);
        doc.Add("Item hotkey R",          "newui_skillbox.jpg",       124,   443,  20,  20);

        // ── Sprite sheets (usados como icono, NO como fondo fijo) ────────────────
        doc.Add("Skill icons sheet 1",    "newui_skill.jpg",          222,   431, 160,  38);
        doc.Add("Skill icons sheet 2",    "newui_skill2.jpg",         222,   431,  32,  38);
        doc.Add("Skill command sheet",    "newui_command.jpg",        222,   431, 160,  38);
        doc.Add("Non-skill sheet 1",      "newui_non_skill.jpg",      222,   431,  32,  38);
        doc.Add("Non-skill sheet 2",      "newui_non_skill2.jpg",     222,   431,  32,  38);
        doc.Add("Non-command sheet",      "newui_non_command.jpg",    222,   431, 160,  38);
        doc.Add("Skill icons sheet 3",    "newui_skill3.jpg",         385,   431,  32,  38);
        doc.Add("Non-skill sheet 3",      "newui_non_skill3.jpg",     385,   431,  32,  38);

        // ── Ventanas principales ──────────────────────────────────────────────────
        // Inventario
        doc.Add("Inv: Fondo",             "newui_msgbox_back.jpg",    450,     0, 190, 429, null, "Inventario");
        doc.Add("Inv: Top",               "newui_item_back04.tga",    450,     0, 190,  64, null, "Inventario");
        doc.Add("Inv: Left",              "newui_item_back02-L.tga",  450,    64,  21, 320, null, "Inventario");
        doc.Add("Inv: Right",             "newui_item_back02-R.tga",  619,    64,  21, 320, null, "Inventario");
        doc.Add("Inv: Bottom",            "newui_item_back03.tga",    450,   384, 190,  45, null, "Inventario");
        doc.Add("Inv: Slot Helper",       "newui_item_fairy.tga",     465,    44,  46,  46, null, "Inventario");
        doc.Add("Inv: Slot Helm",         "newui_item_cap.tga",       525,    44,  46,  46, null, "Inventario");
        doc.Add("Inv: Slot Wing",         "newui_item_wing.tga",      570,    44,  61,  46, null, "Inventario");
        doc.Add("Inv: Slot Weapon (R)",   "newui_item_weapon(L).tga", 465,    87,  46,  66, null, "Inventario");
        doc.Add("Inv: Slot Armor",        "newui_item_upper.tga",     525,    87,  46,  66, null, "Inventario");
        doc.Add("Inv: Slot Weapon (L)",   "newui_item_weapon(R).tga", 585,    87,  46,  66, null, "Inventario");
        doc.Add("Inv: Slot Gloves",       "newui_item_gloves.tga",    465,   150,  46,  46, null, "Inventario");
        doc.Add("Inv: Slot Pants",        "newui_item_lower.tga",     525,   150,  46,  46, null, "Inventario");
        doc.Add("Inv: Slot Boots",        "newui_item_boots.tga",     585,   150,  46,  46, null, "Inventario");
        doc.Add("Inv: Slot Amulet",       "newui_item_necklace.tga",  504,    87,  28,  28, null, "Inventario");
        doc.Add("Inv: Slot Ring (R)",     "newui_item_ring.tga",      504,   150,  28,  28, null, "Inventario");
        doc.Add("Inv: Slot Ring (L)",     "newui_item_ring.tga",      564,   150,  28,  28, null, "Inventario");
        doc.Add("Inv: Money",             "newui_item_money.tga",     461,   364, 170,  26, null, "Inventario");
        doc.Add("Inv: Btn Exit",          "newui_exit_00.tga",        463,   391,  36,  29, null, "Inventario");
        doc.Add("Inv: Btn Repair",        "newui_repair_00.tga",      500,   391,  36,  29, null, "Inventario");
        doc.Add("Inv: Btn MyShop",        "newui_Bt_openshop.tga",    537,   391,  36,  29, null, "Inventario");
        doc.Add("Inv: Btn Expand",        "newui_expansion_btn.tga",  574,   391,  36,  29, null, "Inventario");
        doc.Add("Inv: Btn Reward",        "newui_expansion_btn.tga",  611,   391,  36,  29, null, "Inventario");
        doc.Add("Ventana Inventario",     "newui_item_back01.ozt",    450,     0, 190, 429, null, "Inventario");

        // Personaje
        doc.Add("Cha: Fondo",             "newui_msgbox_back.jpg",    450,     0, 190, 429, null, "Personaje");
        doc.Add("Cha: Top",               "newui_item_back04.tga",    450,     0, 190,  64, null, "Personaje");
        doc.Add("Cha: Left",              "newui_item_back02-L.tga",  450,    64,  21, 320, null, "Personaje");
        doc.Add("Cha: Right",             "newui_item_back02-R.tga",  619,    64,  21, 320, null, "Personaje");
        doc.Add("Cha: Bottom",            "newui_item_back03.tga",    450,   384, 190,  45, null, "Personaje");
        doc.Add("Cha: Stat Box 1",        "newui_cha_textbox02.tga",  462,   120, 170,  19, null, "Personaje");
        doc.Add("Cha: Stat Box 2",        "newui_cha_textbox02.tga",  462,   175, 170,  19, null, "Personaje");
        doc.Add("Cha: Stat Box 3",        "newui_cha_textbox02.tga",  462,   240, 170,  19, null, "Personaje");
        doc.Add("Cha: Stat Box 4",        "newui_cha_textbox02.tga",  462,   295, 170,  19, null, "Personaje");
        doc.Add("Cha: Stat Box 5",        "newui_cha_textbox02.tga",  462,   350, 170,  19, null, "Personaje");
        doc.Add("Cha: Btn Stat 1",        "newui_chainfo_btn_level.tga", 610, 122, 16,  15, null, "Personaje");
        doc.Add("Cha: Btn Stat 2",        "newui_chainfo_btn_level.tga", 610, 177, 16,  15, null, "Personaje");
        doc.Add("Cha: Btn Stat 3",        "newui_chainfo_btn_level.tga", 610, 242, 16,  15, null, "Personaje");
        doc.Add("Cha: Btn Stat 4",        "newui_chainfo_btn_level.tga", 610, 297, 16,  15, null, "Personaje");
        doc.Add("Cha: Btn Stat 5",        "newui_chainfo_btn_level.tga", 610, 352, 16,  15, null, "Personaje");
        doc.Add("Cha: Btn Exit",          "newui_exit_00.tga",        463,   392,  36,  29, null, "Personaje");
        doc.Add("Cha: Btn Quest",         "newui_chainfo_btn_quest.tga", 500,  392,  36,  29, null, "Personaje");
        doc.Add("Cha: Btn Pet",           "newui_chainfo_btn_pet.tga",   537,  392,  36,  29, null, "Personaje");
        doc.Add("Cha: Btn Master",        "newui_chainfo_btn_master.tga", 574, 392,  36,  29, null, "Personaje");
        doc.Add("Cha: Btn Reward",        "newui_chainfo_btn_quest.tga", 611,  392,  36,  29, null, "Personaje");
        doc.Add("Ventana Personaje",      "newui_item_back01.ozt",    450,     0, 190, 429, null, "Personaje");

        // Amigos
        doc.Add("Ventana Amigos",         "MenuS2_friend.OZJ",        390,   259, 250, 173, null, "Amigos");

        // Pantalla de Carga (Dimensiones matching g_HudLayout 800x600)
        doc.Add("Carga: Loading Part 1",  "LSBg01.OZJ",                 0,     0, 400, 512, null, "Carga");
        doc.Add("Carga: Loading Part 2",  "LSBg02.OZJ",               400,     0, 400, 512, null, "Carga");
        doc.Add("Carga: Loading Part 3",  "LSBg03.OZJ",                 0,   512, 400,  88, null, "Carga");
        doc.Add("Carga: Loading Part 4",  "LSBg04.OZJ",               400,   512, 400,  88, null, "Carga");



        for (int i = 0; i < doc.Elements.Count; i++)
            doc.Elements[i].ZOrder = i;

        doc.ClassifyElements();
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
        
        // Sincronizar con defaults para asegurar que las decoraciones y carga existan
        var def = CreateDefault();
        foreach (var defEl in def.Elements)
        {
            // Mezclamos si:
            // 1) Es una decoración (tiene :)
            // 2) Pertenece a la categoría Carga (para que siempre se vea el fondo)
            bool shouldMerge = defEl.Label.Contains(":") || defEl.Category == "Carga";
            
            if (shouldMerge && !doc.Elements.Any(e => e.Label == defEl.Label))
            {
                doc.Elements.Add(defEl.Clone());
            }
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
        var name = el.ResolvePath;
        var fullPath = Path.Combine(baseFolder, name);
        var dir = Path.GetDirectoryName(fullPath) ?? baseFolder;
        var fname = Path.GetFileNameWithoutExtension(name);
        foreach (var ext in new[] { ".ozj", ".ozt", ".jpg", ".jpeg", ".tga" })
        {
            var p = Path.Combine(dir, fname + ext);
            if (File.Exists(p)) return p;
        }
        return Path.Combine(dir, fname + ".ozj");
    }
}
