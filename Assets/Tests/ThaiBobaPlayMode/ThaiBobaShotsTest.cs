using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Renders the runtime-only parts of the living soi (night headlights + wet reflections, the rival across the street,
// the neighbour's stall at lunch, festival decor, a delivery rider, the new panels) to BlenderSource/renders/play_*.png
// for a visual check. Needs graphics (run without -nographics):
//   Unity -batchmode -runTests -testPlatform PlayMode -testFilter ThaiBobaShotsTest
[Explicit("renders screenshots; run on purpose, with graphics")]
public class ThaiBobaShotsTest
{
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type T(string name) => Type.GetType(name + ", Assembly-CSharp");
    static Component Find(string type) => (Component)UnityEngine.Object.FindFirstObjectByType(T(type));
    static Component Inst(string type) => (Component)T(type).GetProperty("Instance").GetValue(null);

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

    static Vector3 P(float bx, float by, float bz) => new Vector3(-bx, bz, -by);       // Blender -> Unity

    // starting a new game deletes the saves: keep the player's real ones aside (same as ThaiBobaLoopTest)
    static string SavePath => Path.Combine(Application.persistentDataPath, "bubble_tea_save.json");
    static string SlotDir => Path.Combine(Application.persistentDataPath, "saves");
    static string BackupDir => Path.Combine(Application.persistentDataPath, "saves_shots_backup");
    string savedBackup;
    int tutorialPref;

    [SetUp]
    public void BackupSave()
    {
        savedBackup = File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;
        if (savedBackup != null) File.Delete(SavePath);
        if (Directory.Exists(BackupDir)) Directory.Delete(BackupDir, true);
        if (Directory.Exists(SlotDir)) Directory.Move(SlotDir, BackupDir);
        tutorialPref = PlayerPrefs.GetInt("tb_tutorial_done", 0);
        PlayerPrefs.SetInt("tb_tutorial_done", 1);
    }

    [TearDown]
    public void RestoreSave()
    {
        if (savedBackup != null) File.WriteAllText(SavePath, savedBackup);
        else if (File.Exists(SavePath)) File.Delete(SavePath);
        if (Directory.Exists(SlotDir)) Directory.Delete(SlotDir, true);
        if (Directory.Exists(BackupDir)) Directory.Move(BackupDir, SlotDir);
        PlayerPrefs.SetInt("tb_tutorial_done", tutorialPref);
        Time.timeScale = 1f;
    }

    Camera shot;
    RenderTexture rt;
    Texture2D tex;
    string dir;

    void Shoot(string name, Vector3 pos, Vector3 look, bool ui = false)
    {
        shot.transform.position = pos;
        shot.transform.LookAt(look);
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var modes = new RenderMode[canvases.Length];
        for (int i = 0; i < canvases.Length; i++)
        {
            modes[i] = canvases[i].renderMode;
            if (ui && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay && canvases[i].isRootCanvas)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = shot;
                canvases[i].planeDistance = 0.5f;
            }
        }
        Canvas.ForceUpdateCanvases();
        shot.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        for (int i = 0; i < canvases.Length; i++)
            if (canvases[i] != null && canvases[i].renderMode != modes[i]) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = null; }
        Debug.Log("[SHOT] " + name);
    }

    IEnumerator Wait(float seconds)
    {
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < seconds) yield return null;
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator LivingSoiScreenshots()
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
        dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../BlenderSource/renders"));
        Directory.CreateDirectory(dir);
        GameObject camGo = new GameObject("ShotCam");
        shot = camGo.AddComponent<Camera>();
        shot.fieldOfView = 55f;
        shot.nearClipPlane = 0.05f;
        // bloom etc. like the game camera (URP isn't referenced by the test assembly: reflection)
        Type urpExt = Type.GetType("UnityEngine.Rendering.Universal.CameraExtensions, Unity.RenderPipelines.Universal.Runtime");
        object camData = urpExt?.GetMethod("GetUniversalAdditionalCameraData")?.Invoke(null, new object[] { shot });
        camData?.GetType().GetProperty("renderPostProcessing")?.SetValue(camData, true);
        shot.enabled = false;
        rt = new RenderTexture(1600, 900, 24);
        tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        shot.targetTexture = rt;                                     // keeps the shot's aspect for the wet-street reflection too
        T("PlanarReflection").GetField("ViewOverride").SetValue(null, shot);
        T("RainFollow").GetField("ViewOverride").SetValue(null, shot);
        Component clock = Find("TimeCyc"), weather = Find("WeatherSystem");

        // noon: the neighbour open with riders waiting, cats, the topping bar, the rival across the street
        Call(clock, "SetTime", 12, 0);
        yield return Wait(4f);
        Shoot("play_neighbour", P(0.8f, -4.6f, 2.0f), P(4.4f, -0.4f, 1.0f));
        Shoot("play_toppings", P(0.0f, 3.2f, 1.6f), P(1.78f, 4.15f, 0.95f));
        Call(Inst("RivalShop"), "OpenUp");
        yield return Wait(1f);
        Shoot("play_rival", P(5.0f, -2.4f, 2.2f), P(9.2f, -13.4f, 2.4f));

        // a delivery: ticket on the HUD, the rider at the rack
        T("GameManager").GetField("money").SetValue(Find("GameManager"), 20000);
        Call(Inst("UpgradeShop"), "Buy", "delivery");
        Call(Inst("DecorShop"), "Buy", "barista");
        Component ds = Inst("DeliverySystem");
        Call(ds, "NewTicket", 3);
        Time.timeScale = 4f;
        float t0 = Time.realtimeSinceStartup;
        while (!(bool)Get(ds, "RiderWaiting") && Time.realtimeSinceStartup - t0 < 30f) yield return null;
        Time.timeScale = 1f;
        yield return Wait(0.5f);
        Shoot("play_delivery", P(2.6f, -3.2f, 1.7f), P(1.0f, 0.9f, 1.1f));
        Shoot("play_hud_delivery", P(0.0f, 3.4f, 1.65f), P(0.0f, -2.0f, 1.2f), ui: true);

        // festivals
        Component ev = Inst("DailyEventSystem");
        Call(ev, "RollForDay", 29);
        yield return Wait(0.5f);
        Shoot("play_cny", P(-3.0f, -6.0f, 3.2f), P(1.0f, -0.5f, 2.6f));
        Call(ev, "RollForDay", 19);
        yield return Wait(0.5f);
        Shoot("play_vegetarian", P(-3.0f, -6.0f, 3.2f), P(1.0f, -0.5f, 2.6f));
        Call(ev, "RollForDay", 17);
        yield return Wait(0.5f);
        Shoot("play_mothers_day", P(-3.0f, -6.0f, 3.2f), P(1.0f, -0.5f, 2.6f));

        // rain: the shop stays dry, the street reflects (mirror camera follows the shot camera: give it a frame)
        Call(clock, "SetTime", 13, 0);
        Call(weather, "Force", 1f, 1f, false);
        yield return Wait(3f);
        shot.transform.position = P(0.0f, 3.2f, 1.6f);
        shot.transform.LookAt(P(0.0f, -6.0f, 1.0f));
        yield return null;
        Shoot("play_rain_from_shop", P(0.0f, 3.2f, 1.6f), P(0.0f, -6.0f, 1.0f));
        shot.transform.position = P(-2.0f, -2.6f, 1.6f);
        shot.transform.LookAt(P(4.0f, -9.0f, 0.4f));
        yield return null;
        Shoot("play_rain_reflection", P(-2.0f, -2.6f, 1.6f), P(4.0f, -9.0f, 0.4f));
        shot.transform.position = new Vector3(-7f, 4.2f, 10.5f);
        shot.transform.LookAt(new Vector3(-0.2f, 1.2f, 0f));
        yield return null;
        Shoot("play_rain_street", new Vector3(-7f, 4.2f, 10.5f), new Vector3(-0.2f, 1.2f, 0f));
        Call(weather, "Force", 0f, 0f, false);

        // a cat, close up, sitting and walking
        var cat = (Component)UnityEngine.Object.FindFirstObjectByType(T("StrayCat"));
        Vector3 cp = cat.transform.position;
        Shoot("play_cat", cp + cat.transform.right * 0.9f + Vector3.up * 0.45f + cat.transform.forward * 0.3f, cp + Vector3.up * 0.22f);

        // a rainy night: headlights, tail lights, wet reflections of lamps and signs
        Component traffic = Find("TrafficSpawner");
        var prefabs = (GameObject[])Get(traffic, "vehiclePrefabs");
        var routes = (Array)Get(traffic, "routes");
        for (int i = 0; i < 4; i++)
        {
            object route = routes.GetValue((i * 3) % routes.Length);
            GameObject v = UnityEngine.Object.Instantiate(prefabs[i % prefabs.Length]);
            Component car = v.GetComponent(T("TrafficCar"));
            Call(car, "Init", (Vector3[])Get(route, "points"), (float)Get(route, "Length") * (0.45f + 0.03f * i), 0f);
        }
        Call(clock, "SetTime", 21, 0);
        Call(weather, "Force", 1f, 1f, false);
        yield return Wait(2f);
        shot.transform.position = new Vector3(-7f, 4.2f, 10.5f);
        shot.transform.LookAt(new Vector3(-0.2f, 1.2f, 0f));
        yield return null;
        Shoot("play_night_wet", new Vector3(-7f, 4.2f, 10.5f), new Vector3(-0.2f, 1.2f, 0f));
        shot.transform.position = P(-6.0f, -6.0f, 1.6f);
        shot.transform.LookAt(P(6.0f, -6.0f, 0.6f));
        yield return null;
        Shoot("play_night_road", P(-6.0f, -6.0f, 1.6f), P(6.0f, -6.0f, 0.6f));
        Call(weather, "Force", -1f, -1f, false);

        // the panels
        Call(clock, "SetTime", 12, 0);
        Component story = Inst("StoryMode");
        Call(story, "ShowPanel");
        yield return null;
        Shoot("play_story", P(0.0f, 3.4f, 1.65f), P(0.0f, -2.0f, 1.2f), ui: true);
        Call(story, "ClosePanel");
        object perfect = Activator.CreateInstance(T("ServeResult"));
        T("ServeResult").GetField("quality").SetValue(perfect, Enum.Parse(T("ServeQuality"), "Perfect"));
        Call(T("ShopProgress").GetProperty("Instance").GetValue(null), "RegisterServe", null, perfect, 45, 5);
        Call(menus, "OpenStats");
        yield return null;
        Shoot("play_stats", P(0.0f, 3.4f, 1.65f), P(0.0f, -2.0f, 1.2f), ui: true);
        ((GameObject)Get(menus, "statsPanel")).SetActive(false);                    // (Back() would return to the pause menu)
        Component panel = Inst("OrderPanel");
        Call(panel, "Open");
        Call(panel, "ShowStaffTab");
        yield return null;
        Shoot("play_computer_staff", P(0.0f, 3.4f, 1.65f), P(0.0f, -2.0f, 1.2f), ui: true);
        Call(panel, "Close");
        Component dp = Inst("DecorPlacement");
        Call(dp, "Begin");
        yield return null;
        Shoot("play_arrange", P(0.0f, 3.4f, 1.65f), P(0.0f, -2.0f, 1.2f), ui: true);
        Call(dp, "End");

        T("PlanarReflection").GetField("ViewOverride").SetValue(null, null);
        T("RainFollow").GetField("ViewOverride").SetValue(null, null);
        UnityEngine.Object.Destroy(camGo);
        T("StoryMode").GetField("AutoPopups").SetValue(null, true);
        T("StatsSystem").GetMethod("EndTests").Invoke(null, null);
        Assert.Pass("screenshots written to " + dir);
    }
}
