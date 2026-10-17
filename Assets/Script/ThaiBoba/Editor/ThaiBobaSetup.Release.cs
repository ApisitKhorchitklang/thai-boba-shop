using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Release: scene report, player settings + icons, itch.io store art, Windows build.
public static partial class ThaiBobaSetup
{
    const string BrandingDir = "Assets/Branding/";
    public const string ProductName = "Thai Boba Shop";
    public const string CompanyName = "Hoy Studio";
    public const string Version = "1.1.1";
    static string MarketingDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Marketing/itch"));
    static string BuildDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds"));

    [MenuItem("Tools/Thai Boba Shop/Release/Report Scene")]
    public static void ReportScene()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder("[Release] scene roots:\n");
        foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            int count = g.GetComponentsInChildren<Transform>(true).Length;
            string comps = string.Join(",", g.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
            sb.AppendLine($"  {(g.activeSelf ? "+" : "-")} {g.name}  ({count} objects)  [{comps}]");
        }
        int missing = 0;
        foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (Component c in g.GetComponents<Component>()) if (c == null) { missing++; sb.AppendLine("  missing script on " + PathOf(g.transform)); }
        sb.AppendLine($"  missing scripts: {missing}");
        sb.AppendLine("  build scenes: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => (s.enabled ? "" : "(off) ") + s.path)));
        sb.AppendLine($"  product '{PlayerSettings.productName}' company '{PlayerSettings.companyName}' version '{PlayerSettings.bundleVersion}'");
        Debug.Log(sb.ToString());
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    // ---------------------------------------------------------------- player settings
    [MenuItem("Tools/Thai Boba Shop/Release/Apply Player Settings + Icons")]
    public static void ApplyPlayerSettings()
    {
        PlayerSettings.productName = ProductName;
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.hoystudio.thaibobashop");
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.hoystudio.thaibobashop");
        PlayerSettings.Android.bundleVersionCode = 4;

        // icons: rounded for desktop, full-bleed square for Android
        Texture2D rounded = IconTexture("icon_rounded_1024.png");
        Texture2D square = IconTexture("icon_square_1024.png");
        if (rounded != null)
        {
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { rounded }, IconKind.Any);
            int n = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any).Length;
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, Enumerable.Repeat(rounded, n).ToArray(), IconKind.Any);
        }
        if (square != null)
        {
            int n = PlayerSettings.GetIconSizes(NamedBuildTarget.Android, IconKind.Any).Length;
            if (n > 0) PlayerSettings.SetIcons(NamedBuildTarget.Android, Enumerable.Repeat(square, n).ToArray(), IconKind.Any);
        }

        // desktop window
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.visibleInBackground = false;
        PlayerSettings.allowFullscreenSwitch = true;
        // phones: landscape only
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // splash: cream background with the cup logo (Unity's own logo stays on Personal licences)
        PlayerSettings.SplashScreen.backgroundColor = new Color(1f, 0.957f, 0.89f);
        PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.DarkOnLight;
        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
        Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "logo_cup.png");
        if (logo != null)
        {
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, logo) };
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
        }

        // the game boots straight into the soi (main menu lives in it)
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log($"[Release] player settings: {PlayerSettings.productName} {PlayerSettings.bundleVersion} by {PlayerSettings.companyName}; build scenes = {ScenePath}");
    }

    static Texture2D IconTexture(string file)
    {
        string path = BrandingDir + file;
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("missing icon " + path); return null; }
        bool dirty = false;
        if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
        if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
        if (ti.npotScale != TextureImporterNPOTScale.None) { ti.npotScale = TextureImporterNPOTScale.None; dirty = true; }
        if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
        if (dirty) ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------------------------------------------------------- itch.io store art
    [MenuItem("Tools/Thai Boba Shop/Release/Render Store Art (itch.io)")]
    public static void RenderStoreArt()
    {
        EditorSceneManager.OpenScene(ScenePath);
        EnsureUIFonts();
        Directory.CreateDirectory(MarketingDir);
        Directory.CreateDirectory(Path.Combine(MarketingDir, "screenshots"));

        Camera cam = Camera.main;
        GameMenus menus = Object.FindFirstObjectByType<GameMenus>();
        if (menus != null && menus.menuCamera != null) menus.menuCamera.gameObject.SetActive(false);
        foreach (Canvas cv in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) cv.enabled = false;   // no HUD / menu in key art
        cam.transform.SetParent(null, true);
        float fov = cam.fieldOfView;

        // golden hour: warm sun, string lights + shop windows lit
        DayNightCycle dn = Object.FindFirstObjectByType<DayNightCycle>();
        NightLights nl = Object.FindFirstObjectByType<NightLights>();
        ShopNightGlow glow = Object.FindFirstObjectByType<ShopNightGlow>();
        Material sky = RenderSettings.skybox != null ? new Material(RenderSettings.skybox) : null;
        Material oldSky = RenderSettings.skybox;
        if (dn != null) { RenderSettings.skybox = sky; dn.SetSkyMaterial(sky); dn.Apply(17.2f); }
        // the street is dry: puddles are only shown by WetSurfaces at runtime when it rains
        WetSurfaces wet = Object.FindFirstObjectByType<WetSurfaces>();
        if (wet != null && wet.puddles != null) foreach (Transform pd in wet.puddles) if (pd != null) pd.gameObject.SetActive(false);
        if (nl != null) foreach (Light l in nl.lights) l.enabled = true;
        if (glow != null) glow.Preview(true);

        var temp = new List<GameObject>();
        StageCrowd(temp);

        // 1) key art 1920x1080 (also the itch page banner source)
        RenderComposite(cam, 1920, 1080, new Vector3(-5.6f, 2.35f, 10.6f), new Vector3(0.6f, 1.75f, 0.2f), 50f,
                        Path.Combine(MarketingDir, "keyart_1920x1080.png"), wide: true);
        // 2) itch cover 630x500 (rendered at 2x, then downsampled)
        RenderComposite(cam, 1260, 1000, new Vector3(-3.9f, 2.1f, 8.6f), new Vector3(0.5f, 1.9f, 0.2f), 58f,
                        Path.Combine(MarketingDir, "cover_630x500.png"), wide: false, outW: 630, outH: 500);

        foreach (GameObject g in temp) Object.DestroyImmediate(g);
        if (glow != null) glow.RestoreOriginals();
        if (nl != null) foreach (Light l in nl.lights) l.enabled = false;
        if (dn != null) { RenderSettings.skybox = oldSky; dn.SetSkyMaterial(null); Object.DestroyImmediate(sky); }
        cam.fieldOfView = fov;

        // icon + screenshots from the regular scene captures
        foreach (string f in new[] { "icon_square_512.png", "icon_rounded_512.png", "icon_square_1024.png" })
            File.Copy(Path.Combine(Application.dataPath, "Branding", f), Path.Combine(MarketingDir, f), true);
        string renders = Path.GetFullPath(Path.Combine(Application.dataPath, "../BlenderSource/renders"));
        string[] shots = { "unity_hud.png", "unity_night_street.png", "unity_rain_street.png", "unity_tutorial.png", "unity_liquid.png", "unity_powerlines.png", "unity_summary.png" };
        for (int i = 0; i < shots.Length; i++)
        {
            string src = Path.Combine(renders, shots[i]);
            if (File.Exists(src)) File.Copy(src, Path.Combine(MarketingDir, "screenshots", $"{i + 1:00}_{shots[i].Replace("unity_", "")}"), true);
        }
        Debug.Log("[Release] store art written to " + MarketingDir);
        // scene was only borrowed for rendering: do not save it
    }

    // A small crowd at the shop + a tuk-tuk passing, posed mid-walk (rigged FBX clips sampled in edit mode)
    static void StageCrowd(List<GameObject> temp)
    {
        var people = new (string model, float bx, float by, float yaw, float t)[]
        {
            ("NPC_Student", 0.15f, -1.25f, 180f, 0.0f), ("NPC_Office", 0.25f, -2.05f, 175f, 0.3f), ("NPC_Auntie", -0.1f, -2.85f, 185f, 0.6f),
            ("NPC_Tourist", -3.6f, -1.9f, 90f, 0.2f), ("NPC_Rider", 3.2f, -2.2f, -90f, 0.5f), ("NPC_Influencer", -2.0f, -11.4f, 90f, 0.4f),
            ("NPC_Grandpa", 2.4f, -11.6f, -90f, 0.1f), ("NPC_SchoolBoy", -6.5f, -2.4f, 90f, 0.7f),
        };
        foreach (var p in people)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CharDir + p.model + ".fbx");
            if (asset == null) continue;
            GameObject go = (GameObject)Object.Instantiate(asset, P(p.bx, p.by, 0), Quaternion.Euler(0, p.yaw, 0));
            SetupLods(go, 0.11f, 0.012f);
            bool queue = p.yaw > 170f && p.yaw < 190f;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(CharDir + p.model + ".fbx"))
                if (o is AnimationClip clip && clip.name == (queue ? "Idle" : "Walk")) clip.SampleAnimation(go, p.t);
            temp.Add(go);
        }
        GameObject tuk = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Traffic/Traffic_Traffic_TukTuk.prefab");
        if (tuk != null)
        {
            GameObject t = (GameObject)Object.Instantiate(tuk, P(-5.5f, -5.75f, -0.15f), Quaternion.LookRotation(Vector3.left));
            Object.DestroyImmediate(t.GetComponent<VehicleSound>());
            Object.DestroyImmediate(t.GetComponent<TrafficCar>());
            temp.Add(t);
        }
        GameObject scooter = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Traffic/Traffic_Traffic_Scooter.prefab");
        if (scooter != null)
        {
            GameObject s = (GameObject)Object.Instantiate(scooter, P(6f, -8.4f, -0.15f), Quaternion.LookRotation(Vector3.right));
            Object.DestroyImmediate(s.GetComponent<VehicleSound>());
            Object.DestroyImmediate(s.GetComponent<TrafficCar>());
            temp.Add(s);
        }
    }

    // Scene render + title overlay (logo, Thai title, English name, tagline) in one pass
    static void RenderComposite(Camera cam, int w, int h, Vector3 pos, Vector3 look, float fov, string file, bool wide, int outW = 0, int outH = 0)
    {
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(look - pos);
        cam.fieldOfView = fov;

        GameObject canvasGo = new GameObject("StoreArtCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 0.4f;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(w, h);
        scaler.matchWidthOrHeight = 1f;
        Transform root = canvasGo.transform;
        Color brown = new Color(0.24f, 0.14f, 0.08f), pink = new Color(0.95f, 0.36f, 0.54f), cream = new Color(1f, 0.957f, 0.89f);

        // soft dark gradient behind the text so the title always reads (fades out, no hard edge)
        if (wide) Wash(root, true, new Vector2(0, 0), new Vector2(0.62f, 1), 0.62f);
        else
        {
            Wash(root, false, new Vector2(0, 0.62f), new Vector2(1, 1), 0.55f);          // top, behind the title
            Wash(root, false, new Vector2(0, 0), new Vector2(1, 0.16f), 0.5f, flip: true); // bottom, behind the tagline
        }

        float s = wide ? 1f : 0.92f;
        Vector2 logoPos = wide ? new Vector2(250, 170) : new Vector2(180, -300);
        Vector2 anchor = wide ? new Vector2(0, 0.5f) : new Vector2(0, 1f);
        Img(root, "Logo", "logo_cup", anchor, new Vector2(0.5f, 0.5f), logoPos, new Vector2(400, 400) * s);

        TMP_Text Title(string name, string txt, float size, Color fill, Color outline, float ow, Vector2 at, Vector2 box, TMP_FontAsset font)
        {
            TMP_Text t = Label(root, name, txt, size, fill, wide ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Midline, anchor,
                               wide ? new Vector2(0, 0.5f) : new Vector2(0.5f, 0.5f), at, box, true);
            Object.DestroyImmediate(t.GetComponent<LocalizedText>());
            t.font = font;
            t.enableWordWrapping = false;
            t.fontMaterial = new Material(t.fontMaterial);
            t.fontMaterial.EnableKeyword("OUTLINE_ON");
            t.outlineColor = outline;
            t.outlineWidth = ow;
            t.fontMaterial.EnableKeyword("UNDERLAY_ON");
            t.fontMaterial.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.5f));
            t.fontMaterial.SetFloat("_UnderlayOffsetX", 0.6f);
            t.fontMaterial.SetFloat("_UnderlayOffsetY", -0.6f);
            t.fontMaterial.SetFloat("_UnderlaySoftness", 0.3f);
            return t;
        }
        if (wide)
        {
            Title("TitleTH", "บ้านชานมไข่มุก", 150, pink, cream, 0.22f, new Vector2(70, -120), new Vector2(1100, 210), titleFont);
            Title("TitleEN", "THAI BOBA SHOP", 84, cream, brown, 0.28f, new Vector2(80, -265), new Vector2(1000, 110), titleFont);
            Title("Tag", "เปิดร้านชานมในซอยกรุงเทพฯ  ·  Run a bubble tea shop in a Bangkok soi", 34, Color.white, brown, 0.3f,
                  new Vector2(84, -350), new Vector2(1400, 60), uiFont);
        }
        else
        {
            Title("TitleTH", "บ้านชานมไข่มุก", 132, pink, cream, 0.24f, new Vector2(630 + 70, -140), new Vector2(1100, 190), titleFont);
            Title("TitleEN", "THAI BOBA SHOP", 74, cream, brown, 0.3f, new Vector2(630 + 70, -275), new Vector2(1000, 100), titleFont);
            Title("Tag", "Run a bubble tea shop in a Bangkok soi", 36, Color.white, brown, 0.3f, new Vector2(630, -935), new Vector2(1200, 60), uiFont);
        }
        Canvas.ForceUpdateCanvases();

        RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
        int ow2 = outW > 0 ? outW : w, oh2 = outH > 0 ? outH : h;
        RenderTexture outRt = rt;
        if (ow2 != w || oh2 != h)
        {
            outRt = new RenderTexture(ow2, oh2, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(rt, outRt);
        }
        RenderTexture.active = outRt;
        Texture2D tex = new Texture2D(ow2, oh2, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, ow2, oh2), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        if (ow2 != w) File.WriteAllBytes(Path.ChangeExtension(file, null) + "@2x.png", ReadRt(rt, w, h));
        RenderTexture.active = null;
        Object.DestroyImmediate(canvasGo);
        Object.DestroyImmediate(tex);
        if (outRt != rt) outRt.Release();
        rt.Release();
    }

    static void Wash(Transform root, bool horizontal, Vector2 min, Vector2 max, float alpha, bool flip = false)
    {
        const int N = 256;
        Texture2D t = new Texture2D(horizontal ? N : 1, horizontal ? 1 : N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int i = 0; i < N; i++)
        {
            float k = i / (N - 1f);                                   // 0 = text side
            float a = alpha * Mathf.SmoothStep(1f, 0f, k);
            Color c = new Color(0.12f, 0.06f, 0.03f, a);
            if (horizontal) t.SetPixel(i, 0, c);
            else t.SetPixel(0, flip ? i : N - 1 - i, c);              // vertical: dark at the top (or bottom when flipped)
        }
        t.Apply();
        GameObject go = new GameObject("Wash", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(root, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
        RawImage img = go.GetComponent<RawImage>();
        img.texture = t;
        img.raycastTarget = false;
    }

    static byte[] ReadRt(RenderTexture rt, int w, int h)
    {
        RenderTexture.active = rt;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGB24, false);
        t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        t.Apply();
        byte[] png = t.EncodeToPNG();
        Object.DestroyImmediate(t);
        return png;
    }

    // ---------------------------------------------------------------- build
    [MenuItem("Tools/Thai Boba Shop/Release/Build Windows (x64)")]
    public static void BuildWindows()
    {
        ApplyPlayerSettings();
        string dir = Path.Combine(BuildDir, "Windows");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = Path.Combine(dir, "ThaiBobaShop.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[Release] Windows build: {report.summary.result}, {report.summary.totalSize / (1024f * 1024f):0.0} MB, {report.summary.totalErrors} errors, {report.summary.totalTime}");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // ---------------------------------------------------------------- tidy hierarchy
    // Roots stay roots (GameManager / InventoryManager use DontDestroyOnLoad, which needs a root object):
    // they are only sorted into sections under EditorOnly header objects, which the build strips out.
    static readonly (string header, string[] roots)[] Sections =
    {
        ("SYSTEMS", new[] { "GameManager", "ThaiBobaSystems", "GameAudio", "GraphicsQuality", "EventSystem" }),
        ("LIGHT & WORLD", new[] { "Sun (TimeCyc)", "PostFX Volume", "ThaiBobaStreet" }),
        ("PLAYER & PEOPLE", new[] { "Player", "MenuCamera", "CustomerSpawner", "FarSidePedestrians", "LivingSoi" }),
        ("UI", new[] { "ThaiBobaHUD", "ThaiBobaMenus" }),
    };

    public static void TidyHierarchy()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects().ToDictionary(g => g.name, g => g);
        foreach (GameObject g in roots.Values.Where(g => g.name.StartsWith("── ")).ToList()) Object.DestroyImmediate(g);
        int index = 0;
        foreach (var (header, names) in Sections)
        {
            GameObject h = new GameObject("── " + header + " ──") { tag = "EditorOnly" };
            h.transform.SetSiblingIndex(index++);
            foreach (string n in names)
                if (roots.TryGetValue(n, out GameObject g) && g != null) g.transform.SetSiblingIndex(index++);
        }
        // anything new and unsorted goes to the end, where it is easy to spot
    }

    // Run with -buildTarget Android so Unity switches platform before this is called
    [MenuItem("Tools/Thai Boba Shop/Release/Build Android (APK)")]
    public static void BuildAndroid()
    {
        ApplyPlayerSettings();
        EditorUserBuildSettings.buildAppBundle = false;                       // .apk for itch.io sideloading
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        string dir = Path.Combine(BuildDir, "Android");
        Directory.CreateDirectory(dir);
        string apk = Path.Combine(dir, $"ThaiBobaShop_v{Version}.apk");
        if (File.Exists(apk)) File.Delete(apk);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = apk,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });
        Debug.Log($"[Release] Android build: {report.summary.result}, {report.summary.totalSize / (1024f * 1024f):0.0} MB, {report.summary.totalErrors} errors, {report.summary.totalTime}");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // batch entry: settings + store art in one Unity run
    public static void PrepareRelease()
    {
        ApplyPlayerSettings();
        ReportScene();
        RenderStoreArt();
    }
}
