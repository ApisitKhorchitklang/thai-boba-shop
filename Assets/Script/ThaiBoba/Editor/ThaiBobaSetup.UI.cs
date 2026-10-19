using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// HUD + menus built from the Illustrator UI kit (ArtSource/UI/make_ui_kit.jsx -> Assets/UI/Sprites).
public static partial class ThaiBobaSetup
{
    const string SpriteDir = "Assets/UI/Sprites/";
    static readonly Color Brown = new Color32(0x4A, 0x2C, 0x1A, 255);
    static readonly Color Cream = new Color32(0xFF, 0xF4, 0xE3, 255);
    static readonly Color Pink = new Color32(0xF2, 0x5C, 0x8A, 255);
    static readonly Color Green = new Color32(0x4E, 0x8A, 0x3C, 255);

    static TMP_FontAsset uiFont, titleFont;
    public static TMP_Text lastClockText, lastDayText;

    // ------------------------------------------------------------------ assets
    static readonly Dictionary<string, Vector4> NineSlice = new Dictionary<string, Vector4>
    {
        { "panel", new Vector4(60, 64, 60, 60) }, { "panel_dark", new Vector4(60, 60, 60, 60) }, { "chip", new Vector4(46, 48, 46, 44) },
        { "button_pink", new Vector4(56, 60, 56, 52) }, { "button_green", new Vector4(56, 60, 56, 52) },
        { "button_orange", new Vector4(56, 60, 56, 52) }, { "button_cream", new Vector4(56, 60, 56, 52) },
        { "button_blue", new Vector4(56, 60, 56, 52) },
    };

    public static void EnsureUISprites()
    {
        foreach (string f in Directory.GetFiles(SpriteDir, "*.png"))
        {
            string path = f.Replace('\\', '/');
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            string name = Path.GetFileNameWithoutExtension(path);
            Vector4 border = NineSlice.TryGetValue(name, out Vector4 b) ? b : Vector4.zero;
            if (ti.textureType == TextureImporterType.Sprite && ti.spriteBorder == border) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.spriteBorder = border;
            ti.SaveAndReimport();
        }
    }

    // Fixed UI text: Thai -> English (LocalizedText switches them at runtime)
    static readonly Dictionary<string, string> EN = new Dictionary<string, string>
    {
        ["ภารกิจวันนี้"] = "Today's goals",
        ["WASD เดิน · เมาส์ มอง\nคลิกซ้าย/E ใช้ · คลิกขวา/Q โยน · Esc หยุด"] = "WASD walk · Mouse look\nLeft-click/E use · Right-click/Q throw · Esc pause",
        ["ใช้"] = "Use",
        ["โยน"] = "Throw",
        ["สั่งของสำหรับพรุ่งนี้"] = "Restock for tomorrow",
        ["แก้ว +10"] = "Cups +10",
        ["ไข่มุก +50"] = "Pearls +50",
        ["น้ำแข็ง +50"] = "Ice +50",
        ["ชา +50"] = "Tea +50",
        ["อัปเกรดร้าน"] = "Shop upgrades",
        ["เปิดร้านวันถัดไป"] = "Open the next day",
        ["เกมจะบันทึกอัตโนมัติทุกวัน"] = "The game saves automatically every day",
        ["บ้านชานมไข่มุก"] = "Thai Boba Shop",
        ["Thai Boba Shop · เกมเปิดร้านชานมในซอยไทย"] = "Run your own bubble tea shop in a Thai soi",
        ["โหลดวันที่บันทึกไว้"] = "Load a saved day",
        ["ตั้งค่า"] = "Settings",
        ["ออกจากเกม"] = "Quit",
        ["v1.0 · เสียง CC0 จาก Kenney / OpenGameArt / BigSoundBank · ฟอนต์ Kanit & Sarabun (OFL)"] =
            "v1.0 · CC0 sounds from Kenney / OpenGameArt / BigSoundBank · Fonts Kanit & Sarabun (OFL)",
        ["หยุดเกม"] = "Paused",
        ["เล่นต่อ"] = "Continue",
        ["บันทึกเกม"] = "Save game",
        ["เล่นบทสอนอีกครั้ง"] = "Replay tutorial",
        ["กลับเมนูหลัก"] = "Main menu",
        ["เสียงทั้งหมด"] = "Master volume",
        ["เพลง"] = "Music",
        ["เอฟเฟกต์"] = "Effects",
        ["ความไวเมาส์"] = "Mouse sensitivity",
        ["ขนาด UI"] = "UI size",
        ["กลับ"] = "Back",
        ["ท้าทายรายวัน"] = "Daily challenge",
        ["สั่งของ · คอมพิวเตอร์ร้าน"] = "Order supplies · Shop PC",
        ["สินค้าส่งถึงร้านในไม่กี่วินาที · เงินหักทันทีที่สั่ง"] = "Deliveries arrive in a few seconds · paid when you order",
        ["ยังไม่มีเซฟ · เกมจะบันทึกอัตโนมัติตอนจบแต่ละวัน"] = "No saves yet · the game saves automatically at the end of each day",
        ["สถิติ & เหรียญรางวัล"] = "Stats & medals",
        ["สถิติ & เหรียญ"] = "Stats & medals",
        ["เนื้อเรื่อง"] = "Story",
        ["ไปกันเลย!"] = "Let's go!",
        ["หมุน"] = "Turn",
        ["เสร็จ"] = "Done",
    };

    static void Localize(TMP_Text t)
    {
        if (t != null && EN.TryGetValue(t.text, out string en))
        {
            LocalizedText lt = t.gameObject.AddComponent<LocalizedText>();
            lt.th = t.text;
            lt.en = en;
        }
    }

    // Labels whose text the game rewrites itself (order card, summary title, settings values...)
    static void Unlocalize(TMP_Text t)
    {
        if (t == null) return;
        LocalizedText lt = t.GetComponent<LocalizedText>();
        if (lt != null) Object.DestroyImmediate(lt);
    }

    static Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + name + ".png");

    static TMP_FontAsset FontAsset(string ttf)
    {
        string assetPath = Path.ChangeExtension(ttf, null) + " SDF.asset";
        TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fa != null) return fa;
        Font font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        return font != null ? CreateFontAsset(font, assetPath) : null;
    }

    static void EnsureUIFonts()
    {
        EnsureThaiFont();   // Sarabun fallback for anything not covered
        uiFont = FontAsset("Assets/Fonts/Thai/Kanit-Medium.ttf") ?? EnsureThaiFont();
        titleFont = FontAsset("Assets/Fonts/Thai/Kanit-Bold.ttf") ?? uiFont;
    }

    // ------------------------------------------------------------------ widget helpers
    static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretch(Transform parent, string name)
    {
        RectTransform rt = Rect(parent, name, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Image Img(Transform parent, string name, string sprite, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                     Color? color = null, bool raycast = false)
    {
        RectTransform rt = Rect(parent, name, anchor, pivot, pos, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite != null ? S(sprite) : null;
        img.type = sprite != null && NineSlice.ContainsKey(sprite) ? Image.Type.Sliced : Image.Type.Simple;
        img.preserveAspect = img.type == Image.Type.Simple && sprite != null;
        img.color = color ?? Color.white;
        img.raycastTarget = raycast;
        return img;
    }

    static TMP_Text Label(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align,
                          Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, bool title = false)
    {
        RectTransform rt = Rect(parent, name, anchor, pivot, pos, box);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = title ? titleFont : uiFont;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.text = text;
        t.raycastTarget = false;
        t.enableWordWrapping = true;
        ThaiText.Enable(t);
        Localize(t);
        return t;
    }

    // Text filling its parent with padding
    static TMP_Text FillLabel(Transform parent, string text, float size, Color color, TextAlignmentOptions align,
                              float padL = 0, float padR = 0, bool title = false)
    {
        RectTransform rt = Stretch(parent, "Label");
        rt.offsetMin = new Vector2(padL, 0);
        rt.offsetMax = new Vector2(-padR, 0);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = title ? titleFont : uiFont;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.text = text;
        t.raycastTarget = false;
        ThaiText.Enable(t);
        Localize(t);
        return t;
    }

    static Button Btn(Transform parent, string name, string sprite, string label, string icon, Vector2 anchor, Vector2 pos,
                      Vector2 size, float fontSize, Color textColor, UnityAction onClick = null)
    {
        Image bg = Img(parent, name, sprite, anchor, new Vector2(0.5f, 0.5f), pos, size, null, true);
        Button b = bg.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1f, 0.95f, 0.95f);
        cb.pressedColor = new Color(0.85f, 0.8f, 0.8f);
        cb.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        b.colors = cb;
        float pad = 0;
        if (icon != null)
        {
            float s = Mathf.Min(size.y * 0.62f, 72f);
            Img(bg.transform, "Icon", icon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(size.y * 0.32f, 4), new Vector2(s, s));
            pad = size.y * 0.32f + s;
        }
        if (label != null)
            FillLabel(bg.transform, label, fontSize, textColor, icon != null ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Midline,
                      pad + 14, 20).margin = new Vector4(0, 0, 0, 8);
        if (onClick != null) UnityEventTools.AddPersistentListener(b.onClick, onClick);
        return b;
    }

    // Pill with an icon and a value text
    static TMP_Text Chip(Transform parent, string name, string sprite, string icon, Vector2 anchor, Vector2 pivot, Vector2 pos,
                         Vector2 size, float fontSize, Color textColor, out RectTransform root)
    {
        Image bg = Img(parent, name, sprite, anchor, pivot, pos, size);
        root = (RectTransform)bg.transform;
        float s = size.y * 0.72f;
        if (icon != null)
            Img(bg.transform, "Icon", icon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(size.y * 0.18f, 3), new Vector2(s, s));
        TMP_Text t = FillLabel(bg.transform, "", fontSize, textColor, TextAlignmentOptions.MidlineLeft, icon != null ? s + size.y * 0.32f : 24, 16);
        t.margin = new Vector4(0, 0, 0, 6);
        return t;
    }

    static Slider MakeSlider(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform root = Rect(parent, "Slider", anchor, new Vector2(0.5f, 0.5f), pos, size);
        Slider sl = root.gameObject.AddComponent<Slider>();
        Img(root, "Background", "panel_dark", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x, 26), null, true);
        RectTransform fillArea = Stretch(root, "Fill Area");
        fillArea.offsetMin = new Vector2(12, size.y / 2 - 13);
        fillArea.offsetMax = new Vector2(-12, -(size.y / 2 - 13));
        Image fill = Img(fillArea, "Fill", "chip", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Pink);
        RectTransform frt = (RectTransform)fill.transform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0, 1); frt.offsetMin = frt.offsetMax = Vector2.zero;
        RectTransform handleArea = Stretch(root, "Handle Slide Area");
        handleArea.offsetMin = new Vector2(20, 0);
        handleArea.offsetMax = new Vector2(-20, 0);
        Image handle = Img(handleArea, "Handle", "joystick_knob", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56), null, true);
        RectTransform hrt = (RectTransform)handle.transform;
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = new Vector2(0, 1); hrt.sizeDelta = new Vector2(56, 0);
        sl.fillRect = frt;
        sl.handleRect = hrt;
        sl.targetGraphic = handle;
        sl.minValue = 0f;
        sl.maxValue = 1f;
        return sl;
    }

    static GameObject MakeCanvas(string name, int order)
    {
        GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        go.GetComponent<Canvas>().sortingOrder = order;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return go;
    }

    // ------------------------------------------------------------------ HUD
    [MenuItem("Tools/Thai Boba Shop/3. Create HUD Canvas")]
    public static ShopHUD CreateHUD()
    {
        EnsureUISprites();
        EnsureUIFonts();
        GameObject canvasGo = MakeCanvas("ThaiBobaHUD", 0);
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Thai Boba HUD");
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        ShopHUD hud = canvasGo.AddComponent<ShopHUD>();
        RectTransform game = Stretch(canvasGo.transform, "Gameplay");
        Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), TC = new Vector2(0.5f, 1), BC = new Vector2(0.5f, 0),
                MC = new Vector2(0.5f, 0.5f), ML = new Vector2(0, 0.5f), MR = new Vector2(1, 0.5f), BR = new Vector2(1, 0);

        AddTouchControls(game);

        // top-left stats
        RectTransform r;
        hud.moneyText = Chip(game, "Money", "chip", "icon_coin", TL, TL, new Vector2(24, -20), new Vector2(300, 86), 40, Brown, out r);
        TMP_Text rep = Chip(game, "Reputation", "chip", null, TL, TL, new Vector2(24, -112), new Vector2(300, 70), 28, Brown, out r);
        rep.alignment = TextAlignmentOptions.MidlineRight;
        hud.reputationText = rep;
        hud.reputationStars = new Image[5];
        for (int i = 0; i < 5; i++)
            hud.reputationStars[i] = Img(r, "Star" + i, "icon_star", ML, ML, new Vector2(22 + i * 40, 2), new Vector2(42, 42));
        hud.levelText = Chip(game, "Level", "chip", "icon_levelup", TL, TL, new Vector2(24, -188), new Vector2(300, 70), 24, Brown, out r);
        hud.levelText.alignment = TextAlignmentOptions.TopLeft;
        hud.levelText.margin = new Vector4(0, 10, 0, 0);
        Img(r, "XpBack", "panel_dark", new Vector2(0, 0), new Vector2(0, 0), new Vector2(76, 14), new Vector2(196, 16));
        hud.xpFill = Img(r, "XpFill", "chip", new Vector2(0, 0), new Vector2(0, 0), new Vector2(78, 16), new Vector2(192, 12), new Color32(0x7B, 0xB6, 0x61, 255));
        hud.xpFill.type = Image.Type.Filled;
        hud.xpFill.fillMethod = Image.FillMethod.Horizontal;
        hud.comboText = Chip(game, "Combo", "button_orange", "icon_fire", TL, TL, new Vector2(24, -266), new Vector2(260, 76), 30, Color.white, out r);
        hud.comboRoot = r.gameObject;

        // top-centre daily event banner
        Image banner = Img(game, "EventBanner", "panel", TC, TC, new Vector2(0, -16), new Vector2(680, 104));
        hud.eventIcon = Img(banner.transform, "Icon", "icon_sun", ML, ML, new Vector2(22, 0), new Vector2(70, 70));
        hud.eventText = FillLabel(banner.transform, "", 30, Brown, TextAlignmentOptions.MidlineLeft, 104, 24, true);
        hud.eventText.lineSpacing = -20;
        hud.sunIcon = S("icon_sun"); hud.rainIcon = S("icon_rain"); hud.festivalIcon = S("icon_shop");

        // top-right clock, day and pause
        Button pause = Btn(game, "Pause", "round_cream", null, "icon_pause", TR, new Vector2(-72, -68), new Vector2(104, 104), 0, Brown);
        pause.transform.Find("Icon").GetComponent<RectTransform>().anchoredPosition = new Vector2(20, 4);
        hud.gameObject.AddComponent<HUDPauseLink>().button = pause;
        lastClockText = Chip(game, "Clock", "chip", "icon_clock", TR, TR, new Vector2(-140, -20), new Vector2(230, 86), 40, Brown, out r);
        lastDayText = Chip(game, "Day", "chip", "icon_calendar", TR, TR, new Vector2(-140, -112), new Vector2(230, 70), 28, Brown, out r);
        hud.weatherText = Chip(game, "Weather", "chip", "icon_sun", TR, TR, new Vector2(-24, -188), new Vector2(346, 70), 24, Brown, out r);
        hud.weatherIcon = r.Find("Icon").GetComponent<Image>();
        hud.clearIcon = S("icon_sun"); hud.cloudIcon = S("icon_cloud"); hud.rainSprite = S("icon_rain");
        hud.stormIcon = S("icon_storm"); hud.coolIcon = S("icon_cool");

        // right: current order card
        Image order = Img(game, "OrderCard", "panel", MR, MR, new Vector2(-20, 90), new Vector2(460, 250));
        Img(order.transform, "Icon", "icon_cup", TL, TL, new Vector2(26, -20), new Vector2(64, 64));
        hud.orderTitle = Label(order.transform, "Title", "ออเดอร์", 32, Pink, TextAlignmentOptions.MidlineLeft, TL, TL, new Vector2(100, -24), new Vector2(330, 56), true);
        hud.orderText = Label(order.transform, "Order", "รอลูกค้า...", 26, Brown, TextAlignmentOptions.TopLeft, TL, TL, new Vector2(30, -92), new Vector2(400, 100));
        Img(order.transform, "PatienceBack", "panel_dark", BC, BC, new Vector2(0, 26), new Vector2(400, 24));
        hud.patienceFill = Img(order.transform, "PatienceFill", "chip", BC, BC, new Vector2(0, 30), new Vector2(392, 16));
        hud.patienceFill.type = Image.Type.Filled;
        hud.patienceFill.fillMethod = Image.FillMethod.Horizontal;

        // right, under the order: the delivery app ticket (shown while one is open)
        Image dcard = Img(game, "DeliveryCard", "panel", MR, MR, new Vector2(-20, -140), new Vector2(460, 190));
        Img(dcard.transform, "Icon", "icon_clock", TL, TL, new Vector2(22, -16), new Vector2(52, 52));
        TMP_Text dTitle = Label(dcard.transform, "Title", "", 25, Pink, TextAlignmentOptions.MidlineLeft, TL, TL, new Vector2(84, -16), new Vector2(360, 52), true);
        TMP_Text dItems = Label(dcard.transform, "Items", "", 18, Brown, TextAlignmentOptions.TopLeft, TL, TL, new Vector2(24, -70), new Vector2(420, 84));
        dItems.enableAutoSizing = true;
        dItems.fontSizeMin = 13;
        dItems.fontSizeMax = 18;
        Img(dcard.transform, "TimerBack", "panel_dark", BC, BC, new Vector2(0, 16), new Vector2(400, 20));
        Image dFill = Img(dcard.transform, "TimerFill", "chip", BC, BC, new Vector2(0, 19), new Vector2(392, 14));
        dFill.type = Image.Type.Filled;
        dFill.fillMethod = Image.FillMethod.Horizontal;
        DeliverySystem delivery = Object.FindFirstObjectByType<DeliverySystem>();
        if (delivery != null) { delivery.card = dcard.gameObject; delivery.cardTitle = dTitle; delivery.cardItems = dItems; delivery.timerFill = dFill; }
        dcard.gameObject.SetActive(false);

        // left, under the goals: the story chapter (tap to open the story)
        StoryMode story = Object.FindFirstObjectByType<StoryMode>();
        Button storyChip = Btn(game, "StoryChip", "chip", "", "icon_star", ML, new Vector2(255, -122), new Vector2(470, 62), 22, Brown,
                               story != null ? (UnityAction)story.ShowPanel : null);
        if (story != null) story.hudChip = storyChip.GetComponentInChildren<TMP_Text>();

        // arrange mode: hint bar (top) + turn / done buttons for touch
        Image arrange = Img(game, "ArrangeBar", "panel_dark", TC, TC, new Vector2(0, -132), new Vector2(1180, 72));
        TMP_Text arrangeText = FillLabel(arrange.transform, "", 24, Color.white, TextAlignmentOptions.Midline, 20, 20);
        RectTransform arrangeTouch = Stretch(game, "ArrangeTouch");
        DesktopPlayerInput pin = Object.FindFirstObjectByType<DesktopPlayerInput>();
        Btn(arrangeTouch, "Turn", "round_blue", "หมุน", null, BR, new Vector2(-150, 420), new Vector2(150, 150), 32, Color.white, pin != null ? (UnityAction)pin.RotateButton : null);
        Btn(arrangeTouch, "Done", "button_green", "เสร็จ", null, BR, new Vector2(-400, 300), new Vector2(200, 90), 32, Color.white, pin != null ? (UnityAction)pin.DoneArrangingButton : null);
        DecorPlacement placement = Object.FindFirstObjectByType<DecorPlacement>();
        if (placement != null) { placement.hintBar = arrange.gameObject; placement.hintText = arrangeText; placement.touchButtons = arrangeTouch.gameObject; }
        arrange.gameObject.SetActive(false);
        arrangeTouch.gameObject.SetActive(false);

        // left: daily goals card
        Image goals = Img(game, "GoalsCard", "panel", ML, ML, new Vector2(20, 30), new Vector2(470, 230));
        Img(goals.transform, "Icon", "icon_trophy", TL, TL, new Vector2(24, -18), new Vector2(60, 60));
        Label(goals.transform, "Title", "ภารกิจวันนี้", 30, Pink, TextAlignmentOptions.MidlineLeft, TL, TL, new Vector2(96, -22), new Vector2(340, 52), true);
        hud.goalsText = Label(goals.transform, "Goals", "", 22, Brown, TextAlignmentOptions.TopLeft, TL, TL, new Vector2(28, -84), new Vector2(420, 136));

        // bottom: stock chips
        string[] icons = { "icon_cup", "icon_boba", "icon_ice", "icon_tea", "icon_foam" };
        TMP_Text[] stock = new TMP_Text[5];
        for (int i = 0; i < 5; i++)
            stock[i] = Chip(game, "Stock" + i, "chip", icons[i], BC, BC, new Vector2(-360 + i * 180, 20), new Vector2(170, 70), 28, Brown, out r);
        hud.cupsText = stock[0]; hud.bobaText = stock[1]; hud.iceText = stock[2]; hud.teaText = stock[3]; hud.foamText = stock[4];

        // centre: crosshair, prompt, held cup, toast
        Img(game, "Crosshair", "round_cream", MC, MC, Vector2.zero, new Vector2(18, 18));
        hud.promptText = Chip(game, "Prompt", "panel_dark", null, MC, MC, new Vector2(0, -78), new Vector2(720, 64), 28, Color.white, out r);
        hud.promptText.alignment = TextAlignmentOptions.Midline;
        hud.promptRoot = r.gameObject;
        hud.heldCupText = Chip(game, "HeldCup", "panel_dark", "icon_cup", BC, BC, new Vector2(0, 104), new Vector2(1180, 60), 23, Color.white, out r);
        hud.heldCupRoot = r.gameObject;
        hud.toastText = Chip(game, "Toast", "button_pink", null, MC, MC, new Vector2(0, 200), new Vector2(980, 90), 32, Color.white, out r);
        hud.toastText.alignment = TextAlignmentOptions.Midline;
        hud.toastRoot = r;

        // first-day tutorial card (top centre, under the event banner) + glowing arrow over the next station
        Image tut = Img(game, "TutorialCard", "panel", TC, TC, new Vector2(0, -126), new Vector2(940, 160));
        Image badge = Img(tut.transform, "StepBadge", "round_pink", ML, ML, new Vector2(20, 0), new Vector2(112, 112));
        TutorialSystem tutorial = hud.gameObject.AddComponent<TutorialSystem>();
        tutorial.panel = tut.gameObject;
        tutorial.stepText = FillLabel(badge.transform, "1/9", 30, Color.white, TextAlignmentOptions.Midline, 4, 4, true);
        tutorial.titleText = Label(tut.transform, "Title", "", 30, Pink, TextAlignmentOptions.MidlineLeft, TL, TL, new Vector2(150, -14), new Vector2(600, 54), true);
        tutorial.bodyText = Label(tut.transform, "Body", "", 23, Brown, TextAlignmentOptions.TopLeft, TL, TL, new Vector2(150, -68), new Vector2(560, 84));
        tutorial.skipButton = Btn(tut.transform, "Skip", "button_cream", "ข้าม", null, MR, new Vector2(-96, 0), new Vector2(164, 70), 26, Brown);
        tutorial.finishButton = Btn(tut.transform, "Finish", "button_pink", "เริ่มขายเลย!", null, MR, new Vector2(-122, 0), new Vector2(216, 84), 26, Color.white);
        tutorial.arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "SM_TutorialArrow.fbx");
        Unlocalize(tutorial.skipButton.GetComponentInChildren<TMP_Text>());
        Unlocalize(tutorial.finishButton.GetComponentInChildren<TMP_Text>());
        tut.gameObject.SetActive(false);

        // shop computer: order supplies / decorate & expand (opened from SM_OrderComputer)
        Image op = Img(game, "OrderPanel", "panel", MC, MC, new Vector2(0, 0), new Vector2(1000, 840), null, true);
        Image ob = Img(op.transform, "Ribbon", "button_pink", new Vector2(0.5f, 1), MC, new Vector2(0, -6), new Vector2(620, 100));
        FillLabel(ob.transform, "สั่งของ · คอมพิวเตอร์ร้าน", 36, Color.white, TextAlignmentOptions.Midline, 10, 10, true).margin = new Vector4(0, 0, 0, 10);
        OrderPanel orderPc = hud.gameObject.AddComponent<OrderPanel>();
        orderPc.panel = op.gameObject;
        orderPc.supplyTab = Btn(op.transform, "TabSupply", "button_cream", "", null, new Vector2(0.5f, 1), new Vector2(-312, -112), new Vector2(296, 62), 23, Brown);
        orderPc.decorTab = Btn(op.transform, "TabDecor", "button_cream", "", null, new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(296, 62), 23, Brown);
        orderPc.staffTab = Btn(op.transform, "TabStaff", "button_cream", "", null, new Vector2(0.5f, 1), new Vector2(312, -112), new Vector2(296, 62), 23, Brown);

        RectTransform sg = Stretch(op.transform, "SupplyGroup");
        orderPc.supplyGroup = sg.gameObject;
        orderPc.stockText = Label(sg, "Stock", "", 25, Brown, TextAlignmentOptions.Midline, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f),
                                  new Vector2(0, -180), new Vector2(940, 56));
        string[] oIcons = { "icon_cup", "icon_boba", "icon_ice", "icon_tea", "icon_foam" };
        for (int i = 0; i < 5; i++)
        {
            Vector2 at = i < 4 ? new Vector2(-225 + (i % 2) * 450, -280 - (i / 2) * 122) : new Vector2(0, -524);   // 2 x 2 + cheese foam centred
            Button b = Btn(sg, "Order" + i, i < 4 ? "button_green" : "button_orange", "", oIcons[i], new Vector2(0.5f, 1),
                           at, new Vector2(430, 106), 32, Color.white);
            orderPc.orderButtons[i] = b;
            orderPc.orderLabels[i] = b.GetComponentInChildren<TMP_Text>();
        }
        Label(sg, "Note", "สินค้าส่งถึงร้านในไม่กี่วินาที · เงินหักทันทีที่สั่ง", 22, new Color(0.45f, 0.3f, 0.2f), TextAlignmentOptions.Midline,
              new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(900, 40));

        RectTransform dg = Stretch(op.transform, "DecorGroup");
        orderPc.decorGroup = dg.gameObject;
        orderPc.beautyText = Label(dg, "Beauty", "", 25, Brown, TextAlignmentOptions.Midline, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f),
                                   new Vector2(0, -180), new Vector2(940, 56));
        for (int i = 0; i < 8; i++)
        {
            string look = i == 7 ? "button_blue" : i == 6 ? "button_orange" : "button_green";   // expansion, then "arrange the shop"
            Button b = Btn(dg, "Decor" + i, look, "", null, new Vector2(0.5f, 1),
                           new Vector2(-232 + (i % 2) * 464, -262 - (i / 2) * 104), new Vector2(448, 96), 25, Color.white);
            TMP_Text t = b.GetComponentInChildren<TMP_Text>();
            t.lineSpacing = -18;
            orderPc.decorButtons[i] = b;
            orderPc.decorLabels[i] = t;
        }
        dg.gameObject.SetActive(false);

        RectTransform stg = Stretch(op.transform, "StaffGroup");
        orderPc.staffGroup = stg.gameObject;
        orderPc.staffInfo = Label(stg, "Info", "", 25, Brown, TextAlignmentOptions.Midline, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f),
                                  new Vector2(0, -180), new Vector2(940, 56));
        for (int i = 0; i < 5; i++)
        {
            string look = i == 3 ? "button_blue" : i == 4 ? "button_pink" : i == 2 ? "button_orange" : "button_green";
            Vector2 at = i < 4 ? new Vector2(-232 + (i % 2) * 464, -270 - (i / 2) * 118) : new Vector2(0, -506);
            Button b = Btn(stg, "Staff" + i, look, "", null, new Vector2(0.5f, 1), at, new Vector2(i < 4 ? 448 : 560, 106), 24, Color.white);
            TMP_Text t = b.GetComponentInChildren<TMP_Text>();
            t.lineSpacing = -18;
            orderPc.staffButtons[i] = b;
            orderPc.staffLabels[i] = t;
        }
        stg.gameObject.SetActive(false);

        orderPc.closeButton = Btn(op.transform, "Close", "button_pink", "ปิด", null, new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(300, 88), 32, Color.white);
        orderPc.closeLabel = orderPc.closeButton.GetComponentInChildren<TMP_Text>();
        op.gameObject.SetActive(false);

        // PC key hints
        Image hints = Img(game, "PCHints", "panel_dark", BR, BR, new Vector2(-20, 20), new Vector2(560, 120));
        FillLabel(hints.transform, "WASD เดิน · เมาส์ มอง\nคลิกซ้าย/E ใช้ · คลิกขวา/Q โยน · Esc หยุด", 22, Color.white,
                  TextAlignmentOptions.Midline, 16, 16);
        DesktopPlayerInput pcInput = Object.FindFirstObjectByType<DesktopPlayerInput>();
        if (pcInput != null) pcInput.pcHintsRoot = hints.gameObject;

        BuildSummaryPanel(canvasGo.transform, hud);
        return hud;
    }

    static void AddTouchControls(Transform game)
    {
        DesktopPlayerInput input = Object.FindFirstObjectByType<DesktopPlayerInput>();
        MovementWithJoy move = Object.FindFirstObjectByType<MovementWithJoy>();
        RectTransform touch = Stretch(game, "TouchControls");

        Image lookImg = Img(touch, "LookArea (drag to look)", null, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(1, 1, 1, 0), true);
        RectTransform lrt = (RectTransform)lookImg.transform;
        lrt.anchorMin = new Vector2(0.35f, 0f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        TouchScreenCamera tsc = lookImg.gameObject.AddComponent<TouchScreenCamera>();

        GameObject joyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Joystick Pack/Prefabs/Fixed Joystick.prefab");
        if (joyPrefab != null)
        {
            GameObject joy = (GameObject)PrefabUtility.InstantiatePrefab(joyPrefab, touch);
            RectTransform jrt = (RectTransform)joy.transform;
            jrt.anchorMin = jrt.anchorMax = Vector2.zero;
            jrt.pivot = new Vector2(0.5f, 0.5f);
            jrt.anchoredPosition = new Vector2(230, 230);
            jrt.sizeDelta = new Vector2(300, 300);
            Image[] imgs = joy.GetComponentsInChildren<Image>(true);
            if (imgs.Length > 0) { imgs[0].sprite = S("joystick_bg"); imgs[0].color = Color.white; }
            if (imgs.Length > 1) { imgs[1].sprite = S("joystick_knob"); imgs[1].color = Color.white; }
            if (move != null) move.joystick = joy.GetComponent<Joystick>();
        }

        Button use = Btn(touch, "UseButton", "round_pink", "ใช้", null, new Vector2(1, 0), new Vector2(-150, 190), new Vector2(220, 220), 52, Color.white);
        Img(use.transform, "Icon", "icon_cup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 46), new Vector2(64, 64));
        ((RectTransform)use.transform.Find("Label")).offsetMin = new Vector2(0, -60);
        Button toss = Btn(touch, "ThrowButton", "round_blue", "โยน", null, new Vector2(1, 0), new Vector2(-390, 110), new Vector2(150, 150), 34, Color.white);
        if (input != null)
        {
            input.touchLook = tsc;
            input.touchControlsRoot = touch.gameObject;
            UnityEventTools.AddPersistentListener(use.onClick, input.InteractButton);
            UnityEventTools.AddPersistentListener(toss.onClick, input.ThrowButton);
        }
    }

    static void BuildSummaryPanel(Transform canvas, ShopHUD hud)
    {
        Image dim = Img(canvas, "SummaryPanel", null, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.1f, 0.06f, 0.04f, 0.55f), true);
        RectTransform drt = (RectTransform)dim.transform;
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
        hud.summaryPanel = dim.gameObject;
        Image panel = Img(dim.transform, "Card", "panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1560, 900));
        Image ribbon = Img(panel.transform, "Ribbon", "button_pink", new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(820, 110));
        hud.summaryTitle = FillLabel(ribbon.transform, "ปิดร้าน", 40, Color.white, TextAlignmentOptions.Midline, 20, 20, true);
        hud.summaryTitle.margin = new Vector4(0, 0, 0, 10);

        Img(panel.transform, "CoinIcon", "icon_coin", new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -90), new Vector2(80, 80));
        hud.summaryText = Label(panel.transform, "Summary", "", 28, Brown, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1),
                                new Vector2(60, -180), new Vector2(660, 560));
        hud.summaryText.enableAutoSizing = true;                          // long days (regulars, stock, insights) shrink instead of running under the button
        hud.summaryText.fontSizeMin = 16;
        hud.summaryText.fontSizeMax = 28;

        // supply orders
        Label(panel.transform, "SupplyTitle", "สั่งของสำหรับพรุ่งนี้", 30, Pink, TextAlignmentOptions.MidlineLeft, new Vector2(1, 1), new Vector2(1, 1),
              new Vector2(-60, -90), new Vector2(760, 50), true);
        ComputerTerminal terminal = Object.FindFirstObjectByType<ComputerTerminal>();
        UpgradeShop upgrades = Object.FindFirstObjectByType<UpgradeShop>();
        string[] labels = { "แก้ว +10", "ไข่มุก +50", "น้ำแข็ง +50", "ชา +50" };
        string[] icons = { "icon_cup", "icon_boba", "icon_ice", "icon_tea" };
        for (int i = 0; i < 4; i++)
        {
            UnityAction act = terminal == null ? null
                : i == 0 ? (UnityAction)terminal.OrderCups10 : i == 1 ? terminal.OrderBoba50 : i == 2 ? (UnityAction)terminal.OrderIce50 : terminal.OrderSyrup50;
            Button b = Btn(panel.transform, "Supply" + i, "button_green", labels[i], icons[i], new Vector2(1, 1),
                           new Vector2(-610 + (i % 2) * 380, -180 - (i / 2) * 96), new Vector2(360, 88), 28, Color.white, act);
            b.gameObject.AddComponent<UIClickSound>();
        }
        Label(panel.transform, "UpgradeTitle", "อัปเกรดร้าน", 30, Pink, TextAlignmentOptions.MidlineLeft, new Vector2(1, 1), new Vector2(1, 1),
              new Vector2(-60, -390), new Vector2(760, 50), true);
        if (upgrades != null)
        {
            for (int i = 0; i < upgrades.upgrades.Count; i++)
            {
                UpgradeShop.Upgrade u = upgrades.upgrades[i];
                Button b = Btn(panel.transform, "Upgrade_" + u.id, "button_orange", $"{u.nameTH}  {u.price}฿", null, new Vector2(1, 1),
                               new Vector2(-610 + (i % 2) * 380, -470 - (i / 2) * 78), new Vector2(370, 72), 22, Color.white);
                UnityEventTools.AddStringPersistentListener(b.onClick, upgrades.BuyUpgrade, u.id);
                LocalizedText lt = b.GetComponentInChildren<TMP_Text>().gameObject.AddComponent<LocalizedText>();
                lt.th = $"{u.nameTH}  {u.price}฿";
                lt.en = $"{u.nameEN ?? u.nameTH}  {u.price}฿";
                b.gameObject.AddComponent<UIClickSound>();
            }
        }
        hud.upgradesText = null;
        Btn(panel.transform, "NextDay", "button_pink", "เปิดร้านวันถัดไป", "icon_play", new Vector2(0, 0), new Vector2(360, 80),
            new Vector2(580, 112), 38, Color.white, hud.NextDayButton);
        Label(panel.transform, "AutoSaveNote", "เกมจะบันทึกอัตโนมัติทุกวัน", 22, new Color(0.45f, 0.3f, 0.2f), TextAlignmentOptions.MidlineRight,
              new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 40), new Vector2(600, 40));
        dim.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ menus
    public static GameMenus CreateMenus(ShopHUD hud, Camera playerCam, GameObject player)
    {
        EnsureUISprites();
        EnsureUIFonts();
        GameObject canvasGo = MakeCanvas("ThaiBobaMenus", 10);
        GameMenus menus = canvasGo.AddComponent<GameMenus>();
        Vector2 C = new Vector2(0.5f, 0.5f);

        GameObject Overlay(string name, float alpha)
        {
            Image dim = Img(canvasGo.transform, name, null, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.12f, 0.07f, 0.04f, alpha), true);
            RectTransform rt = (RectTransform)dim.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return dim.gameObject;
        }

        // ----- main menu
        GameObject main = Overlay("MainMenu", 0.3f);
        Image logo = Img(main.transform, "Logo", "logo_cup", new Vector2(0.27f, 0.5f), C, new Vector2(0, 120), new Vector2(420, 420));
        Label(main.transform, "TitleShadow", "บ้านชานมไข่มุก", 104, new Color(0.24f, 0.14f, 0.08f, 0.9f), TextAlignmentOptions.Midline,
              new Vector2(0.27f, 0.5f), C, new Vector2(5, -175), new Vector2(900, 150), true);
        Label(main.transform, "Title", "บ้านชานมไข่มุก", 104, Pink, TextAlignmentOptions.Midline, new Vector2(0.27f, 0.5f), C, new Vector2(0, -168), new Vector2(900, 150), true);
        Label(main.transform, "Subtitle", "Thai Boba Shop · เกมเปิดร้านชานมในซอยไทย", 32, Cream, TextAlignmentOptions.Midline,
              new Vector2(0.27f, 0.5f), C, new Vector2(0, -262), new Vector2(900, 60));
        Image card = Img(main.transform, "Buttons", "panel", new Vector2(0.73f, 0.5f), C, Vector2.zero, new Vector2(640, 858));
        Button cont = Btn(card.transform, "Continue", "button_pink", "เล่นต่อ", "icon_play", new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(540, 100), 34, Color.white, menus.ContinueButton);
        Button newG = Btn(card.transform, "NewGame", "button_cream", "เริ่มเกมใหม่", "icon_shop", new Vector2(0.5f, 1), new Vector2(0, -206), new Vector2(540, 94), 32, Brown, menus.NewGameButton);
        Btn(card.transform, "Challenge", "button_green", "ท้าทายรายวัน", "icon_trophy", new Vector2(0.5f, 1), new Vector2(0, -312), new Vector2(540, 94), 32, Color.white, menus.ChallengeButton);
        Btn(card.transform, "Load", "button_cream", "โหลดวันที่บันทึกไว้", "icon_calendar", new Vector2(0.5f, 1), new Vector2(0, -418), new Vector2(540, 94), 30, Brown, menus.OpenLoad);
        Btn(card.transform, "Stats", "button_cream", "สถิติ & เหรียญ", "icon_star", new Vector2(0.5f, 1), new Vector2(0, -524), new Vector2(540, 94), 32, Brown, menus.OpenStats);
        Btn(card.transform, "Settings", "button_cream", "ตั้งค่า", "icon_gear", new Vector2(0.5f, 1), new Vector2(0, -630), new Vector2(540, 94), 32, Brown, menus.OpenSettings);
        Btn(card.transform, "Quit", "button_orange", "ออกจากเกม", "icon_home", new Vector2(0.5f, 1), new Vector2(0, -736), new Vector2(540, 94), 32, Color.white, menus.QuitButton);
        menus.continueButton = cont;
        menus.continueLabel = cont.GetComponentInChildren<TMP_Text>();
        menus.newGameLabel = newG.GetComponentInChildren<TMP_Text>();
        Unlocalize(menus.continueLabel);                         // shows the saved day: set by GameMenus
        // quick language switch, top-left of the title screen
        menus.mainLanguageLabel = Btn(main.transform, "Language", "button_cream", "English", null, new Vector2(0, 1), new Vector2(150, -70),
                                      new Vector2(240, 76), 28, Brown, menus.CycleLanguage).GetComponentInChildren<TMP_Text>();
        menus.versionText = Label(main.transform, "Credits", "v1.0 · เสียง CC0 จาก Kenney / OpenGameArt / BigSoundBank · ฟอนต์ Kanit & Sarabun (OFL)",
                                  20, new Color(1, 1, 1, 0.75f), TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 16), new Vector2(1200, 40));

        // ----- pause
        GameObject pause = Overlay("PauseMenu", 0.55f);
        Image pcard = Img(pause.transform, "Card", "panel", C, C, Vector2.zero, new Vector2(640, 1000));
        Image pr = Img(pcard.transform, "Ribbon", "button_pink", new Vector2(0.5f, 1), C, new Vector2(0, -6), new Vector2(460, 100));
        FillLabel(pr.transform, "หยุดเกม", 40, Color.white, TextAlignmentOptions.Midline, 10, 10, true).margin = new Vector4(0, 0, 0, 10);
        Btn(pcard.transform, "Resume", "button_pink", "เล่นต่อ", "icon_play", new Vector2(0.5f, 1), new Vector2(0, -135), new Vector2(520, 92), 32, Color.white, menus.ResumeButton);
        Btn(pcard.transform, "Save", "button_green", "บันทึกเกม", "icon_save", new Vector2(0.5f, 1), new Vector2(0, -239), new Vector2(520, 92), 32, Color.white, menus.SaveButton);
        Btn(pcard.transform, "Story", "button_cream", "เนื้อเรื่อง", "icon_star", new Vector2(0.5f, 1), new Vector2(0, -343), new Vector2(520, 92), 32, Brown, menus.StoryButton);
        Btn(pcard.transform, "Stats", "button_cream", "สถิติ & เหรียญ", "icon_trophy", new Vector2(0.5f, 1), new Vector2(0, -447), new Vector2(520, 92), 32, Brown, menus.OpenStats);
        Btn(pcard.transform, "Load", "button_cream", "โหลดวันที่บันทึกไว้", "icon_calendar", new Vector2(0.5f, 1), new Vector2(0, -551), new Vector2(520, 92), 29, Brown, menus.OpenLoad);
        Btn(pcard.transform, "Settings", "button_cream", "ตั้งค่า", "icon_gear", new Vector2(0.5f, 1), new Vector2(0, -655), new Vector2(520, 92), 32, Brown, menus.OpenSettings);
        Btn(pcard.transform, "Tutorial", "button_cream", "เล่นบทสอนอีกครั้ง", "icon_play", new Vector2(0.5f, 1), new Vector2(0, -759), new Vector2(520, 92), 29, Brown, menus.ReplayTutorialButton);
        Btn(pcard.transform, "MainMenu", "button_orange", "กลับเมนูหลัก", "icon_home", new Vector2(0.5f, 1), new Vector2(0, -863), new Vector2(520, 92), 32, Color.white, menus.MainMenuButton);

        // ----- settings
        GameObject settings = Overlay("SettingsMenu", 0.6f);
        Image scard = Img(settings.transform, "Card", "panel", C, C, Vector2.zero, new Vector2(980, 1060));
        Image sr = Img(scard.transform, "Ribbon", "button_pink", new Vector2(0.5f, 1), C, new Vector2(0, -6), new Vector2(460, 100));
        FillLabel(sr.transform, "ตั้งค่า", 40, Color.white, TextAlignmentOptions.Midline, 10, 10, true).margin = new Vector4(0, 0, 0, 10);
        Slider SliderRow(string label, string icon, float y)
        {
            Img(scard.transform, label + "Icon", icon, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(60, y), new Vector2(64, 64));
            Label(scard.transform, label, label, 30, Brown, TextAlignmentOptions.MidlineLeft, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(140, y), new Vector2(300, 60));
            return MakeSlider(scard.transform, new Vector2(1, 1), new Vector2(-280, y), new Vector2(420, 60));
        }
        menus.masterSlider = SliderRow("เสียงทั้งหมด", "icon_sound", -150);
        menus.musicSlider = SliderRow("เพลง", "icon_music", -240);
        menus.sfxSlider = SliderRow("เอฟเฟกต์", "icon_boba", -330);
        menus.mouseSlider = SliderRow("ความไวเมาส์", "icon_gear", -420);
        menus.mouseSlider.minValue = 0.2f;
        menus.mouseSlider.maxValue = 3f;
        menus.uiScaleSlider = SliderRow("ขนาด UI", "icon_star", -510);
        menus.uiScaleSlider.minValue = GameSettings.UIScaleMin;
        menus.uiScaleSlider.maxValue = GameSettings.UIScaleMax;
        menus.controlsLabel = Btn(scard.transform, "Controls", "button_cream", "การควบคุม", null, new Vector2(0.5f, 1), new Vector2(0, -605), new Vector2(760, 84), 28, Brown, menus.CycleControls).GetComponentInChildren<TMP_Text>();
        menus.qualityLabel = Btn(scard.transform, "Quality", "button_cream", "กราฟิก", null, new Vector2(0.5f, 1), new Vector2(-195, -700), new Vector2(370, 84), 28, Brown, menus.CycleQuality).GetComponentInChildren<TMP_Text>();
        Button fs = Btn(scard.transform, "Fullscreen", "button_cream", "เต็มจอ", null, new Vector2(0.5f, 1), new Vector2(195, -700), new Vector2(370, 84), 28, Brown, menus.ToggleFullscreen);
        menus.fullscreenLabel = fs.GetComponentInChildren<TMP_Text>();
        menus.fullscreenRow = fs.gameObject;
        menus.languageLabel = Btn(scard.transform, "Language", "button_cream", "ภาษา", null, new Vector2(0.5f, 1), new Vector2(0, -795),
                                  new Vector2(760, 84), 28, Brown, menus.CycleLanguage).GetComponentInChildren<TMP_Text>();
        Btn(scard.transform, "Back", "button_pink", "กลับ", "icon_home", new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(360, 100), 34, Color.white, menus.Back);

        // ----- load days
        GameObject load = Overlay("LoadMenu", 0.6f);
        Image lcard = Img(load.transform, "Card", "panel", C, C, Vector2.zero, new Vector2(1100, 900));
        Image lr = Img(lcard.transform, "Ribbon", "button_pink", new Vector2(0.5f, 1), C, new Vector2(0, -6), new Vector2(620, 100));
        FillLabel(lr.transform, "โหลดวันที่บันทึกไว้", 38, Color.white, TextAlignmentOptions.Midline, 10, 10, true).margin = new Vector4(0, 0, 0, 10);
        RectTransform list = Rect(lcard.transform, "List", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(980, 660));
        VerticalLayoutGroup vl = list.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 10;
        vl.childControlHeight = vl.childControlWidth = false;
        vl.childAlignment = TextAnchor.UpperCenter;
        Button row = Btn(list, "RowTemplate", "button_cream", "วันที่ 1", "icon_calendar", new Vector2(0.5f, 1), Vector2.zero, new Vector2(960, 74), 24, Brown);
        menus.loadListRoot = list;
        menus.loadRowTemplate = row.gameObject;
        menus.loadEmptyText = Label(lcard.transform, "Empty", "ยังไม่มีเซฟ · เกมจะบันทึกอัตโนมัติตอนจบแต่ละวัน", 28, Brown,
                                    TextAlignmentOptions.Midline, C, C, new Vector2(0, 40), new Vector2(900, 80));
        Btn(lcard.transform, "Back", "button_pink", "กลับ", "icon_home", new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(360, 100), 34, Color.white, menus.Back);

        // ----- stats & medals
        GameObject stats = Overlay("StatsMenu", 0.6f);
        Image tcard = Img(stats.transform, "Card", "panel", C, C, Vector2.zero, new Vector2(1640, 960));
        Image trib = Img(tcard.transform, "Ribbon", "button_pink", new Vector2(0.5f, 1), C, new Vector2(0, -6), new Vector2(640, 100));
        FillLabel(trib.transform, "สถิติ & เหรียญรางวัล", 38, Color.white, TextAlignmentOptions.Midline, 10, 10, true).margin = new Vector4(0, 0, 0, 10);
        menus.statsText = Label(tcard.transform, "Stats", "", 25, Brown, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -120), new Vector2(620, 700));
        menus.statsText.enableAutoSizing = true;
        menus.statsText.fontSizeMin = 16;
        menus.statsText.fontSizeMax = 25;
        menus.medalCountText = Label(tcard.transform, "MedalCount", "", 30, Pink, TextAlignmentOptions.MidlineLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(720, -100), new Vector2(860, 56), true);
        RectTransform grid = Rect(tcard.transform, "MedalGrid", new Vector2(0, 1), new Vector2(0, 1), new Vector2(720, -160), new Vector2(880, 690));
        GridLayoutGroup gl = grid.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(212, 128);
        gl.spacing = new Vector2(10, 10);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 4;
        Image cell = Img(grid, "MedalTemplate", "button_cream", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(212, 128));
        Img(cell.transform, "Icon", "icon_star", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(54, 54));
        TMP_Text cellLabel = Label(cell.transform, "Label", "", 17, Brown, TextAlignmentOptions.Top, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(200, 62));
        cellLabel.enableAutoSizing = true;
        cellLabel.fontSizeMin = 11;
        cellLabel.fontSizeMax = 17;
        menus.medalGrid = grid;
        menus.medalTemplate = cell.gameObject;
        var iconNames = new List<string>();
        foreach (StatsSystem.Medal m in StatsSystem.Medals) if (!iconNames.Contains(m.icon)) iconNames.Add(m.icon);
        menus.medalIconNames = iconNames.ToArray();
        menus.medalIconSprites = iconNames.ConvertAll(n => S(n)).ToArray();
        Btn(tcard.transform, "Back", "button_pink", "กลับ", "icon_home", new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(360, 100), 34, Color.white, menus.Back);
        menus.statsPanel = stats;

        // ----- story popup (StoryMode)
        GameObject storyP = Overlay("StoryPanel", 0.6f);
        Image scard2 = Img(storyP.transform, "Card", "panel", C, C, Vector2.zero, new Vector2(1240, 880));
        Image srib = Img(scard2.transform, "Ribbon", "button_pink", new Vector2(0.5f, 1), C, new Vector2(0, -6), new Vector2(560, 100));
        TMP_Text sChapter = FillLabel(srib.transform, "", 34, Color.white, TextAlignmentOptions.Midline, 10, 10, true);
        sChapter.margin = new Vector4(0, 0, 0, 10);
        TMP_Text sTitle = Label(scard2.transform, "Title", "", 40, Pink, TextAlignmentOptions.Midline, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(1100, 70), true);
        TMP_Text sBody = Label(scard2.transform, "Body", "", 26, Brown, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(1120, 400));
        sBody.enableAutoSizing = true;
        sBody.fontSizeMin = 17;
        sBody.fontSizeMax = 26;
        TMP_Text sGoals = Label(scard2.transform, "Goals", "", 26, Brown, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -590), new Vector2(1120, 150));
        sGoals.enableAutoSizing = true;
        sGoals.fontSizeMin = 17;
        sGoals.fontSizeMax = 26;
        Button sGo = Btn(scard2.transform, "Continue", "button_pink", "ไปกันเลย!", "icon_play", new Vector2(0.5f, 0), new Vector2(0, 66), new Vector2(380, 96), 34, Color.white);
        foreach (TMP_Text t in new[] { sChapter, sTitle, sBody, sGoals }) Unlocalize(t);
        StoryMode storyMode = Object.FindFirstObjectByType<StoryMode>();
        if (storyMode != null)
        {
            storyMode.panel = storyP;
            storyMode.chapterText = sChapter;
            storyMode.titleText = sTitle;
            storyMode.bodyText = sBody;
            storyMode.goalsText = sGoals;
            storyMode.continueButton = sGo;
        }
        storyP.SetActive(false);

        menus.mainMenu = main;
        menus.pausePanel = pause;
        menus.settingsPanel = settings;
        menus.loadPanel = load;
        pause.SetActive(false);
        settings.SetActive(false);
        load.SetActive(false);
        stats.SetActive(false);
        menus.gameplayUI = new[] { hud.transform.Find("Gameplay").gameObject };

        // cinematic camera behind the main menu
        GameObject mc = new GameObject("MenuCamera");
        Camera menuCam = mc.AddComponent<Camera>();
        menuCam.fieldOfView = 55f;
        mc.AddComponent<AudioListener>();
        mc.AddComponent<MenuCamera>();
        mc.transform.position = new Vector3(-5f, 5.2f, 9.5f);
        mc.transform.LookAt(new Vector3(-1.5f, 2f, 0f));
        UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(menuCam).renderPostProcessing = true;
        menus.menuCamera = menuCam;
        menus.playerCamera = playerCam;
        menus.playerControls = new MonoBehaviour[]
        {
            player.GetComponent<MovementWithJoy>(), player.GetComponent<DesktopPlayerInput>(),
            playerCam.GetComponent<CameraMobile>(), playerCam.GetComponent<RaycastAim>()
        };
        HUDPauseLink link = hud.GetComponent<HUDPauseLink>();
        if (link != null && link.button != null) UnityEventTools.AddPersistentListener(link.button.onClick, menus.TogglePause);
        return menus;
    }
}
