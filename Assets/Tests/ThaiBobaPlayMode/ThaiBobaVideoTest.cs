using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Footage for the project presentation video: camera moves through the real game at a fixed 30 fps
// (Time.captureFramerate), every frame written as a JPG to BlenderSource/renders/video/<shot>/f_0000.jpg.
// Run WITHOUT -nographics:  -runTests -testPlatform PlayMode -testFilter ThaiBobaVideoTest
public class ThaiBobaVideoTest
{
    const int Fps = 30, W = 1920, H = 1080;
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    static Type T(string name) => Type.GetType(name + ", Assembly-CSharp");
    static Component Find(string type) => (Component)UnityEngine.Object.FindFirstObjectByType(T(type));
    static Component Inst(string type) => (Component)T(type).GetProperty("Instance").GetValue(null);
    static Vector3 P(float bx, float by, float bz) => new Vector3(-bx, bz, -by);       // Blender -> Unity

    static object Get(object target, string member)
    {
        Type t = target as Type ?? target.GetType();
        object inst = target is Type ? null : target;
        FieldInfo f = t.GetField(member, Any);
        return f != null ? f.GetValue(inst) : t.GetProperty(member, Any).GetValue(inst);
    }

    static object Call(object target, string method, params object[] args)
    {
        Type t = target as Type ?? target.GetType();
        object inst = target is Type ? null : target;
        foreach (MethodInfo m in t.GetMethods(Any))
            if (m.Name == method && m.GetParameters().Length == args.Length) return m.Invoke(inst, args);
        throw new MissingMethodException(t.Name, method);
    }

    // the player's saves are put aside, like the other tests
    static string SavePath => Path.Combine(Application.persistentDataPath, "bubble_tea_save.json");
    static string SlotDir => Path.Combine(Application.persistentDataPath, "saves");
    static string BackupDir => Path.Combine(Application.persistentDataPath, "saves_video_backup");
    // kept on disk (not in memory) so even a killed run leaves the player's save recoverable
    static string SaveBackup => Path.Combine(Application.persistentDataPath, "bubble_tea_save.video_backup.json");
    int tutorialPref;

    [SetUp]
    public void BackupSave()
    {
        RestoreLeftovers();                                                  // an earlier run that never reached TearDown
        if (File.Exists(SavePath)) File.Move(SavePath, SaveBackup);
        if (Directory.Exists(SlotDir)) Directory.Move(SlotDir, BackupDir);
        tutorialPref = PlayerPrefs.GetInt("tb_tutorial_done", 0);
        PlayerPrefs.SetInt("tb_tutorial_done", 1);
    }

    [TearDown]
    public void RestoreSave()
    {
        Time.captureFramerate = 0;
        Time.timeScale = 1f;
        // SetUp moved the player's files aside, so anything here now was written by this run
        if (!File.Exists(SaveBackup) && File.Exists(SavePath)) File.Delete(SavePath);
        if (!Directory.Exists(BackupDir) && Directory.Exists(SlotDir)) Directory.Delete(SlotDir, true);
        RestoreLeftovers();
        PlayerPrefs.SetInt("tb_tutorial_done", tutorialPref);
    }

    static void RestoreLeftovers()
    {
        if (File.Exists(SaveBackup))
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(SaveBackup, SavePath);
        }
        if (Directory.Exists(BackupDir))
        {
            if (Directory.Exists(SlotDir)) Directory.Delete(SlotDir, true);
            Directory.Move(BackupDir, SlotDir);
        }
    }

    Camera shot;
    RenderTexture rt;
    Texture2D tex;
    string root;
    readonly List<string> log = new List<string>();

    static float Ease(float t) => t * t * (3f - 2f * t);

    // Records seconds of footage. path(t, 0..1) -> (camera position, look-at point); cam = null: the shot camera
    IEnumerator Record(string name, float seconds, Func<float, (Vector3, Vector3)> path, Camera cam = null, bool ui = false,
                       Action<float> each = null)
    {
        string only = Environment.GetEnvironmentVariable("VIDEO_SHOTS");
        if (!string.IsNullOrEmpty(only) && Array.IndexOf(only.Split(','), name) < 0) yield break;
        Camera c = cam != null ? cam : shot;
        string dir = Path.Combine(root, name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        T("PlanarReflection").GetField("ViewOverride").SetValue(null, c);
        T("RainFollow").GetField("ViewOverride").SetValue(null, c);
        var canvases = new List<(Canvas, RenderMode)>();
        if (ui)
            foreach (Canvas cv in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (cv.isRootCanvas && cv.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvases.Add((cv, cv.renderMode));
                    cv.renderMode = RenderMode.ScreenSpaceCamera;
                    cv.worldCamera = c;
                    cv.planeDistance = 0.5f;
                }
        int n = Mathf.RoundToInt(seconds * Fps);
        Time.captureFramerate = Fps;
        for (int f = 0; f < n; f++)
        {
            float t = n > 1 ? f / (float)(n - 1) : 0f;
            each?.Invoke(t);
            if (path != null)
            {
                var (pos, look) = path(t);
                c.transform.position = pos;
                c.transform.rotation = Quaternion.LookRotation(look - pos);
            }
            RenderTexture old = c.targetTexture;
            c.targetTexture = rt;
            c.Render();
            c.targetTexture = old;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, $"f_{f:0000}.jpg"), tex.EncodeToJPG(92));
            yield return null;
        }
        Time.captureFramerate = 0;
        foreach (var (cv, mode) in canvases) { cv.renderMode = mode; cv.worldCamera = null; }
        log.Add($"{name} {n}");
        Debug.Log($"[VIDEO] {name}: {n} frames");
    }

    static IEnumerator Wait(float seconds)
    {
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < seconds) yield return null;
    }

    // a straight move with easing from a to b, looking from la to lb
    static Func<float, (Vector3, Vector3)> Move(Vector3 a, Vector3 b, Vector3 la, Vector3 lb) =>
        t => (Vector3.Lerp(a, b, Ease(t)), Vector3.Lerp(la, lb, Ease(t)));

    [UnityTest, Timeout(3600000)]
    public IEnumerator PresentationFootage()
    {
        T("StoryMode").GetField("AutoPopups").SetValue(null, false);
        T("StatsSystem").GetMethod("ResetForTests").Invoke(null, null);
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/ThaiBobaStreet.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null;
        yield return null;
        Component menus = null;
        for (int i = 0; i < 120 && menus == null; i++) { menus = Inst("GameMenus"); yield return null; }
        Call(menus, "StartNewGame");
        yield return null;
        root = Path.GetFullPath(Path.Combine(Application.dataPath, "../BlenderSource/renders/video"));
        Directory.CreateDirectory(root);
        GameObject camGo = new GameObject("VideoCam");
        shot = camGo.AddComponent<Camera>();
        shot.fieldOfView = 55f;
        shot.nearClipPlane = 0.05f;
        Type urpExt = Type.GetType("UnityEngine.Rendering.Universal.CameraExtensions, Unity.RenderPipelines.Universal.Runtime");
        object camData = urpExt?.GetMethod("GetUniversalAdditionalCameraData")?.Invoke(null, new object[] { shot });
        camData?.GetType().GetProperty("renderPostProcessing")?.SetValue(camData, true);
        PropertyInfo aa = camData?.GetType().GetProperty("antialiasing");
        if (aa != null) aa.SetValue(camData, Enum.ToObject(aa.PropertyType, 2));             // SMAA
        shot.enabled = false;
        rt = new RenderTexture(W, H, 24) { antiAliasing = 4 };
        tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        Component clock = Find("TimeCyc"), weather = Find("WeatherSystem"), ev = Inst("DailyEventSystem");
        Component gm = Find("GameManager");
        gm.GetType().GetField("money").SetValue(gm, 30000);
        Call(weather, "Force", 0f, 0f, false);

        // customers gather in front of the shop before the first shots
        Call(clock, "SetTime", 14, 30);
        Time.timeScale = 3f;
        yield return Wait(10f);
        Time.timeScale = 1f;

        // 1. the soi in the afternoon
        Call(clock, "SetTime", 15, 0);
        yield return Record("s01_street", 9f, Move(P(-15f, -6.6f, 4.6f), P(-2.8f, -5.2f, 2.3f), P(0f, 0f, 2.6f), P(0.2f, 0.6f, 1.4f)));
        // 2. the shopfront with the queue (an arc in front of the counter)
        yield return Record("s02_queue", 8f, t =>
        {
            float a = Mathf.Lerp(-35f, 30f, Ease(t)) * Mathf.Deg2Rad;
            Vector3 c = P(0f, 0.2f, 1.1f);
            return (c + new Vector3(Mathf.Sin(a) * 4.4f, 0.65f, -Mathf.Cos(a) * 4.4f), c);
        });

        // 3. the player's view: the bot makes and serves drinks through the real stations (with the HUD)
        Type botT = T("ThaiBobaAutoPlayer");
        Component bot = new GameObject("AutoPlayer").AddComponent(botT);
        botT.GetField("customersToServe").SetValue(bot, 99);
        botT.GetField("endDayWhenDone").SetValue(bot, false);
        botT.GetField("speedUp").SetValue(bot, 3f);
        Component pot = Find("BobaPot");
        Call(pot, "ForceState", Enum.Parse(T("BobaPot+State"), "Fresh"), 30);
        yield return Wait(6f);                                               // first drink under way at speed
        Time.timeScale = 1f;
        botT.GetField("stepSeconds").SetValue(bot, 0.75f);
        Camera main = Camera.main;
        yield return Record("s03_brew", 24f, null, main, ui: true);
        UnityEngine.Object.Destroy(bot.gameObject);
        yield return null;
        Time.timeScale = 1f;

        // 4. afternoon to night in one shot
        yield return Record("s04_timelapse", 9f, Move(P(-8.5f, -9.6f, 3.4f), P(-7.2f, -9.0f, 3.2f), P(1.5f, 0f, 2.2f), P(1.5f, 0f, 2.0f)),
                            each: t => { int m = Mathf.RoundToInt(Mathf.Lerp(16.5f, 20.75f, t) * 60f); Call(clock, "SetTime", m / 60, m % 60); });
        // 5. the soi at night
        Call(clock, "SetTime", 21, 0);
        yield return Record("s05_night", 8f, Move(P(11f, -6.4f, 2.2f), P(3.2f, -5.6f, 1.9f), P(-2f, 0f, 2.2f), P(0f, 0.4f, 1.6f)));

        // 6. rain over the whole soi, the street reflects
        Call(clock, "SetTime", 13, 0);
        Call(weather, "Force", 1f, 1f, false);
        yield return Wait(3f);
        yield return Record("s06_rain", 9f, Move(P(-2f, -2.6f, 1.7f), P(1.5f, -2.8f, 1.8f), P(4f, -9f, 0.5f), P(-6f, -9f, 0.8f)));
        Call(weather, "Force", -1f, -1f, false);
        Call(weather, "Force", 0f, 0f, false);

        // 7. Songkran: the crowd out on the street
        Call(clock, "SetTime", 12, 0);
        Call(ev, "RollForDay", 10);
        yield return Wait(3f);
        Vector3 crowd = P(0f, -2f, 1.2f);
        Component fa = Inst("FestivalActivities");
        if (fa != null && Get(fa, "active") is System.Collections.IList groups && groups.Count > 0)
            crowd = (Vector3)Get(groups[0], "centre") + Vector3.up * 1.1f;
        yield return Record("s07_songkran", 9f, t =>
        {
            float a = Mathf.Lerp(-40f, 40f, Ease(t)) * Mathf.Deg2Rad;
            return (crowd + new Vector3(Mathf.Sin(a) * 4.2f, 1.0f, -Mathf.Cos(a) * 4.2f), crowd);
        });
        // 8. Chinese New Year evening: lanterns
        Call(ev, "RollForDay", 29);
        Call(clock, "SetTime", 18, 40);
        yield return Wait(2f);
        yield return Record("s08_cny", 7f, Move(P(-3.2f, -6.2f, 3.0f), P(-1.0f, -5.4f, 2.6f), P(1f, -0.5f, 2.6f), P(0.5f, 0f, 2.0f)));
        Call(ev, "RollForDay", 3);

        // 9-11. the expansion: the wall comes down, customers sit in the chill zone with their drinks
        Call(clock, "SetTime", 15, 30);
        Call(Inst("DecorShop"), "Buy", "expand");
        foreach (string d in new[] { "plants", "bunting", "lights", "cat", "mural" }) Call(Inst("DecorShop"), "Buy", d);
        foreach (string u in new[] { "speaker", "neon", "fan" }) try { Call(Inst("UpgradeShop"), "Buy", u); } catch (Exception) { }
        T("CustomerController").GetField("SitChance").SetValue(null, 1f);
        bot = new GameObject("AutoPlayer").AddComponent(botT);
        botT.GetField("customersToServe").SetValue(bot, 99);
        botT.GetField("endDayWhenDone").SetValue(bot, false);
        botT.GetField("speedUp").SetValue(bot, 4f);
        float t0 = Time.realtimeSinceStartup;
        while ((int)Get(T("SeatSpot"), "Sitting") < 4 && Time.realtimeSinceStartup - t0 < 240f) yield return null;
        UnityEngine.Object.Destroy(bot.gameObject);
        yield return null;
        Time.timeScale = 1f;
        Call(clock, "SetTime", 16, 0);
        yield return Record("s09_expansion", 8f, Move(P(-6.5f, -6.2f, 2.6f), P(-3.2f, -4.4f, 2.0f), P(-3.5f, 1f, 2.0f), P(-3.2f, 2.5f, 1.2f)));
        yield return Record("s10_chillzone", 9f, Move(P(-2.9f, -0.2f, 1.7f), P(-3.1f, 1.4f, 1.55f), P(-5.6f, 4.2f, 0.9f), P(-5.2f, 5.0f, 0.8f)));
        Component sitter = null;
        foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(T("CustomerController"), FindObjectsSortMode.None))
            if ((bool)Get(o, "IsSeated")) { sitter = (Component)o; break; }
        if (sitter != null)
        {
            Transform s = sitter.transform;
            Vector3 focus = s.position + Vector3.up * 0.75f;
            Vector3 room = P(-3.4f, 3.0f, 0f);                                  // middle of the chill zone: never inside a wall
            Vector3 dir = room - s.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-3f ? dir.normalized : -s.forward;
            yield return Record("s11_seated", 7f, t =>
            {
                float a = Mathf.Lerp(-28f, 28f, Ease(t));
                Vector3 off = Quaternion.AngleAxis(a, Vector3.up) * dir * Mathf.Lerp(1.9f, 1.5f, Ease(t)) + Vector3.up * 0.55f;
                return (focus + off, focus);
            });
        }

        // 12. the end: rise above the shop at dusk
        Call(clock, "SetTime", 18, 50);
        yield return Record("s12_outro", 9f, Move(P(-2f, -5.0f, 1.9f), P(-1f, -13f, 9.5f), P(-2f, 1f, 1.6f), P(-1.5f, 0f, 2.6f)));

        T("PlanarReflection").GetField("ViewOverride").SetValue(null, null);
        T("RainFollow").GetField("ViewOverride").SetValue(null, null);
        UnityEngine.Object.Destroy(camGo);
        T("StoryMode").GetField("AutoPopups").SetValue(null, true);
        T("StatsSystem").GetMethod("EndTests").Invoke(null, null);
        File.WriteAllLines(Path.Combine(root, "shots.txt"), log);
        Assert.Pass("footage written to " + root);
    }
}
