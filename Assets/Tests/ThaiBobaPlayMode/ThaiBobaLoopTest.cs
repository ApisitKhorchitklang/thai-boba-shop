using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Loads ThaiBobaStreet.unity and lets ThaiBobaAutoPlayer serve real customers through the stations.
// Game types live in Assembly-CSharp, which test assemblies can't reference, so they're reached by reflection.
public class ThaiBobaLoopTest
{
    // Tests start new games and end days, which writes saves: keep the player's real saves aside meanwhile
    static string SavePath => Path.Combine(Application.persistentDataPath, "bubble_tea_save.json");
    static string SlotDir => Path.Combine(Application.persistentDataPath, "saves");
    static string BackupDir => Path.Combine(Application.persistentDataPath, "saves_test_backup");
    string savedBackup;
    int controlsPref, tutorialPref, langPref;

    [SetUp]
    public void BackupSave()
    {
        savedBackup = File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;
        if (savedBackup != null) File.Delete(SavePath);
        if (Directory.Exists(BackupDir)) Directory.Delete(BackupDir, true);
        if (Directory.Exists(SlotDir)) Directory.Move(SlotDir, BackupDir);
        controlsPref = PlayerPrefs.GetInt("tb_controls", 0);
        tutorialPref = PlayerPrefs.GetInt("tb_tutorial_done", 0);
        PlayerPrefs.SetInt("tb_tutorial_done", 1);           // other tests play a normal day; the tutorial test turns it back on
        langPref = PlayerPrefs.GetInt("tb_lang", -1);
        SetLanguage("TH");                                    // checks below look for Thai text; the English test switches
        T("StatsSystem").GetMethod("ResetForTests").Invoke(null, null);          // never touch the player's medals / lifetime stats
        T("StoryMode").GetField("AutoPopups").SetValue(null, false);            // no story popups in the middle of other tests
    }

    [TearDown]
    public void RestoreSave()
    {
        if (savedBackup != null) File.WriteAllText(SavePath, savedBackup);
        else if (File.Exists(SavePath)) File.Delete(SavePath);
        if (Directory.Exists(SlotDir)) Directory.Delete(SlotDir, true);
        if (Directory.Exists(BackupDir)) Directory.Move(BackupDir, SlotDir);
        PlayerPrefs.SetInt("tb_controls", controlsPref);
        PlayerPrefs.SetInt("tb_tutorial_done", tutorialPref);
        SetLanguage(langPref == 1 ? "EN" : "TH");
        if (langPref < 0) PlayerPrefs.DeleteKey("tb_lang");
        Time.timeScale = 1f;
        T("StoryMode").GetField("AutoPopups").SetValue(null, true);
        T("StatsSystem").GetMethod("EndTests").Invoke(null, null);
    }

    // ---------------------------------------------------------------- reflection helpers for the newer systems
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    static object Get(object target, string member)
    {
        Type t = target as Type ?? target.GetType();
        object inst = target is Type ? null : target;
        FieldInfo f = t.GetField(member, Any);
        if (f != null) return f.GetValue(inst);
        return t.GetProperty(member, Any).GetValue(inst);
    }

    static void Set(object target, string member, object value)
    {
        Type t = target as Type ?? target.GetType();
        object inst = target is Type ? null : target;
        FieldInfo f = t.GetField(member, Any);
        if (f != null) f.SetValue(inst, value);
        else t.GetProperty(member, Any).SetValue(inst, value);
    }

    static object Call(object target, string method, params object[] args)
    {
        Type t = target as Type ?? target.GetType();
        object inst = target is Type ? null : target;
        foreach (MethodInfo m in t.GetMethods(Any))
            if (m.Name == method && m.GetParameters().Length == args.Length) return m.Invoke(inst, args);
        throw new MissingMethodException(t.Name, method);
    }

    static Component Inst(string type) => (Component)T(type).GetProperty("Instance").GetValue(null);

    static object NewOrder(string flavor, int boba, bool ice, int sweet) =>
        Activator.CreateInstance(T("Order"), new object[] { boba, ice, flavor, sweet });

    static object Result(string quality)
    {
        object r = Activator.CreateInstance(T("ServeResult"));
        Set(r, "quality", Enum.Parse(T("ServeQuality"), quality));
        return r;
    }

    // a finished, sealed cup made exactly to the order (straight through CupController)
    static GameObject MakeCup(object order, bool extraFoam = false)
    {
        Component dispenser = Find("CupDispenser");
        GameObject prefab = (GameObject)Get(dispenser, "cupPrefab");
        GameObject go = UnityEngine.Object.Instantiate(prefab, new Vector3(0, 30f, 0), Quaternion.identity);
        go.GetComponent<Rigidbody>().isKinematic = true;
        Component cup = go.GetComponent(T("CupController"));
        Call(cup, "Grab");
        int boba = (int)Get(order, "bobaCount");
        if (boba > 0) Call(cup, "AddBoba", boba);
        if ((bool)Get(order, "wantsIce")) Call(cup, "AddIce");
        Call(cup, "Fill", (string)Get(order, "flavor"));
        Call(cup, "SetSweetness", (int)Get(order, "sweetness"));
        if ((bool)Get(order, "wantsFoam") || extraFoam) Call(cup, "AddFoam");
        if ((bool)Get(order, "blended")) Call(cup, "Blend");
        if ((bool)Get(order, "marshmallow")) Call(cup, "AddMarshmallow");
        string top = (string)Get(order, "topping");
        if (!string.IsNullOrEmpty(top)) Call(cup, "AddTopping", top);
        Call(cup, "CloseLid");
        return go;
    }

    static string Quality(object order, GameObject cup) =>
        Get(Call(T("ServeResult"), "Evaluate", order, cup.GetComponent(T("CupController"))), "quality").ToString();

    static Type T(string name) => Type.GetType(name + ", Assembly-CSharp");

    static void SetLanguage(string lang)
    {
        Type loc = T("Loc");
        loc.GetProperty("Current").SetValue(null, Enum.Parse(loc.GetNestedType("Lang"), lang));
    }

    // every visible screen-space UI text that still contains Thai letters
    static System.Collections.Generic.List<string> ThaiOnScreen()
    {
        var left = new System.Collections.Generic.List<string>();
        foreach (TMPro.TMP_Text t in UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
        {
            if (!t.isActiveAndEnabled) continue;
            Canvas c = t.canvas;
            if (c == null || c.renderMode == RenderMode.WorldSpace) continue;
            if (t.text == "ภาษาไทย") continue;                     // the switch back to Thai is written in Thai on purpose
            foreach (char ch in t.text)
                if (ch >= '\u0E01' && ch <= '\u0E5B' && ch != '\u0E3F') { left.Add(t.name + ": " + t.text); break; }   // U+0E3F = baht sign, fine in English
        }
        return left;
    }

    static object Static(string type, string prop) =>
        T(type).GetProperty(prop, BindingFlags.Public | BindingFlags.Static).GetValue(null);

    static Component Find(string type) => (Component)UnityEngine.Object.FindFirstObjectByType(T(type));

    static IEnumerator LoadScene(bool startGame = true)
    {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/ThaiBobaStreet.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null;
        yield return null;
        if (startGame)
        {
            // wait for the new scene's menu to be up (the previous scene's copy may linger for a frame)
            Component menus = null;
            for (int i = 0; i < 120; i++)
            {
                menus = (Component)Static("GameMenus", "Instance");
                if (menus != null && menus.gameObject.scene == SceneManager.GetActiveScene())
                {
                    GameObject main = (GameObject)menus.GetType().GetField("mainMenu").GetValue(menus);
                    if (main != null && main.activeSelf) break;
                }
                yield return null;
            }
            Assert.IsNotNull(menus, "GameMenus not found");
            menus.GetType().GetMethod("StartNewGame").Invoke(menus, null);   // "เริ่มเกมใหม่"
            yield return null;
            Assert.IsTrue((bool)Static("GameMenus", "GameRunning"), "game did not start from the menu");
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator MenuDailySavesAndControls()
    {
        yield return LoadScene(startGame: false);
        yield return null;

        // main menu is up, the shop is paused behind it
        Component menus = (Component)Static("GameMenus", "Instance");
        Assert.IsNotNull(menus, "no GameMenus in the scene");
        GameObject main = (GameObject)menus.GetType().GetField("mainMenu").GetValue(menus);
        Assert.IsTrue(main.activeSelf, "main menu not shown at start");
        Assert.IsFalse((bool)Static("GameMenus", "GameRunning"), "game runs behind the main menu");

        menus.GetType().GetMethod("StartNewGame").Invoke(menus, null);
        yield return null;
        Assert.IsTrue((bool)Static("GameMenus", "GameRunning"), "new game did not start");
        Assert.IsFalse(main.activeSelf, "main menu still visible");
        Component gm = Find("GameManager");
        Component clock = Find("TimeCyc");
        int Money() => (int)gm.GetType().GetField("money").GetValue(gm);
        int Day() => (int)clock.GetType().GetProperty("Days").GetValue(clock);
        Debug.Log($"[TEST] new game: money {Money()}, day {Day()}");
        Assert.AreEqual(1, Day());

        // ending the day autosaves a slot for the new day
        gm.GetType().GetMethod("EndDay").Invoke(gm, null);
        yield return null;
        var list = (System.Collections.IList)T("SaveSystem").GetMethod("ListDaySaves").Invoke(null, null);
        Debug.Log($"[TEST] day saves after EndDay: {list.Count}, day now {Day()}");
        Assert.AreEqual(2, Day());
        Assert.GreaterOrEqual(list.Count, 1, "no daily save written");
        string slot = (string)list[0].GetType().GetField("path").GetValue(list[0]);
        Assert.IsTrue(slot.EndsWith("day_002.json"), "unexpected slot " + slot);

        // spend/earn, then load the day-2 slot: money returns to the saved value
        int savedMoney = Money();
        gm.GetType().GetMethod("AddMoney").Invoke(gm, new object[] { 777 });
        T("SaveSystem").GetMethod("LoadGame", new[] { typeof(string) }).Invoke(null, new object[] { slot });
        yield return null;
        Debug.Log($"[TEST] load slot: money {Money()} (saved {savedMoney}), day {Day()}");
        Assert.AreEqual(savedMoney, Money());
        Assert.AreEqual(2, Day());

        // pause freezes time
        menus.GetType().GetMethod("TogglePause").Invoke(menus, null);
        Assert.AreEqual(0f, Time.timeScale);
        menus.GetType().GetMethod("TogglePause").Invoke(menus, null);
        Assert.AreEqual(1f, Time.timeScale);

        // PC mode hides the touch controls, touch mode shows them
        GameObject touch = Find("ShopHUD").transform.Find("Gameplay/TouchControls").gameObject;   // may be inactive
        GameObject hints = Find("ShopHUD").transform.Find("Gameplay/PCHints").gameObject;
        PropertyInfo controls = T("GameSettings").GetProperty("Controls");
        Type mode = T("GameSettings+ControlMode");
        controls.SetValue(null, Enum.Parse(mode, "PC"));
        yield return null;
        bool hiddenOnPc = !touch.activeSelf;
        Assert.IsTrue(hints.activeSelf, "PC key hints missing in PC mode");
        controls.SetValue(null, Enum.Parse(mode, "Touch"));
        yield return null;
        bool shownOnTouch = touch.activeSelf;
        Assert.IsFalse(hints.activeSelf, "PC key hints still shown in touch mode");
        Debug.Log($"[TEST] touch controls hidden on PC: {hiddenOnPc}, shown on touch: {shownOnTouch}");
        Assert.IsTrue(hiddenOnPc && shownOnTouch, "control mode doesn't switch the touch UI");
    }

    static int CountVisible(string typeName)
    {
        Type t = Type.GetType(typeName + ", Assembly-CSharp");
        int n = 0;
        foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(t, FindObjectsSortMode.None))
        {
            Transform visual = ((Component)o).transform.Find("Visual");
            if (visual == null) continue;
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
                if (r.enabled && r.gameObject.activeInHierarchy) { n++; break; }
        }
        return n;
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator PlayerWalksAndNpcsAppear()
    {
        yield return LoadScene();

        // Walk forward with the same input the on-screen joystick feeds
        Type moveType = Type.GetType("MovementWithJoy, Assembly-CSharp");
        Component move = (Component)UnityEngine.Object.FindFirstObjectByType(moveType);
        Assert.IsNotNull(move, "no MovementWithJoy player");
        Assert.IsNotNull(moveType.GetField("joystick").GetValue(move), "joystick not wired to the player");
        Vector3 start = move.transform.position;
        MethodInfo setInput = moveType.GetMethod("SetJoystickInput");
        float t0 = Time.time;
        while (Time.time - t0 < 1f)
        {
            setInput.Invoke(move, new object[] { new Vector2(0f, 1f) });
            yield return null;
        }
        float walked = Vector3.Distance(start, move.transform.position);
        Debug.Log($"[TEST] player walked {walked:0.00} m");
        Assert.Greater(walked, 0.5f, "player did not move with joystick input");

        // Customers and passers-by should show up within a minute of real time
        float w0 = Time.realtimeSinceStartup;
        int customers = 0, walkers = 0;
        while (Time.realtimeSinceStartup - w0 < 60f && (customers == 0 || walkers == 0))
        {
            customers = CountVisible("CustomerController");
            walkers = CountVisible("PedestrianWalker");
            yield return null;
        }
        Debug.Log($"[TEST] visible customers {customers}, pedestrians {walkers} after {Time.realtimeSinceStartup - w0:0}s");
        Assert.Greater(customers, 0, "no visible customer NPC");
        Assert.Greater(walkers, 0, "no visible pedestrian NPC");

        // A moving NPC should be playing its Walk clip (rigged PS2-style characters)
        bool walking = false;
        float a0 = Time.realtimeSinceStartup;
        while (!walking && Time.realtimeSinceStartup - a0 < 20f)
        {
            foreach (Animator an in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                if (an.isActiveAndEnabled && an.runtimeAnimatorController != null &&
                    an.GetCurrentAnimatorStateInfo(0).IsName("Walk"))
                    walking = true;
            yield return null;
        }
        Debug.Log($"[TEST] NPC walk animation playing: {walking}");
        Assert.IsTrue(walking, "no NPC is playing the Walk animation");

        // NPCs stay on the footpath: no NavMesh on the road, and nobody steps onto it (road starts at z = 3.2)
        bool roadWalkable = UnityEngine.AI.NavMesh.SamplePosition(new Vector3(0f, -0.15f, 6.7f), out _, 1.0f,
                                                                  UnityEngine.AI.NavMesh.AllAreas);
        Assert.IsFalse(roadWalkable, "the road is part of the NavMesh");
        // The whole footpath must be walkable end to end, and both ends must reach the shop door
        Vector3 Pos(string n) => GameObject.Find(n).transform.position;
        foreach (var (from, to) in new[] { ("Spawn_Left", "Spawn_Right"), ("Spawn_Left", "EnterPoint"), ("Spawn_Right", "EnterPoint"),
                                           ("EnterPoint", "OrderPoint"), ("FarEnd_Left", "FarEnd_Right") })
        {
            var path = new UnityEngine.AI.NavMeshPath();
            UnityEngine.AI.NavMesh.SamplePosition(Pos(from), out var a, 2f, UnityEngine.AI.NavMesh.AllAreas);
            UnityEngine.AI.NavMesh.SamplePosition(Pos(to), out var b, 2f, UnityEngine.AI.NavMesh.AllAreas);
            UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, UnityEngine.AI.NavMesh.AllAreas, path);
            Debug.Log($"[TEST] path {from} -> {to}: {path.status}");
            Assert.AreEqual(UnityEngine.AI.NavMeshPathStatus.PathComplete, path.status, $"footpath blocked between {from} and {to}");
        }
        // the two footpaths are separate islands: the far side can't reach the shop
        {
            var cross = new UnityEngine.AI.NavMeshPath();
            UnityEngine.AI.NavMesh.SamplePosition(Pos("FarEnd_Left"), out var fa, 2f, UnityEngine.AI.NavMesh.AllAreas);
            UnityEngine.AI.NavMesh.SamplePosition(Pos("EnterPoint"), out var fb, 2f, UnityEngine.AI.NavMesh.AllAreas);
            UnityEngine.AI.NavMesh.CalculatePath(fa.position, fb.position, UnityEngine.AI.NavMesh.AllAreas, cross);
            Debug.Log($"[TEST] path FarEnd_Left -> EnterPoint (must not cross the road): {cross.status}");
            Assert.AreNotEqual(UnityEngine.AI.NavMeshPathStatus.PathComplete, cross.status, "a path crosses the road");
        }
        int onRoad = 0, farWalkers = 0;
        float r0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - r0 < 8f)
        {
            foreach (string tn in new[] { "PedestrianWalker", "CustomerController" })
                foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(Type.GetType(tn + ", Assembly-CSharp"), FindObjectsSortMode.None))
                {
                    float z = ((Component)o).transform.position.z;
                    if (z > 3.2f && z < 10.2f) onRoad++;          // road spans z 3.2 .. 10.2
                    if (z > 10.3f) farWalkers = Mathf.Max(farWalkers, 1);
                }
            yield return null;
        }
        int farCount = 0;
        foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(Type.GetType("PedestrianWalker, Assembly-CSharp"), FindObjectsSortMode.None))
            if (((Component)o).transform.position.z > 10.3f) farCount++;
        Debug.Log($"[TEST] NPC samples on the road: {onRoad}; pedestrians on the opposite footpath: {farCount}");
        Assert.AreEqual(0, onRoad, "an NPC walked onto the road");
        Assert.Greater(farWalkers, 0, "nobody walks on the opposite footpath");

        // Day/night follows the clock: noon = bright high sun, 20:30 = dark with street lamps on
        {
            Type clockType = Type.GetType("TimeCyc, Assembly-CSharp");
            Component clock = (Component)UnityEngine.Object.FindFirstObjectByType(clockType);
            Light sunLight = clock.GetComponent<Light>();
            MethodInfo setTime = clockType.GetMethod("SetTime");
            setTime.Invoke(clock, new object[] { 12, 0 });
            yield return null; yield return null;
            float noonI = sunLight.intensity, noonPitch = sunLight.transform.eulerAngles.x;
            float noonAmb = RenderSettings.ambientSkyColor.grayscale;
            setTime.Invoke(clock, new object[] { 20, 30 });
            yield return null; yield return null;
            float nightI = sunLight.intensity, nightAmb = RenderSettings.ambientSkyColor.grayscale;
            int lampsOn = 0;
            foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.name == "LampLight" && l.enabled) lampsOn++;
            Debug.Log($"[TEST] noon: sun {noonI:0.00} pitch {noonPitch:0} ambient {noonAmb:0.00} | 20:30: light {nightI:0.00} ambient {nightAmb:0.00}, lamps on {lampsOn}");
            Assert.Greater(noonI, 1f, "sun too weak at noon");
            Assert.Greater(noonPitch, 60f, "sun not high at noon");
            Assert.Less(nightI, 0.3f, "still bright at night");
            Assert.Less(nightAmb, noonAmb * 0.35f, "ambient light not darker at night");
            Assert.Greater(lampsOn, 0, "street lamps off at night");
            setTime.Invoke(clock, new object[] { 9, 0 });
        }

        // Effects: steam is playing
        int steaming = 0;
        foreach (ParticleSystem ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            if (ps.name.StartsWith("FX_Steam") && ps.isPlaying) steaming++;
        Debug.Log($"[TEST] steam emitters playing: {steaming}");
        Assert.Greater(steaming, 0, "no steam effect playing");

        // Traffic: vehicles exist and drive along the road
        Type carType = Type.GetType("TrafficCar, Assembly-CSharp");
        UnityEngine.Object[] cars = UnityEngine.Object.FindObjectsByType(carType, FindObjectsSortMode.None);
        Assert.Greater(cars.Length, 0, "no traffic vehicles");
        // a car may be braking behind another one, so take the one that moved the most
        var starts = new System.Collections.Generic.Dictionary<Transform, Vector3>();
        foreach (UnityEngine.Object o in cars) starts[((Component)o).transform] = ((Component)o).transform.position;
        float c0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - c0 < 1.5f)
            yield return null;
        float moved = 0f;
        foreach (var kv in starts)
            moved = Mathf.Max(moved, kv.Key != null ? Vector3.Distance(kv.Value, kv.Key.position) : 99f);
        Debug.Log($"[TEST] traffic: {cars.Length} vehicles, fastest moved {moved:0.0} m in 1.5 s");
        Assert.Greater(moved, 2f, "traffic is not moving");

        // Every vehicle has a pass-by sound + horn; the soi has day / night / market ambience
        int voiced = 0, alive = 0;
        foreach (UnityEngine.Object o in cars)
        {
            if (o == null) continue;                // drove into a tunnel meanwhile
            alive++;
            Component vs = ((Component)o).GetComponent(T("VehicleSound"));
            if (vs == null) continue;
            var pass = (AudioClip[])vs.GetType().GetField("passBy").GetValue(vs);
            var horns = (AudioClip[])vs.GetType().GetField("horns").GetValue(vs);
            if (pass != null && pass.Length > 0 && pass[0] != null && horns != null && horns.Length > 0 && horns[0] != null) voiced++;
        }
        Component audio = Find("GameAudio");
        int loops = 0;
        foreach (string f in new[] { "birdsLoop", "nightLoop", "marketLoop" })
            if (audio.GetType().GetField(f).GetValue(audio) != null) loops++;
        Debug.Log($"[TEST] vehicles with sound {voiced}/{alive}, ambience loops {loops}/3");
        Assert.AreEqual(alive, voiced, "some vehicles have no pass-by/horn sound");
        Assert.AreEqual(3, loops, "ambience loops missing");
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator StormMakesRainWetStreetsAndUmbrellas()
    {
        yield return LoadScene();

        Type wType = T("WeatherSystem");
        Assert.AreEqual("Rainy", wType.GetMethod("SeasonForDay").Invoke(null, new object[] { 11, 10 }).ToString(), "day 11 should be rainy season");
        Component weather = Find("WeatherSystem");
        Assert.IsNotNull(weather, "no WeatherSystem in the scene");
        Assert.IsNotNull(Find("CloudLayer"), "no clouds");

        int flashes = 0;
        Action onFlash = () => flashes++;
        EventInfo ev = wType.GetEvent("OnLightning");
        ev.AddEventHandler(null, onFlash);
        try
        {
            wType.GetMethod("Force").Invoke(weather, new object[] { 1f, 1f, true });

            Component wet = Find("WetSurfaces");
            Assert.IsNotNull(wet, "no WetSurfaces");
            ParticleSystem rain = (ParticleSystem)wType.GetField("rain").GetValue(weather);
            Assert.IsNotNull(rain, "rain particles not wired to WeatherSystem");

            Type npcW = T("NPCWeather");
            int umbrellas = 0;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 40f)
            {
                umbrellas = 0;
                foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(npcW, FindObjectsSortMode.None))
                    if ((bool)npcW.GetProperty("UmbrellaOpen").GetValue(o)) umbrellas++;
                if (umbrellas > 0 && flashes > 0 && rain.particleCount > 50) break;
                yield return null;
            }
            float smooth = (float)wet.GetType().GetProperty("CurrentSmoothness").GetValue(wet);
            Debug.Log($"[TEST] rain particles {rain.particleCount}, smoothness {smooth:0.00}, umbrellas {umbrellas}, flashes {flashes}");
            Assert.Greater(rain.particleCount, 50, "no rain falling during the storm");
            Assert.Greater(smooth, 0.7f, "street is not wet / glossy");
            Assert.Greater(umbrellas, 0, "nobody opened an umbrella");
            Assert.Greater(flashes, 0, "no lightning in the storm");

            Component hud = Find("ShopHUD");
            object wt = hud.GetType().GetField("weatherText").GetValue(hud);
            Assert.IsNotNull(wt, "HUD has no weather chip");
            string label = (string)wt.GetType().GetProperty("text").GetValue(wt);
            StringAssert.Contains("พายุ", label);
        }
        finally
        {
            ev.RemoveEventHandler(null, onFlash);
            wType.GetMethod("Force").Invoke(weather, new object[] { -1f, -1f, false });
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator EnglishLanguageSwitchesEveryText()
    {
        yield return LoadScene(startGame: false);
        SetLanguage("EN");
        yield return null;
        var menuLeft = ThaiOnScreen();
        Debug.Log($"[TEST] EN main menu, Thai left: {menuLeft.Count} {string.Join(" | ", menuLeft)}");
        Assert.IsEmpty(menuLeft, "Thai text left on the main menu in English");

        Component menus = (Component)Static("GameMenus", "Instance");
        menus.GetType().GetMethod("OpenSettings").Invoke(menus, null);
        yield return null;
        var settingsLeft = ThaiOnScreen();
        Assert.IsEmpty(settingsLeft, "Thai text left in Settings: " + string.Join(" | ", settingsLeft));
        menus.GetType().GetMethod("Back").Invoke(menus, null);

        menus.GetType().GetMethod("StartNewGame").Invoke(menus, null);
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 6f) yield return null;     // let a customer order / toasts appear
        var hudLeft = ThaiOnScreen();
        Debug.Log($"[TEST] EN in-game, Thai left: {hudLeft.Count} {string.Join(" | ", hudLeft)}");
        Assert.IsEmpty(hudLeft, "Thai text left on the HUD in English");

        menus.GetType().GetMethod("TogglePause").Invoke(menus, null);
        yield return null;
        var pauseLeft = ThaiOnScreen();
        Assert.IsEmpty(pauseLeft, "Thai text left in the pause menu: " + string.Join(" | ", pauseLeft));
        menus.GetType().GetMethod("TogglePause").Invoke(menus, null);

        // and back to Thai
        SetLanguage("TH");
        yield return null;
        Component hud = Find("ShopHUD");
        TMPro.TMP_Text goalsTitle = hud.transform.Find("Gameplay/GoalsCard/Title").GetComponent<TMPro.TMP_Text>();
        Assert.AreEqual("ภารกิจวันนี้", goalsTitle.text, "labels did not switch back to Thai");
    }

    [UnityTest, Timeout(240000)]
    public IEnumerator TutorialGuidesFirstCustomer()
    {
        PlayerPrefs.SetInt("tb_tutorial_done", 0);
        yield return LoadScene();
        Type tutType = T("TutorialSystem");
        Component tut = (Component)tutType.GetProperty("Instance").GetValue(null);
        Assert.IsNotNull(tut, "no TutorialSystem in the scene");
        float w = Time.realtimeSinceStartup;
        while (!(bool)tutType.GetProperty("Running").GetValue(tut) && Time.realtimeSinceStartup - w < 5f) yield return null;
        Assert.IsTrue((bool)tutType.GetProperty("Running").GetValue(tut), "tutorial did not start on day 1");
        Assert.IsTrue(tut.transform.Find("Gameplay/TutorialCard").gameObject.activeInHierarchy, "tutorial card not shown");

        var seen = new System.Collections.Generic.List<string>();     // steps visited (polled every frame)
        Component clock = Find("TimeCyc");
        float t0 = (float)clock.GetType().GetProperty("TimeOfDay").GetValue(clock);

        // let the tutorial walk the player up to "take a cup" before the bot starts working
        float wait = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - wait < 90f)
        {
            string st = tutType.GetProperty("Current").GetValue(tut).ToString();
            if (!seen.Contains(st)) seen.Add(st);
            if (st == "Cook" || st == "TakeCup") break;          // the bot takes over from the first hands-on step
            yield return null;
        }
        Type bot = Type.GetType("ThaiBobaAutoPlayer, Assembly-CSharp");
        Component b = new GameObject("AutoPlayer").AddComponent(bot);
        bot.GetField("customersToServe").SetValue(b, 1);
        bot.GetField("endDayWhenDone").SetValue(b, false);

        float start = Time.realtimeSinceStartup;
        string step = "";
        while (Time.realtimeSinceStartup - start < 180f)
        {
            step = tutType.GetProperty("Current").GetValue(tut).ToString();
            if (!seen.Contains(step)) seen.Add(step);
            if (step == "Done") break;
            yield return null;
        }
        float t1 = (float)clock.GetType().GetProperty("TimeOfDay").GetValue(clock);
        var queue = (System.Collections.IList)T("CustomerController").GetField("Queue").GetValue(null);
        Debug.Log($"[TEST] tutorial steps: {string.Join(" > ", seen)} | clock {t0:0.00} -> {t1:0.00} | queue {queue.Count}");
        Assert.AreEqual("Done", step, "tutorial never reached the end");
        foreach (string must in new[] { "Cook", "TakeCup", "Pour", "Seal", "Serve" })
            Assert.Contains(must, seen, "tutorial skipped step " + must);
        Assert.AreEqual(t0, t1, 0.01f, "clock kept running during the tutorial");

        tutType.GetMethod("Finish").Invoke(tut, null);
        yield return null;
        Assert.IsFalse((bool)tutType.GetProperty("Running").GetValue(tut));
        Assert.AreEqual(1, PlayerPrefs.GetInt("tb_tutorial_done", 0), "tutorial not marked as done");
        Assert.IsFalse((bool)T("Spawner").GetField("TutorialActive").GetValue(null), "spawner still in tutorial mode");
        UnityEngine.Object.Destroy(b.gameObject);
    }

    [UnityTest, Timeout(150000)]
    public IEnumerator OnlyFrontCustomerLosesPatience()
    {
        yield return LoadScene();
        var queue = (System.Collections.IList)T("CustomerController").GetField("Queue").GetValue(null);
        PropertyInfo patience = T("CustomerController").GetProperty("Patience");
        Time.timeScale = 3f;                                   // let a line build up faster (nobody serves them)
        float t0 = Time.realtimeSinceStartup;
        while (queue.Count < 2 && Time.realtimeSinceStartup - t0 < 90f) yield return null;
        Time.timeScale = 1f;
        if (queue.Count < 2) Assert.Ignore("no second customer queued within 90 s");

        object front = queue[0], second = queue[1];
        float f0 = (float)patience.GetValue(front), s0 = (float)patience.GetValue(second);
        float w0 = Time.time;
        while (Time.time - w0 < 3f && queue.Count >= 2 && queue[1] == second) yield return null;
        float f1 = (float)patience.GetValue(front), s1 = (float)patience.GetValue(second);
        Debug.Log($"[TEST] front patience {f0:0.0} -> {f1:0.0}, second in line {s0:0.0} -> {s1:0.0}");
        Assert.AreEqual(s0, s1, 0.001f, "customer waiting in line lost patience");
        Assert.Less(f1, f0 - 1f, "the customer at the counter never loses patience");

        // the story popup opened during the tutorial must not leave patience frozen once both are gone
        Type cc = T("CustomerController");
        Component story = Find("StoryMode");
        cc.GetField("FrozenByTutorial").SetValue(null, true);         // tutorial running
        story.GetType().GetMethod("ShowPanel").Invoke(story, null);
        cc.GetField("FrozenByTutorial").SetValue(null, false);        // tutorial done
        Assert.IsTrue((bool)cc.GetProperty("PatienceFrozen").GetValue(null), "story popup open: patience should wait");
        story.GetType().GetMethod("ClosePanel").Invoke(story, null);
        Assert.IsFalse((bool)cc.GetProperty("PatienceFrozen").GetValue(null), "patience stays frozen after the popup and the tutorial closed");
        float p0 = (float)patience.GetValue(front);
        yield return new WaitForSeconds(1.5f);
        if (queue.Count > 0 && queue[0] == front)
            Assert.Less((float)patience.GetValue(front), p0 - 0.5f, "patience does not drop after closing the story popup");
    }

    static Component NewCup(out Type ct)
    {
        Component dispenser = Find("CupDispenser");
        GameObject cupGo = UnityEngine.Object.Instantiate((GameObject)dispenser.GetType().GetField("cupPrefab").GetValue(dispenser), new Vector3(0, 30, 0), Quaternion.identity);
        cupGo.GetComponent<Rigidbody>().isKinematic = true;
        Component cup = cupGo.GetComponent(T("CupController"));
        ct = cup.GetType();
        ct.GetMethod("Grab").Invoke(cup, null);
        return cup;
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator SeasonsAndFestivals()
    {
        yield return LoadScene();
        // seasonal stations on the cart
        bool blender = false, marsh = false;
        foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(T("IngredientStation"), FindObjectsSortMode.None))
        {
            string k = o.GetType().GetField("kind").GetValue(o).ToString();
            blender |= k == "Blend"; marsh |= k == "Marshmallow";
        }
        Assert.IsTrue(blender && marsh, "seasonal cart stations missing");

        // frappe: tea + ice + blend, shows the dome; graded against the order
        Component cup = NewCup(out Type ct);
        ct.GetMethod("Fill").Invoke(cup, new object[] { "ThaiTea" });
        Assert.IsFalse((bool)ct.GetMethod("Blend").Invoke(cup, null), "blended without ice");
        ct.GetMethod("AddIce").Invoke(cup, null);
        Assert.IsTrue((bool)ct.GetMethod("Blend").Invoke(cup, null));
        yield return null;
        Transform dome = null;
        foreach (Transform t in cup.GetComponentsInChildren<Transform>(true)) if (t.name == "Cup_Frappe") dome = t;
        Assert.IsTrue(dome != null && dome.gameObject.activeInHierarchy, "frappe not shown");
        ct.GetMethod("CloseLid").Invoke(cup, null);
        Type orderT = T("Order");
        object frappeOrder = Activator.CreateInstance(orderT, new object[] { 0, true, "ThaiTea", 100 });
        orderT.GetField("blended").SetValue(frappeOrder, true);
        MethodInfo eval = T("ServeResult").GetMethod("Evaluate");
        string qFrappe = eval.Invoke(null, new object[] { frappeOrder, cup }).GetType().GetField("quality").GetValue(eval.Invoke(null, new object[] { frappeOrder, cup })).ToString();
        UnityEngine.Object.Destroy(cup.gameObject);

        // Songkran: an open cup near the counter gets soaked and is refused
        Component events = Find("DailyEventSystem");
        events.GetType().GetMethod("RollForDay").Invoke(events, new object[] { 10 });
        string songkran = events.GetType().GetProperty("Current").GetValue(events).GetType().GetField("type").GetValue(events.GetType().GetProperty("Current").GetValue(events)).ToString();
        Component splash = Find("SongkranSplash");
        Assert.IsNotNull(splash, "no Songkran water fight");
        Component aim = Find("RaycastAim");
        Component wet = NewCup(out ct);
        ct.GetMethod("Fill").Invoke(wet, new object[] { "ThaiTea" });
        aim.GetType().GetMethod("HoldItem").Invoke(aim, new object[] { wet.transform });
        Transform player = Find("MovementWithJoy").transform;
        var charCtrl = player.GetComponent<CharacterController>();
        charCtrl.enabled = false;
        player.position = new Vector3(player.position.x, player.position.y, -2.0f);       // right behind the counter
        charCtrl.enabled = true;
        yield return null;
        splash.GetType().GetMethod("Splash").Invoke(splash, null);
        bool soaked = (bool)ct.GetField("watered").GetValue(wet);
        ct.GetMethod("CloseLid").Invoke(wet, null);
        object plain = Activator.CreateInstance(orderT, new object[] { 0, false, "ThaiTea", 100 });
        object wetRes = eval.Invoke(null, new object[] { plain, wet });
        string qWet = wetRes.GetType().GetField("quality").GetValue(wetRes).ToString();

        // Loy Krathong: lanterns + evening rush
        events.GetType().GetMethod("RollForDay").Invoke(events, new object[] { 27 });
        object cur = events.GetType().GetProperty("Current").GetValue(events);
        string loy = cur.GetType().GetField("type").GetValue(cur).ToString();
        GameObject loyDecor = (GameObject)events.GetType().GetField("loyKrathongDecor").GetValue(events);
        float evening = (float)events.GetType().GetMethod("HourSpawnMultiplier").Invoke(events, new object[] { 19 });

        Debug.Log($"[TEST] seasons: frappe order -> {qFrappe}, day10 {songkran}, soaked {soaked} -> {qWet}, day27 {loy}, lanterns {loyDecor != null && loyDecor.activeSelf}, evening x{evening}");
        Assert.AreEqual("Perfect", qFrappe);
        Assert.AreEqual("Songkran", songkran);
        Assert.IsTrue(soaked, "open cup by the counter was not soaked");
        Assert.AreEqual("Wrong", qWet, "soaked drink was accepted");
        Assert.AreEqual("LoyKrathong", loy);
        Assert.IsTrue(loyDecor != null && loyDecor.activeSelf, "no Loy Krathong decorations");
        Assert.AreEqual(2f, evening, 0.01f);
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator DailyChallengeIsSeededAndUnsaved()
    {
        yield return LoadScene(startGame: false);
        Type cm = T("ChallengeMode");
        Type cp = T("CustomerProfiles");
        // same seed -> same customers and orders
        string Orders()
        {
            cm.GetMethod("Begin").Invoke(null, new object[] { 20261016 });
            var sb = new System.Text.StringBuilder();
            object prof = cp.GetMethod("Get").Invoke(null, new object[] { Enum.Parse(T("CustomerType"), "Student") });
            for (int i = 0; i < 6; i++)
            {
                sb.Append(cp.GetMethod("Roll").Invoke(null, new object[] { 12 })).Append(':');
                sb.Append(cp.GetMethod("CreateOrder").Invoke(null, new object[] { prof, 3, false })).Append('|');
            }
            return sb.ToString();
        }
        string a = Orders(), b = Orders();
        Assert.AreEqual(a, b, "challenge orders differ for the same day");

        int savesBefore = Directory.Exists(SlotDir) ? Directory.GetFiles(SlotDir).Length : 0;
        Component menus = (Component)Static("GameMenus", "Instance");
        menus.GetType().GetMethod("StartChallenge").Invoke(menus, new object[] { 20261016 });
        yield return null;
        Component gm = Find("GameManager");
        Component clock = Find("TimeCyc");
        int money = (int)gm.GetType().GetField("money").GetValue(gm);
        int hour = (int)clock.GetType().GetProperty("Hours").GetValue(clock);
        Assert.IsTrue((bool)cm.GetProperty("Active").GetValue(null));
        T("SaveSystem").GetMethod("SaveGame").Invoke(null, null);
        int savesAfter = Directory.Exists(SlotDir) ? Directory.GetFiles(SlotDir).Length : 0;

        // 15:00 -> the challenge result
        clock.GetType().GetMethod("SetTime").Invoke(clock, new object[] { 15, 0 });
        float t0 = Time.realtimeSinceStartup;
        while (!(bool)Static("ShopHUD", "SummaryOpen") && Time.realtimeSinceStartup - t0 < 5f) yield return null;
        Component hud = Find("ShopHUD");
        string title = ((TMPro.TMP_Text)hud.GetType().GetField("summaryTitle").GetValue(hud)).text;
        string body = ((TMPro.TMP_Text)hud.GetType().GetField("summaryText").GetValue(hud)).text;
        bool supplyHidden = !hud.transform.Find("SummaryPanel/Card/Supply0").gameObject.activeSelf;
        string clip = GUIUtility.systemCopyBuffer;
        Debug.Log($"[TEST] challenge: start {hour}:00 money {money}, saves {savesBefore}->{savesAfter}, summary '{title}', restock hidden {supplyHidden}, share '{clip}'");
        Assert.AreEqual(11, hour);
        Assert.AreEqual(300, money);
        Assert.AreEqual(savesBefore, savesAfter, "the challenge wrote a save");
        Assert.IsTrue((bool)Static("ShopHUD", "SummaryOpen"), "no result at 15:00");
        StringAssert.Contains("16/10/2026", title);
        Assert.IsTrue(supplyHidden, "restock buttons still shown in the challenge result");
        StringAssert.Contains("#ThaiBobaShop", clip);
        cm.GetMethod("End").Invoke(null, null);
        PlayerPrefs.DeleteKey("tb_challenge_20261016");
    }

    [UnityTest, Timeout(60000)]
    public IEnumerator DrinkAnimationsEmotesAndDayReport()
    {
        yield return LoadScene();
        // pouring: a stream appears and the level rises from the bottom
        Component dispenser = Find("CupDispenser");
        GameObject cupGo = UnityEngine.Object.Instantiate((GameObject)dispenser.GetType().GetField("cupPrefab").GetValue(dispenser), new Vector3(0, 30, 0), Quaternion.identity);
        cupGo.GetComponent<Rigidbody>().isKinematic = true;
        Component cup = cupGo.GetComponent(T("CupController"));
        Type ct = cup.GetType();
        Component slosh = cupGo.GetComponent(T("LiquidSlosh"));
        PropertyInfo fillP = slosh.GetType().GetProperty("Fill");
        yield return null;
        ct.GetMethod("Grab").Invoke(cup, null);
        ct.GetMethod("Fill").Invoke(cup, new object[] { "Taro" });
        yield return null;
        float fillStart = (float)fillP.GetValue(slosh);
        Transform stream = cupGo.transform.Find("PourStream");
        bool streamShown = stream != null && stream.gameObject.activeSelf;
        float t0 = Time.time;
        while (Time.time - t0 < 1.2f) yield return null;
        float fillEnd = (float)fillP.GetValue(slosh);

        // pearls drop in from above
        Transform pearls = null;
        foreach (Transform t in cupGo.GetComponentsInChildren<Transform>(true)) if (t.name == "Cup_Pearls") pearls = t;
        Vector3 rest = pearls.localPosition;
        ct.GetMethod("AddBoba").Invoke(cup, new object[] { 1 });
        yield return null;
        float dropOffset = pearls.localPosition.y - rest.y;
        UnityEngine.Object.Destroy(cupGo);

        // emote bubble + reviewer photo flash
        Component sp = Find("Spawner");
        GameObject custPrefab = (GameObject)sp.GetType().GetField("customerPrefab").GetValue(sp);
        Component prefabEmote = custPrefab.GetComponent(T("CustomerEmote"));
        Assert.IsNotNull(prefabEmote, "customers have no emote bubble");
        GameObject dummy = new GameObject("EmoteDummy");
        dummy.transform.position = new Vector3(0, 30, 0);
        Component emote = dummy.AddComponent(T("CustomerEmote"));
        foreach (string f in new[] { "love", "happy", "meh", "angry", "camera" })
            emote.GetType().GetField(f).SetValue(emote, emote.GetType().GetField(f).GetValue(prefabEmote));
        yield return null;
        emote.GetType().GetMethod("Show").Invoke(emote, new object[] { Enum.Parse(T("CustomerEmote+Kind"), "Love"), 1f, 0f });
        yield return null;
        var bubble = dummy.GetComponentInChildren<SpriteRenderer>();
        bool bubbleOn = bubble != null && bubble.enabled && bubble.sprite != null;
        emote.GetType().GetMethod("TakePhoto").Invoke(emote, null);
        bool flashed = false;
        t0 = Time.time;
        while (Time.time - t0 < 1.5f) { Light l = dummy.GetComponentInChildren<Light>(); if (l != null && l.enabled) flashed = true; yield return null; }
        UnityEngine.Object.Destroy(dummy);

        // end-of-day insights
        Type ds = T("ShopProgress+DayStats");
        object stats = Activator.CreateInstance(ds);
        ((int[])ds.GetField("soldByDrink").GetValue(stats))[0] = 5;
        ((int[])ds.GetField("servedByHour").GetValue(stats))[12] = 7;
        object[] outArg = { 0 };
        string best = (string)ds.GetMethod("BestSeller").Invoke(stats, outArg);
        object[] outH = { 0 };
        int hour = (int)ds.GetMethod("BusiestHour").Invoke(stats, outH);

        Debug.Log($"[TEST] feel: fill {fillStart:0.00} -> {fillEnd:0.00}, stream {streamShown}, pearl drop +{dropOffset:0.000}, bubble {bubbleOn}, flash {flashed}, best '{best}' x{outArg[0]}, busiest {hour}:00 x{outH[0]}");
        Assert.Less(fillStart, 0.5f, "tea did not start low");
        Assert.AreEqual(1f, fillEnd, 0.001f, "cup never filled up");
        Assert.IsTrue(streamShown, "no pouring stream");
        Assert.Greater(dropOffset, 0.02f, "pearls did not drop in");
        Assert.IsTrue(bubbleOn, "emote bubble not shown");
        Assert.IsTrue(flashed, "reviewer photo did not flash");
        Assert.IsNotNull(best, "no best seller");
        Assert.AreEqual(12, hour);
    }

    // After the expansion the party wall is knocked through, and served customers sit down with their drinks
    // (at the annex tables and at the seating set out front), play the Sit animation, then get up and leave.
    [UnityTest, Timeout(240000)]
    public IEnumerator CustomersSitAfterTheExpansion()
    {
        yield return LoadScene();
        Component gm = Find("GameManager");
        gm.GetType().GetField("money").SetValue(gm, 20000);
        // the wall between our shop and the shop next door: a ray from behind the counter toward the neighbour
        Vector3 from = new Vector3(1.5f, 1.2f, -1.9f), dir = Vector3.right;
        Assert.IsTrue(Physics.Raycast(from, dir, 2.6f, ~0, QueryTriggerInteraction.Ignore), "no wall next door before the expansion?");
        Component ds = Inst("DecorShop");
        Assert.IsTrue((bool)Call(ds, "Buy", "expand"), "could not expand");
        Assert.IsTrue((bool)Call(Find("UpgradeShop"), "Buy", "seating"), "could not buy the seating set");
        yield return null;
        yield return new WaitForFixedUpdate();
        GameObject open = GameObject.Find("SM_Shophouse_BobaShopOpen"), annex = GameObject.Find("SM_Shophouse_BobaAnnex");
        Assert.IsTrue(open != null && annex != null, "the knocked-through shop is not shown");
        Assert.IsNull(GameObject.Find("BeforeExpansion"), "the old shell / pharmacy are still there");
        bool blocked = Physics.Raycast(from, dir, out RaycastHit hit, 2.6f, ~0, QueryTriggerInteraction.Ignore);
        Assert.IsFalse(blocked, "the wall is still there: hit " + (blocked ? hit.collider.name : ""));
        var seats = (System.Collections.IList)Get(T("SeatSpot"), "All");
        Assert.AreEqual(11, seats.Count, "8 seats in the annex + 3 at the table out front");

        // serve customers until one sits down
        Type cc = T("CustomerController");
        cc.GetField("SitChance").SetValue(null, 1f);
        cc.GetField("SatDown").SetValue(null, 0);
        Type bot = T("ThaiBobaAutoPlayer");
        Component b = new GameObject("AutoPlayer").AddComponent(bot);
        Set(b, "customersToServe", 3);
        Set(b, "endDayWhenDone", false);
        float t0 = Time.realtimeSinceStartup;
        Component sitter = null;
        while (sitter == null && Time.realtimeSinceStartup - t0 < 150f)
        {
            foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(cc, FindObjectsSortMode.None))
                if ((bool)Get(o, "IsSeated")) { sitter = (Component)o; break; }
            yield return null;
        }
        Assert.IsNotNull(sitter, "nobody sat down after being served (" + bot.GetField("Report").GetValue(null) + ")");
        yield return new WaitForSeconds(0.8f);                    // blend into the sitting pose
        Component seat = (Component)Get(sitter, "Seat");
        float off = Vector3.Distance(sitter.transform.position, seat.transform.position);
        Animator anim = null;
        foreach (Animator a in sitter.GetComponentsInChildren<Animator>()) if (a.isActiveAndEnabled) anim = a;
        Assert.IsNotNull(anim, "seated customer has no animator");
        Transform hips = null;
        foreach (Transform t in anim.GetComponentsInChildren<Transform>()) if (t.name == "Hips") hips = t;
        float hipsY = hips != null ? hips.position.y - sitter.transform.position.y : -1f;
        bool sitState = anim.GetCurrentAnimatorStateInfo(0).IsName("Sit");
        // the same model standing (rest pose of the rig) for comparison
        float standY = -1f;
        foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(cc, FindObjectsSortMode.None))
        {
            Component c = (Component)o;
            if (c == sitter || (bool)Get(o, "InSeatFlow")) continue;
            foreach (Transform t in c.GetComponentsInChildren<Transform>())
                if (t.name == "Hips" && t.gameObject.activeInHierarchy) { standY = t.position.y - c.transform.position.y; break; }
            if (standY > 0f) break;
        }
        string where = seat.transform.parent != null ? seat.transform.parent.name : "?";
        Debug.Log($"[TEST] sitting: {sitter.name} on {seat.name} ({where}), off {off:0.00} m, hips {hipsY:0.00} m (standing {standY:0.00} m), Sit state {sitState}, seats {seats.Count}");
        Assert.Less(off, 0.05f, "not on the stool");
        Assert.IsTrue(sitState, "Sit animation not playing");
        Assert.IsTrue(hipsY > 0.4f && (standY < 0f || hipsY < standY - 0.2f), $"hips at {hipsY:0.00} m (standing {standY:0.00} m): not sitting on a stool");
        Assert.IsTrue(sitter.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled == false, "agent should be off while seated");

        // they get up after a while and walk off down the soi
        t0 = Time.realtimeSinceStartup;
        while (sitter != null && (bool)Get(sitter, "InSeatFlow") && Time.realtimeSinceStartup - t0 < 60f) yield return null;
        Assert.IsTrue(sitter == null || !(bool)Get(sitter, "InSeatFlow"), "the customer never left the seat");
        if (sitter != null)
        {
            Assert.IsTrue(sitter.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled, "agent not back on after leaving the seat");
            Assert.IsNull(Get(seat, "taken"), "seat still reserved");
        }
        UnityEngine.Object.Destroy(b.gameObject);
        Time.timeScale = 1f;
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator DecorateHireAndExpand()
    {
        yield return LoadScene();
        Type dsT = T("DecorShop");
        Component ds = (Component)dsT.GetProperty("Instance").GetValue(null);
        Assert.IsNotNull(ds, "no DecorShop");
        Component gm = Find("GameManager");
        FieldInfo moneyF = gm.GetType().GetField("money");
        moneyF.SetValue(gm, 20000);

        // the computer has a decorate tab with every item listed
        Type panelT = T("OrderPanel");
        Component panel = (Component)panelT.GetProperty("Instance").GetValue(null);
        panelT.GetMethod("Open").Invoke(panel, null);
        panelT.GetMethod("ShowDecorTab").Invoke(panel, null);
        yield return null;
        Assert.IsTrue(((GameObject)panelT.GetField("decorGroup").GetValue(panel)).activeInHierarchy, "decorate tab not shown");
        var labels = (TMPro.TMP_Text[])panelT.GetField("decorLabels").GetValue(panel);
        Assert.IsNotEmpty(labels[6].text, "expansion not listed");
        Assert.IsNotEmpty(labels[7].text, "no 'arrange the shop' button");
        panelT.GetMethod("Close").Invoke(panel, null);

        var items = (System.Collections.IList)dsT.GetField("items").GetValue(ds);
        GameObject pharmacy = null;
        foreach (object it in items) if ((string)it.GetType().GetField("id").GetValue(it) == "expand") pharmacy = (GameObject)it.GetType().GetField("hide").GetValue(it);
        Assert.IsNotNull(pharmacy, "expansion does not know which neighbour to replace");
        int spentFrom = (int)moneyF.GetValue(gm);
        foreach (object it in items)
        {
            string id = (string)it.GetType().GetField("id").GetValue(it);
            Assert.IsTrue((bool)dsT.GetMethod("Buy").Invoke(ds, new object[] { id }), "could not buy " + id);
            GameObject shown = (GameObject)it.GetType().GetField("show").GetValue(it);
            Assert.IsTrue(shown != null && shown.activeInHierarchy, id + " did not appear");
        }
        int spent = spentFrom - (int)moneyF.GetValue(gm);
        Assert.IsFalse(pharmacy.activeSelf, "the pharmacy is still there after expanding");
        int beauty = (int)dsT.GetProperty("Beauty").GetValue(ds);
        float spawn = (float)dsT.GetProperty("SpawnMultiplier").GetValue(ds);

        // the helper puts pearls on by himself
        Component pot = (Component)T("BobaPot").GetProperty("Instance").GetValue(null);
        Time.timeScale = 3f;
        float t0 = Time.realtimeSinceStartup;
        while (pot.GetType().GetProperty("Current").GetValue(pot).ToString() == "Empty" && Time.realtimeSinceStartup - t0 < 20f) yield return null;
        Time.timeScale = 1f;
        string potState = pot.GetType().GetProperty("Current").GetValue(pot).ToString();

        // wage at closing, and everything is saved
        int before = (int)moneyF.GetValue(gm);
        Component sp = Find("ShopProgress");
        sp.GetType().GetMethod("FinalizeDay").Invoke(sp, null);
        int after = (int)moneyF.GetValue(gm);
        string[] owned = (string[])dsT.GetMethod("OwnedIds").Invoke(ds, null);
        Debug.Log($"[TEST] decor: spent {spent}, beauty {beauty}, customers x{spawn:0.00}, helper pot {potState}, wage {before - after}, owned {owned.Length}");
        Assert.AreNotEqual("Empty", potState, "helper did not cook pearls");
        int wages = (int)dsT.GetField("helperWage").GetValue(ds) + (int)dsT.GetProperty("BaristaWage").GetValue(ds);
        Assert.AreEqual(wages, before - after, "staff wages not paid at closing");
        Assert.AreEqual(items.Count, owned.Length, "purchases not in save data");
        Assert.Greater(spawn, 1.3f, "decor/expansion did not bring more customers");
    }

    [UnityTest, Timeout(150000)]
    public IEnumerator RegularVisitsAndBecomesAFriend()
    {
        yield return LoadScene();
        Type rsT = T("RegularsSystem");
        Component rs = (Component)rsT.GetProperty("Instance").GetValue(null);
        Assert.IsNotNull(rs, "no RegularsSystem");
        var list = (System.Collections.IList)rsT.GetField("regulars").GetValue(rs);
        object mind = list[0];
        Type regT = mind.GetType();
        rsT.GetMethod("ForceDue").Invoke(rs, new object[] { 0 });

        // she should come in, with her usual order and her own look
        Type cc = T("CustomerController");
        FieldInfo regF = cc.GetField("regular");
        Component visitor = null;
        Time.timeScale = 3f;
        float t0 = Time.realtimeSinceStartup;
        while (visitor == null && Time.realtimeSinceStartup - t0 < 90f)
        {
            foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(cc, FindObjectsSortMode.None))
                if (regF.GetValue(o) == mind) visitor = (Component)o;
            yield return null;
        }
        Time.timeScale = 1f;
        Assert.IsNotNull(visitor, "the regular never showed up");
        yield return null;
        object order = cc.GetField("order").GetValue(visitor);
        Assert.AreEqual("ThaiTea", order.GetType().GetField("flavor").GetValue(order), "not her usual drink");
        Assert.AreEqual(2, order.GetType().GetField("bobaCount").GetValue(order));
        Transform student = visitor.transform.Find("Visual/NPC_Student");
        Assert.IsTrue(student != null && student.gameObject.activeSelf, "regular does not look like herself");
        string title = (string)cc.GetProperty("DisplayName").GetValue(visitor);

        // five perfect drinks -> best friends, secret recipe, saved
        object perfect = Enum.Parse(T("ServeQuality"), "Perfect");
        for (int i = 0; i < 5; i++) rsT.GetMethod("OnServed").Invoke(rs, new object[] { mind, perfect });
        int hearts = (int)regT.GetField("hearts").GetValue(mind);
        bool secret = (bool)rsT.GetProperty("SecretUnlocked").GetValue(null);
        Type dm = T("DrinkMenu");
        var unlocked = (System.Collections.IList)dm.GetMethod("Unlocked").Invoke(null, new object[] { 1 });
        bool hasSecret = false;
        foreach (object d in unlocked) if ((string)d.GetType().GetField("id").GetValue(d) == "ButterflyPea") hasSecret = true;
        int[] saved = (int[])rsT.GetMethod("HeartsArray").Invoke(rs, null);
        float bonus = (float)rsT.GetProperty("SpawnBonus").GetValue(rs);
        Debug.Log($"[TEST] regular: '{title}', hearts {hearts}, secret {secret}, menu has secret {hasSecret}, saved [{string.Join(",", saved)}], spawn bonus {bonus:0.00}");
        Assert.AreEqual(5, hearts);
        Assert.IsTrue(secret && hasSecret, "secret recipe not unlocked");
        Assert.AreEqual(5, saved[0], "friendship not in save data");
        Assert.Greater(bonus, 1f, "friends did not bring more customers");
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator PearlPotCooksThenGoesHard()
    {
        yield return LoadScene();
        Type potT = T("BobaPot");
        Component pot = (Component)potT.GetProperty("Instance").GetValue(null);
        Assert.IsNotNull(pot, "no pearl pot");
        PropertyInfo state = potT.GetProperty("Current");
        Assert.AreEqual("Empty", state.GetValue(pot).ToString(), "a new day should start with an empty pot");

        Component inv = Find("InventoryManager");
        int raw0 = (int)inv.GetType().GetField("boba").GetValue(inv);
        Component station = pot.GetComponent(T("IngredientStation"));
        station.GetType().GetMethod("Interact").Invoke(station, new object[] { Find("RaycastAim") });     // press E on the pot
        Assert.AreEqual("Cooking", state.GetValue(pot).ToString(), "pressing the pot did not start cooking");
        int raw1 = (int)inv.GetType().GetField("boba").GetValue(inv);
        Assert.Less(raw1, raw0, "cooking did not use raw pearls");

        Time.timeScale = 5f;
        float t0 = Time.realtimeSinceStartup;
        while (state.GetValue(pot).ToString() == "Cooking" && Time.realtimeSinceStartup - t0 < 20f) yield return null;
        Time.timeScale = 1f;
        Assert.AreEqual("Fresh", state.GetValue(pot).ToString(), "pearls never finished cooking");
        int portions = (int)potT.GetProperty("Portions").GetValue(pot);

        object[] args = { false };
        Assert.IsTrue((bool)potT.GetMethod("TryScoop").Invoke(pot, args));
        Assert.IsFalse((bool)args[0], "fresh pearls reported as hard");
        potT.GetMethod("ForceState").Invoke(pot, new object[] { Enum.Parse(potT.GetNestedType("State"), "Stale"), 10 });
        args = new object[] { false };
        potT.GetMethod("TryScoop").Invoke(pot, args);
        Assert.IsTrue((bool)args[0], "hard pearls not flagged");

        // a cup with hard pearls is noticed when served
        Component dispenser = Find("CupDispenser");
        GameObject cupGo = UnityEngine.Object.Instantiate((GameObject)dispenser.GetType().GetField("cupPrefab").GetValue(dispenser), new Vector3(0, 30, 0), Quaternion.identity);
        cupGo.GetComponent<Rigidbody>().isKinematic = true;
        Component cup = cupGo.GetComponent(T("CupController"));
        Type ct = cup.GetType();
        ct.GetMethod("Grab").Invoke(cup, null);
        ct.GetMethod("AddBoba").Invoke(cup, new object[] { 1 });
        ct.GetField("stalePearls").SetValue(cup, true);
        ct.GetMethod("Fill").Invoke(cup, new object[] { "ThaiTea" });
        ct.GetMethod("CloseLid").Invoke(cup, null);
        object order = Activator.CreateInstance(T("Order"), new object[] { 1, false, "ThaiTea", 100 });
        object res = T("ServeResult").GetMethod("Evaluate").Invoke(null, new object[] { order, cup });
        string q = res.GetType().GetField("quality").GetValue(res).ToString();

        // next morning the pot is empty again
        Component clock = Find("TimeCyc");
        clock.GetType().GetMethod("AdvanceToNextDay").Invoke(clock, null);
        yield return null;
        Debug.Log($"[TEST] pot: raw {raw0} -> {raw1}, cooked {portions} portions, hard-pearl drink = {q}, next day pot = {state.GetValue(pot)}");
        Assert.AreEqual("Okay", q, "hard pearls were not noticed");
        Assert.AreEqual("Empty", state.GetValue(pot).ToString(), "pot not emptied overnight");
        UnityEngine.Object.Destroy(cupGo);
    }

    [UnityTest, Timeout(60000)]
    public IEnumerator ButtonPopupsReplaceEachOtherImmediately()
    {
        yield return LoadScene();
        Type hudT = T("ShopHUD");
        Component hud = Find("ShopHUD");
        var toastText = (TMPro.TMP_Text)hudT.GetField("toastText").GetValue(hud);
        hudT.GetMethod("Toast").Invoke(null, new object[] { "queued message" });
        yield return null;
        Assert.IsNotEmpty(toastText.text, "no popup showing to replace");      // the day's event (or the queued one) is up
        // pressing E (sweetness, ice...) must not wait for whatever popup is showing to close
        hudT.GetMethod("ToastNow").Invoke(null, new object[] { "sweetness 25%", 1.6f });
        yield return null;
        Assert.AreEqual("sweetness 25%", toastText.text, "popup waited instead of replacing");
        hudT.GetMethod("ToastNow").Invoke(null, new object[] { "sweetness 50%", 1.6f });
        yield return null;
        Debug.Log("[TEST] popup now shows: " + toastText.text);
        Assert.AreEqual("sweetness 50%", toastText.text, "second press did not update the popup right away");
    }

    [UnityTest, Timeout(60000)]
    public IEnumerator CheeseFoamFromTheFridge()
    {
        yield return LoadScene();
        Component fridge = null;
        foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(T("IngredientStation"), FindObjectsSortMode.None))
            if (((Component)o).GetType().GetField("kind").GetValue(o).ToString() == "Foam") fridge = (Component)o;
        Assert.IsNotNull(fridge, "the fridge is not a cheese foam station");

        Component dispenser = Find("CupDispenser");
        GameObject prefab = (GameObject)dispenser.GetType().GetField("cupPrefab").GetValue(dispenser);
        GameObject cupGo = UnityEngine.Object.Instantiate(prefab, new Vector3(0, 30f, 0), Quaternion.identity);
        cupGo.GetComponent<Rigidbody>().isKinematic = true;
        Component cup = cupGo.GetComponent(T("CupController"));
        Type ct = cup.GetType();
        ct.GetMethod("Grab").Invoke(cup, null);
        Assert.IsFalse((bool)ct.GetProperty("CanAddFoam").GetValue(cup), "foam allowed before the tea");
        ct.GetMethod("Fill").Invoke(cup, new object[] { "ThaiTea" });
        Assert.IsTrue((bool)ct.GetMethod("AddFoam").Invoke(cup, null), "could not add cheese foam");
        yield return null;
        Transform foam = null;
        foreach (Transform t in cupGo.GetComponentsInChildren<Transform>(true)) if (t.name == "Cup_Foam") foam = t;
        Assert.IsNotNull(foam, "cup model has no foam layer");
        Assert.IsTrue(foam.gameObject.activeInHierarchy, "foam layer not shown");
        ct.GetMethod("CloseLid").Invoke(cup, null);

        Type orderT = T("Order");
        object plain = Activator.CreateInstance(orderT, new object[] { 0, false, "ThaiTea", 100 });
        object withFoam = Activator.CreateInstance(orderT, new object[] { 0, false, "ThaiTea", 100 });
        orderT.GetField("wantsFoam").SetValue(withFoam, true);
        MethodInfo eval = T("ServeResult").GetMethod("Evaluate");
        object good = eval.Invoke(null, new object[] { withFoam, cup });
        object bad = eval.Invoke(null, new object[] { plain, cup });
        string q1 = good.GetType().GetField("quality").GetValue(good).ToString(), q2 = bad.GetType().GetField("quality").GetValue(bad).ToString();
        Debug.Log($"[TEST] foam: order with foam -> {q1}, order without foam -> {q2}; price {orderT.GetProperty("Price").GetValue(withFoam)} vs {orderT.GetProperty("Price").GetValue(plain)}");
        Assert.AreEqual("Perfect", q1);
        Assert.AreEqual("Okay", q2, "unwanted foam was not noticed");
        UnityEngine.Object.Destroy(cupGo);
    }

    // The bot makes a cheese-foam order end to end: walks to the fridge, aims at it, tops the cup, seals, serves
    [UnityTest, Timeout(180000)]
    public IEnumerator BotServesCheeseFoamOrder()
    {
        yield return LoadScene();
        Type sp = T("Spawner"), orderT = T("Order");
        object o = Activator.CreateInstance(orderT, new object[] { 1, true, "ThaiTea", 100 });
        orderT.GetField("wantsFoam").SetValue(o, true);
        sp.GetField("TutorialOrder").SetValue(null, o);                // reuse the tutorial's "one fixed customer" mode
        sp.GetField("TutorialCustomerSpawned").SetValue(null, false);
        sp.GetField("TutorialActive").SetValue(null, true);
        try
        {
            Type bot = Type.GetType("ThaiBobaAutoPlayer, Assembly-CSharp");
            bot.GetField("Passed").SetValue(null, null);
            Component b = new GameObject("AutoPlayer").AddComponent(bot);
            bot.GetField("customersToServe").SetValue(b, 1);
            bot.GetField("endDayWhenDone").SetValue(b, false);
            float t0 = Time.realtimeSinceStartup;
            while (bot.GetField("Passed").GetValue(null) == null && Time.realtimeSinceStartup - t0 < 150f) yield return null;
            string report = (string)bot.GetField("Report").GetValue(null);
            Debug.Log("[TEST] foam bot: " + report);
            Assert.AreEqual(true, bot.GetField("Passed").GetValue(null), "bot could not make the cheese foam order: " + report);
            UnityEngine.Object.Destroy(b.gameObject);
        }
        finally
        {
            sp.GetField("TutorialActive").SetValue(null, false);
        }
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator ComputerOrdersSupplies()
    {
        yield return LoadScene();
        Component pc = Find("SupplyComputer");
        Assert.IsNotNull(pc, "no shop computer in the scene");
        Component aim = Find("RaycastAim");
        pc.GetType().GetMethod("Interact").Invoke(pc, new object[] { aim });
        yield return null;
        Type panelType = T("OrderPanel");
        Assert.IsTrue((bool)panelType.GetProperty("IsOpen").GetValue(null), "order screen did not open");
        Assert.IsTrue((bool)Static("GameMenus", "IsOpen"), "cursor/menus not freed while ordering");

        Component gm = Find("GameManager");
        Component inv = Find("InventoryManager");
        FieldInfo moneyF = gm.GetType().GetField("money"), cupsF = inv.GetType().GetField("cups");
        int money0 = (int)moneyF.GetValue(gm), cups0 = (int)cupsF.GetValue(inv);
        Component panel = (Component)panelType.GetProperty("Instance").GetValue(null);
        var buttons = (UnityEngine.UI.Button[])panelType.GetField("orderButtons").GetValue(panel);
        buttons[0].onClick.Invoke();                                  // cups +10
        int money1 = (int)moneyF.GetValue(gm);
        float t0 = Time.time;
        while ((int)cupsF.GetValue(inv) == cups0 && Time.time - t0 < 15f) yield return null;
        int cups1 = (int)cupsF.GetValue(inv);
        Debug.Log($"[TEST] computer order: money {money0} -> {money1}, cups {cups0} -> {cups1}");
        Assert.Less(money1, money0, "ordering did not cost money");
        Assert.AreEqual(cups0 + 10, cups1, "ordered cups never arrived");

        panelType.GetMethod("Close").Invoke(panel, null);
        yield return null;
        Assert.IsFalse((bool)panelType.GetProperty("IsOpen").GetValue(null), "order screen did not close");
    }

    [UnityTest, Timeout(150000)]
    public IEnumerator NewDayStartsWithoutYesterdaysCustomers()
    {
        yield return LoadScene();
        Type cc = T("CustomerController");
        var queue = (System.Collections.IList)cc.GetField("Queue").GetValue(null);
        Time.timeScale = 3f;
        float t0 = Time.realtimeSinceStartup;
        while (queue.Count < 1 && Time.realtimeSinceStartup - t0 < 90f) yield return null;
        Time.timeScale = 1f;
        Assert.Greater(queue.Count, 0, "no customer showed up to test with");
        int before = UnityEngine.Object.FindObjectsByType(cc, FindObjectsSortMode.None).Length;

        Component gm = Find("GameManager");
        gm.GetType().GetMethod("EndDay").Invoke(gm, null);
        yield return null;
        yield return null;
        int after = UnityEngine.Object.FindObjectsByType(cc, FindObjectsSortMode.None).Length;
        Debug.Log($"[TEST] customers before new day {before}, after {after}, queue {queue.Count}");
        Assert.AreEqual(0, queue.Count, "yesterday's queue is still there");
        Assert.AreEqual(0, after, "yesterday's customers are still in the shop");
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator UpgradesShowAndCartStationsAreReachable()
    {
        yield return LoadScene();
        Transform player = Find("MovementWithJoy").transform;

        // aiming at the blender / marshmallow jar hits that station, not the cart body around it
        int carts = 0;
        foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(T("IngredientStation"), FindObjectsSortMode.None))
        {
            Component st = (Component)o;
            string k = st.GetType().GetField("kind").GetValue(st).ToString();
            if (k != "Blend" && k != "Marshmallow") continue;
            carts++;
            Bounds b = st.GetComponent<Renderer>().bounds;
            Vector3 from = new Vector3(0f, b.center.y + 0.4f, b.center.z);
            RaycastHit[] hits = Physics.RaycastAll(from, (b.center - from).normalized, 5f);
            Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            Collider first = null;
            foreach (RaycastHit h in hits) if (!h.collider.transform.IsChildOf(player)) { first = h.collider; break; }   // not the player's own capsule
            Assert.IsNotNull(first, k + ": nothing hit");
            Component got = first.GetComponentInParent(T("IngredientStation"));
            Assert.IsTrue(got == st, $"{k}: aiming at it hits {first.name} instead");
        }
        Assert.AreEqual(2, carts, "seasonal cart stations missing");

        // buy every upgrade: each one shows up somewhere
        Component gm = Find("GameManager");
        gm.GetType().GetField("money").SetValue(gm, 99999);
        Component shop = Find("UpgradeShop");
        var ups = (System.Collections.IList)shop.GetType().GetField("upgrades").GetValue(shop);
        Transform potBody = null;
        foreach (Transform t in Find("BobaPot").GetComponentsInChildren<Transform>(true)) if (t.name == "Pot_Body") potBody = t;
        float pot0 = potBody.lossyScale.x;
        foreach (object u in ups)
            Assert.IsTrue((bool)shop.GetType().GetMethod("Buy").Invoke(shop, new object[] { (string)u.GetType().GetField("id").GetValue(u) }));
        yield return null;
        var missing = new System.Collections.Generic.List<string>();
        foreach (object u in ups)
        {
            string id = (string)u.GetType().GetField("id").GetValue(u);
            var show = (GameObject[])u.GetType().GetField("enableWhenOwned").GetValue(u);
            if (id == "bigger_pot") continue;                                 // the pot grows instead
            if (show == null || show.Length == 0) { missing.Add(id + " (nothing)"); continue; }
            foreach (GameObject g in show) if (g == null || !g.activeInHierarchy) missing.Add(id);
        }
        float pot1 = potBody.lossyScale.x;

        // the day summary shows them as owned
        Component progress = Find("ShopProgress");
        progress.GetType().GetMethod("FinalizeDay").Invoke(progress, null);
        yield return null;
        GameObject panel = (GameObject)Find("ShopHUD").GetType().GetField("summaryPanel").GetValue(Find("ShopHUD"));
        Transform neonBtn = panel.transform.Find("Card/Upgrade_neon");
        string label = neonBtn.GetComponentInChildren<TMPro.TMP_Text>(true).text;
        bool clickable = neonBtn.GetComponent<UnityEngine.UI.Button>().interactable;
        TMPro.TMP_Text summary = (TMPro.TMP_Text)Find("ShopHUD").GetType().GetField("summaryText").GetValue(Find("ShopHUD"));
        summary.ForceMeshUpdate();
        Debug.Log($"[TEST] upgrades missing [{string.Join(", ", missing)}], pot {pot0:0.00} -> {pot1:0.00}, neon button '{label}' clickable {clickable}, summary fits {summary.preferredHeight:0}/{summary.rectTransform.rect.height:0} at {summary.fontSize:0}pt");
        Assert.IsEmpty(missing, "bought upgrades not visible");
        Assert.Greater(pot1, pot0 * 1.2f, "big pot did not grow");
        Assert.IsTrue(label.Contains("มีแล้ว"), "owned upgrade still shows a price: " + label);
        Assert.IsFalse(clickable, "owned upgrade can be bought again");
        Assert.IsFalse(summary.isTextOverflowing, "day summary runs out of its box");
    }

    // Busy shop: the line fills up (nobody serving), then a bot serves for a while. Nobody may stand in the road,
    // clump together, or be stuck walking; everyone in line reaches a spot; served customers walk away.
    [UnityTest, Timeout(300000)]
    public IEnumerator CustomersQueueWithoutPilingUp()
    {
        yield return LoadScene();
        Type cc = T("CustomerController");
        var slots = (Vector3[])cc.GetField("Slots").GetValue(null);
        Assert.IsNotNull(slots, "no queue line in the scene");
        foreach (Vector3 sl in slots)
            Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(sl, out UnityEngine.AI.NavMeshHit _, 0.25f, UnityEngine.AI.NavMesh.AllAreas), "queue spot off the walkable area: " + sl);
        Component spawner = Find("Spawner");
        spawner.GetType().GetField("spawnInterval").SetValue(spawner, 1.5f);
        PropertyInfo stateP = cc.GetProperty("StateName"), arrivedP = cc.GetProperty("ArrivedInLine"), inLineP = cc.GetProperty("InLine");
        var problems = new System.Collections.Generic.List<string>();
        var waitingSince = new System.Collections.Generic.Dictionary<UnityEngine.Object, float>();
        var stillSince = new System.Collections.Generic.Dictionary<UnityEngine.Object, float>();
        int maxQueue = 0, maxClump = 0, left = 0;
        cc.GetField("Hops").SetValue(null, 0);
        var seen = new System.Collections.Generic.HashSet<int>();
        var alive = new System.Collections.Generic.HashSet<int>();

        IEnumerator Watch(float gameSeconds, string phase)
        {
            float t0 = Time.time;
            while (Time.time - t0 < gameSeconds)
            {
                var all = UnityEngine.Object.FindObjectsByType(cc, FindObjectsSortMode.None);
                var now = new System.Collections.Generic.HashSet<int>();
                foreach (UnityEngine.Object o in all)
                {
                    Component c = (Component)o;
                    now.Add(c.GetInstanceID());
                    seen.Add(c.GetInstanceID());
                    Vector3 p = c.transform.position;
                    string st = (string)stateP.GetValue(c);
                    bool inLine = (bool)inLineP.GetValue(c), arrived = (bool)arrivedP.GetValue(c);
                    if (p.z > 3.05f && problems.Count < 12) problems.Add($"{phase}: customer in the road at {p} ({st})");
                    if (inLine && !arrived)
                    {
                        if (!waitingSince.ContainsKey(c)) waitingSince[c] = Time.time;
                        else if (Time.time - waitingSince[c] > 12f && problems.Count < 12) { problems.Add($"{phase}: never reached its spot in line ({p})"); waitingSince[c] = 1e9f; }
                    }
                    else waitingSince.Remove(c);
                    var ag = c.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    bool moving = ag != null && ag.velocity.sqrMagnitude > 0.02f;
                    bool shouldMove = st == "GoingToEnter" || st == "GoingToExit" || st == "GoingToWaypoints" || (inLine && !arrived);
                    if (shouldMove && !moving)
                    {
                        if (!stillSince.ContainsKey(c)) stillSince[c] = Time.time;
                        else if (Time.time - stillSince[c] > 3.5f && problems.Count < 12) { problems.Add($"{phase}: stuck while {st} at {p}"); stillSince[c] = 1e9f; }
                    }
                    else stillSince.Remove(c);
                    int near = 0;
                    foreach (UnityEngine.Object o2 in all)
                        if (o2 != o && Vector3.Distance(((Component)o2).transform.position, p) < 0.9f) near++;
                    maxClump = Mathf.Max(maxClump, near);
                }
                foreach (int id in alive) if (!now.Contains(id)) left++;
                alive = now;
                if (Time.frameCount % 600 == 0)
                {
                    var sb = new System.Text.StringBuilder($"[Q] t={Time.time - t0:0} {phase}:");
                    var qlist = (System.Collections.IList)cc.GetField("Queue").GetValue(null);
                    foreach (UnityEngine.Object o in all)
                    {
                        Component c = (Component)o;
                        var ag = c.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        sb.Append($" | id{c.GetInstanceID() % 10000} #{qlist.IndexOf(c)} {stateP.GetValue(c)} arr={arrivedP.GetValue(c)} pos=({c.transform.position.x:0.00},{c.transform.position.z:0.00}) " +
                                  $"dest=({ag.destination.x:0.00},{ag.destination.z:0.00}) v={ag.velocity.magnitude:0.00} rem={ag.remainingDistance:0.00} prio={ag.avoidancePriority}");
                    }
                    Debug.Log(sb.ToString());
                }
                maxQueue = Mathf.Max(maxQueue, ((System.Collections.IList)cc.GetField("Queue").GetValue(null)).Count);
                yield return null;
            }
        }
        Time.timeScale = 3f;
        yield return Watch(110f, "filling up");
        int leftBefore = left;
        Type bot = Type.GetType("ThaiBobaAutoPlayer, Assembly-CSharp");
        Component b = new GameObject("AutoPlayer").AddComponent(bot);
        bot.GetField("customersToServe").SetValue(b, 50);
        bot.GetField("endDayWhenDone").SetValue(b, false);
        bot.GetField("speedUp").SetValue(b, 3f);
        yield return Watch(120f, "serving");
        UnityEngine.Object.Destroy(b);
        Time.timeScale = 1f;
        Debug.Log($"[TEST] queue: hops {cc.GetField("Hops").GetValue(null)}, customers {seen.Count}, longest line {maxQueue}, most neighbours within 0.9 m {maxClump}, " +
                  $"walked off {left} ({left - leftBefore} while serving), problems [{string.Join(" | ", problems)}]");
        Assert.IsEmpty(problems);
        Assert.GreaterOrEqual(maxQueue, 4, "the line never filled up (test did not stress the queue)");
        Assert.LessOrEqual(maxClump, 3, "customers clumped together");
        Assert.Greater(left - leftBefore, 2, "served customers did not walk away");
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator VehiclesRollAndCarryPeople()
    {
        yield return LoadScene();
        Component spawner = Find("TrafficSpawner");
        var prefabs = (GameObject[])spawner.GetType().GetField("vehiclePrefabs").GetValue(spawner);
        var problems = new System.Collections.Generic.List<string>();
        GameObject taxiPrefab = null;
        foreach (GameObject p in prefabs)
        {
            int wheels = 0, riders = 0;
            foreach (Transform t in p.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Wheel_")) wheels++;
                if (t.name.StartsWith("SEAT_")) riders++;
            }
            if (p.GetComponent(T("WheelSpin")) == null || wheels < 2) problems.Add($"{p.name}: {wheels} wheels");
            int need = p.name.Contains("Songthaew") ? 3 : p.name.Contains("Scooter") || p.name.Contains("TukTuk") ? 1 : 0;
            if (riders < need) problems.Add($"{p.name}: {riders}/{need} riders");
            if (p.name.Contains("Taxi")) taxiPrefab = p;
        }
        Assert.IsNotNull(taxiPrefab, "no taxi in the traffic");

        // roll a taxi forward by hand: the top of each wheel must move ahead of its hub
        GameObject taxi = UnityEngine.Object.Instantiate(taxiPrefab, new Vector3(0, -40, 0), Quaternion.identity);
        UnityEngine.Object.Destroy(taxi.GetComponent(T("VehicleSound")));          // depends on TrafficCar: goes first
        yield return null;
        UnityEngine.Object.Destroy(taxi.GetComponent(T("TrafficCar")));
        yield return null;
        Transform wheel = null;
        foreach (Transform t in taxi.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Wheel_")) { wheel = t; break; }
        Renderer wr = wheel.GetComponent<Renderer>();
        Vector3 hub = wr.bounds.center;
        Vector3 topLocal = wheel.InverseTransformPoint(hub + Vector3.up * wr.bounds.extents.y);
        for (int i = 0; i < 6; i++)
        {
            taxi.transform.position += taxi.transform.forward * 0.04f;
            yield return null;
        }
        Vector3 topNow = wheel.TransformPoint(topLocal) - wr.bounds.center;
        float ahead = Vector3.Dot(topNow, taxi.transform.forward);
        Debug.Log($"[TEST] vehicles: {prefabs.Length} prefabs, problems [{string.Join(", ", problems)}], wheel top moved {ahead:0.000} m ahead");
        UnityEngine.Object.Destroy(taxi);
        Assert.IsEmpty(problems);
        Assert.Greater(ahead, 0.01f, "wheels roll backwards (or not at all)");
    }

    [UnityTest, Timeout(60000)]
    public IEnumerator DrinkSloshesAndSettles()
    {
        yield return LoadScene();
        Component dispenser = Find("CupDispenser");
        GameObject prefab = (GameObject)dispenser.GetType().GetField("cupPrefab").GetValue(dispenser);
        GameObject cup = UnityEngine.Object.Instantiate(prefab, new Vector3(0, 30f, 0), Quaternion.identity);
        try
        {
            Rigidbody rb = cup.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            Type sloshType = T("LiquidSlosh");
            Component slosh = cup.GetComponent(sloshType);
            Assert.IsNotNull(slosh, "cup prefab has no LiquidSlosh");
            Renderer liquid = null;
            foreach (Renderer r in cup.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("Liquid_")) { liquid = r; break; }
            Assert.IsNotNull(liquid, "cup has no liquid mesh");
            Assert.AreEqual("ThaiBoba/LiquidWobble", liquid.sharedMaterial.shader.name, "liquid does not use the slosh shader");
            PropertyInfo slope = sloshType.GetProperty("CurrentSlope");

            // one visible pearl group per scoop
            Component cc = cup.GetComponent(T("CupController"));
            cc.GetType().GetMethod("Grab").Invoke(cc, null);
            cc.GetType().GetMethod("AddBoba").Invoke(cc, new object[] { 1 });
            cc.GetType().GetMethod("AddBoba").Invoke(cc, new object[] { 1 });
            yield return null;
            int groups = 0;
            foreach (Transform t in cup.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Cup_Pearls")) groups++;
            Debug.Log($"[TEST] pearl groups visible after 2 scoops: {groups}");
            Assert.AreEqual(2, groups, "pearls in the cup do not match the scoops");

            yield return null;
            // walk with the cup at 3 m/s for 0.4 s, then stop dead (time based, so frame rate does not matter)
            float peak = 0f, tm = 0f;
            while (tm < 0.4f)
            {
                cup.transform.position += new Vector3(3f * Time.deltaTime, 0, 0);
                tm += Time.deltaTime;
                yield return null;
                peak = Mathf.Max(peak, ((Vector2)slope.GetValue(slosh)).magnitude);
            }
            tm = 0f;
            while (tm < 0.6f)
            {
                tm += Time.deltaTime;
                yield return null;
                peak = Mathf.Max(peak, ((Vector2)slope.GetValue(slosh)).magnitude);
            }
            // then hold it still and let it settle
            float t0 = Time.time;
            while (Time.time - t0 < 4f) yield return null;
            float rest = ((Vector2)slope.GetValue(slosh)).magnitude;
            Debug.Log($"[TEST] slosh peak slope {peak:0.00}, after 4 s {rest:0.000}");
            Assert.Greater(peak, 0.08f, "liquid did not react to the cup moving");
            Assert.Less(rest, 0.03f, "liquid never settled");
        }
        finally
        {
            UnityEngine.Object.Destroy(cup);
        }
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator ServesCustomersAndEndsDay()
    {
        yield return LoadScene();   // loads the street and presses "เริ่มเกมใหม่"

        Type bot = Type.GetType("ThaiBobaAutoPlayer, Assembly-CSharp");
        Assert.IsNotNull(bot, "ThaiBobaAutoPlayer not found");
        new GameObject("AutoPlayer").AddComponent(bot);

        FieldInfo passedField = bot.GetField("Passed", BindingFlags.Public | BindingFlags.Static);
        float start = Time.realtimeSinceStartup;
        while (passedField.GetValue(null) == null && Time.realtimeSinceStartup - start < 240f)
            yield return null;

        object result = passedField.GetValue(null);
        string report = (string)bot.GetField("Report", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Debug.Log("[TEST] " + report);
        Assert.IsNotNull(result, "bot timed out before serving all customers");
        Assert.IsTrue((bool)result, report);
    }

    // ======================================================================== the living soi (phase 2)

    // Topping bar: konjac / pandan jelly / pudding; graded like everything else, vegetarian customers refuse dairy + egg
    [UnityTest, Timeout(60000)]
    public IEnumerator ToppingsAndVegetarianOrders()
    {
        yield return LoadScene();
        int tubs = 0;
        foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(T("IngredientStation"), FindObjectsSortMode.None))
            if (Get(o, "kind").ToString() == "Topping") tubs++;
        Assert.AreEqual(3, tubs, "the topping bar should have three tubs");

        object pudding = NewOrder("ThaiTea", 1, true, 50);
        Set(pudding, "topping", "Pudding");
        GameObject cup = MakeCup(pudding);
        yield return null;
        bool puddingShown = false, jellyShown = false;
        foreach (Transform t in cup.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Cup_Pudding") puddingShown = t.gameObject.activeInHierarchy;
            if (t.name == "Cup_Jelly") jellyShown = t.gameObject.activeInHierarchy;
        }
        Assert.IsTrue(puddingShown, "pudding not shown in the cup");
        Assert.IsFalse(jellyShown, "jelly shown although the cup has pudding");
        Assert.AreEqual("Perfect", Quality(pudding, cup));
        object jelly = NewOrder("ThaiTea", 1, true, 50);
        Set(jelly, "topping", "Jelly");
        Assert.AreEqual("Okay", Quality(jelly, cup), "wrong topping not noticed");
        Assert.AreEqual(65, (int)Get(pudding, "Price"), "pudding should cost +15");

        object veg = NewOrder("Matcha", 0, true, 50);
        Set(veg, "vegetarian", true);
        GameObject foamy = MakeCup(veg, extraFoam: true);
        object res = Call(T("ServeResult"), "Evaluate", veg, foamy.GetComponent(T("CupController")));
        string summary = (string)Get(res, "Summary");
        Assert.AreEqual("Okay", Get(res, "quality").ToString());
        StringAssert.Contains("เจ", summary, "foam for a vegetarian customer not called out");

        // customers ask for toppings once they're unlocked; on the vegetarian festival many eat เจ
        Set(Find("ShopProgress"), "level", 6);
        object reviewer = Call(T("CustomerProfiles"), "Get", Enum.Parse(T("CustomerType"), "Influencer"));
        int withTopping = 0;
        for (int i = 0; i < 80; i++)
            if (!string.IsNullOrEmpty((string)Get(Call(T("CustomerProfiles"), "CreateOrder", reviewer, 6, false), "topping"))) withTopping++;
        Component events = Inst("DailyEventSystem");
        Call(events, "RollForDay", 19);
        object student = Call(T("CustomerProfiles"), "Get", Enum.Parse(T("CustomerType"), "Student"));
        int jay = 0, broken = 0;
        for (int i = 0; i < 80; i++)
        {
            object o = Call(T("CustomerProfiles"), "CreateOrder", student, 6, false);
            if (!(bool)Get(o, "vegetarian")) continue;
            jay++;
            if ((bool)Get(o, "wantsFoam") || (string)Get(o, "topping") == "Pudding") broken++;
        }
        Debug.Log($"[TEST] toppings: reviewer orders with topping {withTopping}/80, vegetarian orders {jay}/80 ({broken} with dairy/egg)");
        Assert.Greater(withTopping, 15, "customers never order toppings");
        Assert.Greater(jay, 25, "vegetarian festival has no vegetarian customers");
        Assert.AreEqual(0, broken, "a vegetarian ordered cheese foam or pudding");
        UnityEngine.Object.Destroy(cup);
        UnityEngine.Object.Destroy(foamy);
    }

    // App delivery: ticket pops up, wrong cups are refused, the rider rides up and takes the bag
    [UnityTest, Timeout(120000)]
    public IEnumerator DeliveryOrderPickedUpByRider()
    {
        yield return LoadScene();
        Component gm = Find("GameManager");
        Set(gm, "money", 5000);
        Assert.IsTrue((bool)Call(Inst("UpgradeShop"), "Buy", "delivery"), "could not buy the delivery upgrade");
        Component ds = Inst("DeliverySystem");
        Assert.IsTrue((bool)Get(ds, "Enabled"));
        object ticket = Call(ds, "NewTicket", 2);
        var items = (System.Collections.IList)Get(ticket, "items");
        Assert.AreEqual(2, items.Count);
        yield return null;
        Assert.IsTrue(((GameObject)Get(ds, "card")).activeInHierarchy, "the delivery ticket card is not on the HUD");

        Component aim = Find("RaycastAim");
        GameObject wrong = MakeCup(NewOrder("ButterflyPea", 0, true, 100));
        Call(aim, "HoldItem", wrong.transform);
        Assert.IsFalse((bool)Call(ds, "PlaceCup", aim), "a drink that isn't on the order was accepted");
        UnityEngine.Object.Destroy(wrong);
        yield return null;

        int moneyBefore = (int)Get(gm, "money");
        foreach (object it in items)
        {
            GameObject c = MakeCup(Get(it, "order"));
            Call(aim, "HoldItem", c.transform);
            Assert.IsTrue((bool)Call(ds, "PlaceCup", aim), "the rack refused a correct drink");
            yield return null;
        }
        Time.timeScale = 3f;
        bool riderSeen = false;
        float t0 = Time.realtimeSinceStartup;
        while (Get(ds, "Current") != null && Time.realtimeSinceStartup - t0 < 45f)
        {
            riderSeen |= ((Component)Get(ds, "rider")).gameObject.activeInHierarchy;
            yield return null;
        }
        Time.timeScale = 1f;
        object today = Get(Find("ShopProgress"), "today");
        int money = (int)Get(gm, "money");
        Debug.Log($"[TEST] delivery: picked up {Get(ds, "Current") == null}, rider seen {riderSeen}, money +{money - moneyBefore}, deliveries {Get(today, "deliveries")}");
        Assert.IsNull(Get(ds, "Current"), "the rider never picked the order up");
        Assert.IsTrue(riderSeen, "no rider came to the shop");
        Assert.AreEqual(1, (int)Get(today, "deliveries"));
        Assert.Greater(money, moneyBefore, "the delivery wasn't paid");
    }

    // The barista takes the 2nd customer in line and serves them while the front one waits
    [UnityTest, Timeout(150000)]
    public IEnumerator BaristaServesTheNextCustomer()
    {
        yield return LoadScene();
        Set(Find("GameManager"), "money", 10000);
        Component shop = Inst("DecorShop");
        Assert.IsTrue((bool)Call(shop, "Buy", "barista"), "could not hire the barista");
        Component barista = Inst("BaristaStaff");
        yield return null;
        Assert.IsTrue(((GameObject)Get(barista, "body")).activeInHierarchy, "the barista is not at the counter");
        Component spawner = Find("Spawner");
        Set(spawner, "spawnInterval", 1.5f);
        Time.timeScale = 3f;
        object today = Get(Find("ShopProgress"), "today");
        bool sawWork = false, sawShake = false;
        float t0 = Time.realtimeSinceStartup;
        while ((int)Get(today, "staffDrinks") < 1 && Time.realtimeSinceStartup - t0 < 100f)
        {
            object working = Get(barista, "Working");
            if (working != null)
            {
                sawWork = true;
                object mine = T("CustomerController").GetProperty("PlayerFront").GetValue(null);
                Assert.AreNotSame(working, mine, "the barista took the player's own customer (the one on the order card)");
                Component drv = ((Component)barista).GetComponentInChildren(T("NPCAnimDriver"), true);
                if (drv != null && Get(drv, "Current").ToString() == "Shake") sawShake = true;
            }
            yield return null;
        }
        Time.timeScale = 1f;
        int made = (int)Get(today, "staffDrinks");
        Debug.Log($"[TEST] barista: worked {sawWork}, shaking {sawShake}, drinks served {made}, level {Get(barista, "Level")}");
        Assert.IsTrue(sawWork, "the barista never took an order");
        Assert.IsTrue(sawShake, "the barista doesn't shake the drink");
        Assert.GreaterOrEqual(made, 1, "the barista never served anyone");
        Assert.IsTrue((bool)Call(shop, "TrainBarista"), "training failed");
        Assert.AreEqual(2, (int)Get(barista, "Level"));
    }

    // A rival opens across the street, pulls customers, and perfect drinks (with a promo) beat it
    [UnityTest, Timeout(60000)]
    public IEnumerator RivalOpensAndIsBeaten()
    {
        yield return LoadScene();
        Component rival = Inst("RivalShop");
        GameObject closed = (GameObject)Get(rival, "closedFront"), open = (GameObject)Get(rival, "rivalFront");
        Assert.IsNotNull(closed, "the empty shophouse opposite isn't linked");
        Assert.IsTrue(closed.activeInHierarchy && !open.activeInHierarchy, "the rival is there from the start");
        Component sp = Find("ShopProgress");
        float before = (float)Get(sp, "SpawnRateMultiplier");
        Call(rival, "OpenUp");
        yield return null;
        Assert.IsTrue((bool)Get(rival, "Open"));
        Assert.IsTrue(open.activeInHierarchy && !closed.activeInHierarchy, "the rival's shopfront didn't appear");
        Assert.Less((float)Get(sp, "SpawnRateMultiplier"), before, "the rival doesn't pull customers away");
        Component gm = Find("GameManager");
        Set(gm, "money", 1000);
        Assert.IsTrue((bool)Call(rival, "BuyPromo"), "promo failed");
        Assert.AreEqual(1000 - (int)Get(rival, "promoCost"), (int)Get(gm, "money"));
        object perfect = Result("Perfect");
        int n = 0;
        while ((bool)Get(rival, "Open") && n++ < 60) Call(rival, "Served", null, perfect);
        yield return null;
        object life = Get(T("StatsSystem"), "Life");
        Debug.Log($"[TEST] rival: beaten after {n} perfect drinks, wins {Get(life, "rivalWins")}");
        Assert.IsFalse((bool)Get(rival, "Open"), "perfect drinks never beat the rival");
        Assert.IsTrue(closed.activeInHierarchy && !open.activeInHierarchy, "the rival's shop didn't close");
        Assert.AreEqual(1, (int)Get(life, "rivalWins"));
        Assert.IsTrue((bool)Call(T("StatsSystem"), "Has", "rival_win"), "no medal for beating the rival");
    }

    // Chinese New Year, Mother's Day and the vegetarian festival: their days, decor, firecrackers and ang pao
    [UnityTest, Timeout(60000)]
    public IEnumerator NewFestivalsHaveDecorAndMoments()
    {
        yield return LoadScene();
        Component ev = Inst("DailyEventSystem");
        (int day, string type, string decor)[] fests = { (29, "ChineseNewYear", "chineseNewYearDecor"), (17, "MothersDay", "mothersDayDecor"), (19, "Vegetarian", "vegetarianDecor") };
        foreach (var f in fests)
        {
            Call(ev, "RollForDay", f.day);
            yield return null;
            Assert.AreEqual(f.type, Get(Get(ev, "Current"), "type").ToString(), $"day {f.day}");
            GameObject d = (GameObject)Get(ev, f.decor);
            Assert.IsNotNull(d, f.decor + " missing");
            Assert.IsTrue(d.activeInHierarchy, f.type + " decor not shown");
            Assert.IsTrue((bool)Call(T("DailyEventSystem"), "IsFestival", Get(Get(ev, "Current"), "type")));
        }
        Assert.IsFalse(((GameObject)Get(ev, "chineseNewYearDecor")).activeInHierarchy, "old festival decor stays up");
        Call(ev, "RollForDay", 29);
        Component fx = Find("FestivalFX");
        Call(fx, "Fire");
        ParticleSystem sparks = (ParticleSystem)Get(fx, "firecrackerSparks");
        Assert.IsTrue(sparks != null && sparks.isPlaying, "no firecracker sparks");
        Set(fx, "angPaoChance", 1f);
        Component gm = Find("GameManager");
        int money = (int)Get(gm, "money");
        Call(fx, "Served", null, Result("Perfect"));
        object today = Get(Find("ShopProgress"), "today");
        Assert.AreEqual(1, (int)Get(today, "angPao"), "no ang pao on Chinese New Year");
        Assert.Greater((int)Get(gm, "money"), money);
    }

    // Next door: the chicken-rice stall opens in the morning, has riders at lunch, closes its shutter after
    [UnityTest, Timeout(60000)]
    public IEnumerator NeighbourKeepsShopHours()
    {
        yield return LoadScene();
        Component clock = Find("TimeCyc");
        Component stall = Find("NeighbourStall");
        Assert.IsNotNull(stall, "no neighbour stall");
        Call(clock, "SetTime", 12, 0);
        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue((bool)Get(stall, "IsOpen"), "closed at noon");
        Assert.IsTrue(((GameObject)Get(stall, "vendor")).activeInHierarchy, "no vendor at the stall");
        Assert.IsFalse(((GameObject)Get(stall, "tarp")).activeInHierarchy, "the tarp is on while open");
        Assert.IsTrue(((GameObject[])Get(stall, "lunchRiders"))[0].activeInHierarchy, "no riders waiting at lunch");
        Call(clock, "SetTime", 16, 0);
        yield return new WaitForSeconds(4f);
        Transform shutter = (Transform)Get(stall, "shutterDoor");
        Assert.IsFalse((bool)Get(stall, "IsOpen"), "still open in the afternoon");
        Assert.IsTrue(shutter.gameObject.activeInHierarchy && shutter.localScale.y > 0.9f, "the shutter didn't come down");
        Assert.IsTrue(((GameObject)Get(stall, "tarp")).activeInHierarchy, "no tarp over the closed stall");
        Assert.IsFalse(((GameObject[])Get(stall, "lunchRiders"))[0].activeInHierarchy, "riders still waiting after closing");
        Call(clock, "SetTime", 9, 0);
        yield return new WaitForSeconds(4f);
        Assert.IsTrue((bool)Get(stall, "IsOpen"));
        Assert.IsFalse(shutter.gameObject.activeInHierarchy, "the shutter didn't roll up");
    }

    // Stray cats wander the footpath and can be petted
    [UnityTest, Timeout(90000)]
    public IEnumerator StrayCatsWanderAndCanBePetted()
    {
        yield return LoadScene();
        UnityEngine.Object[] cats = UnityEngine.Object.FindObjectsByType(T("StrayCat"), FindObjectsSortMode.None);
        Assert.AreEqual(3, cats.Length, "expected three stray cats");
        var start = new Vector3[cats.Length];
        for (int i = 0; i < cats.Length; i++) start[i] = ((Component)cats[i]).transform.position;
        Time.timeScale = 4f;
        float moved = 0f, t0 = Time.realtimeSinceStartup;
        while (moved < 1f && Time.realtimeSinceStartup - t0 < 30f)
        {
            for (int i = 0; i < cats.Length; i++) moved = Mathf.Max(moved, Vector3.Distance(start[i], ((Component)cats[i]).transform.position));
            yield return null;
        }
        Time.timeScale = 1f;
        foreach (UnityEngine.Object c in cats)
        {
            Vector3 p = ((Component)c).transform.position;
            Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(p, out UnityEngine.AI.NavMeshHit _, 0.4f, UnityEngine.AI.NavMesh.AllAreas), "a cat left the walkable area: " + p);
        }
        Call(cats[0], "Interact", Find("RaycastAim"));
        object life = Get(T("StatsSystem"), "Life");
        Debug.Log($"[TEST] cats: furthest walk {moved:0.0} m, petted {Get(life, "catsPetted")}");
        Assert.Greater(moved, 1f, "the cats never move");
        Assert.AreEqual(1, (int)Get(life, "catsPetted"));
    }

    // After dark vehicles light the road; on a wet night lamps, signs and headlights reflect on the asphalt
    [UnityTest, Timeout(60000)]
    public IEnumerator NightHeadlightsAndWetReflections()
    {
        yield return LoadScene();
        Component weather = Find("WeatherSystem");
        try
        {
            Component spawner = Find("TrafficSpawner");
            GameObject taxi = null;
            foreach (GameObject p in (GameObject[])Get(spawner, "vehiclePrefabs")) if (p.name.Contains("Taxi")) taxi = p;
            Assert.IsNotNull(taxi.GetComponent(T("VehicleLights")), "vehicles have no lights");
            var heads = (Vector3[])Get(taxi.GetComponent(T("VehicleLights")), "headlights");
            Assert.AreEqual(2, heads.Length, "taxi headlights not found on the model");
            GameObject car = UnityEngine.Object.Instantiate(taxi, new Vector3(0f, 0f, 5.8f), Quaternion.Euler(0f, 90f, 0f));
            Call(Find("TimeCyc"), "SetTime", 21, 0);
            Call(weather, "Force", 1f, 1f, false);
            for (int i = 0; i < 5; i++) yield return null;
            Component lights = car.GetComponent(T("VehicleLights"));
            Assert.IsTrue(((Renderer)Get(lights, "pool")).enabled, "no light pool on the road at night");
            var streaks = (Renderer[])Get(lights, "streaks");
            Assert.IsTrue(streaks.Length > 0 && streaks[0].enabled, "no headlight reflections on the wet road");
            Component refl = Find("NightReflections");
            var lampStreaks = (Renderer[])Get(refl, "streaks");
            int on = 0;
            foreach (Renderer r in lampStreaks) if (r != null && r.enabled) on++;
            Assert.Greater(on, 4, "lamps and signs don't reflect on the wet street");
            Call(Find("TimeCyc"), "SetTime", 12, 0);
            Call(weather, "Force", 0f, 0f, false);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(((Renderer)Get(lights, "pool")).enabled, "headlights still on at noon");
            Debug.Log($"[TEST] night: {lampStreaks.Length} reflection sources, {on} lit on the wet night");
            UnityEngine.Object.Destroy(car);
        }
        finally { Call(weather, "Force", -1f, -1f, false); }
    }

    // Story: chapters advance on their goals, the rival arrives in chapter 3, the ending opens branch 2; it's saved
    [UnityTest, Timeout(60000)]
    public IEnumerator StoryChaptersToTheSecondBranch()
    {
        yield return LoadScene();
        Component story = Inst("StoryMode");
        Assert.AreEqual(0, (int)Get(story, "ChapterIndex"), "a new game should start the story at chapter 1");
        Component gm = Find("GameManager");
        Component sp = Find("ShopProgress");
        object st = Get(story, "S");
        Set(st, "served", 40);
        Set(st, "profit", 2000);
        Set(sp, "reputation", 3.6f);
        int money = (int)Get(gm, "money");
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.AreEqual(1, (int)Get(story, "ChapterIndex"), "chapter 1 did not complete");
        Assert.AreEqual(money + 300, (int)Get(gm, "money"), "no chapter reward");

        Type stateT = T("StoryMode").GetNestedType("State");
        object s2 = Activator.CreateInstance(stateT);
        Set(s2, "chapter", 2);
        Set(s2, "seenIntro", true);
        Call(story, "Load", s2);
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.IsTrue((bool)Get(Inst("RivalShop"), "Open"), "chapter 3 should bring the rival");

        object s4 = Activator.CreateInstance(stateT);
        Set(s4, "chapter", 4);
        Set(s4, "seenIntro", true);
        Call(story, "Load", s4);
        Set(gm, "money", 16000);
        Assert.IsTrue((bool)Get(story, "CanOpenBranch"), "can't open branch 2 with the money saved");
        Assert.IsTrue((bool)Call(story, "OpenBranch"));
        Assert.IsTrue(((GameObject)Get(Inst("RivalShop"), "branchFront")).activeInHierarchy, "branch 2 doesn't appear across the street");
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.IsTrue((bool)Get(story, "Finished"), "the story didn't end after opening branch 2");
        Call(story, "ShowPanel");
        yield return null;
        Assert.IsTrue((bool)Get(T("StoryMode"), "PanelOpen"));
        Assert.IsNotEmpty(((TMPro.TMP_Text)Get(story, "titleText")).text);
        Call(story, "ClosePanel");

        // saved with the game
        Call(T("SaveSystem"), "SaveGame");
        Call(story, "Load", Activator.CreateInstance(stateT));
        Call(T("SaveSystem"), "LoadGame");
        Assert.IsTrue((bool)Get(story, "Finished"), "story progress not saved");
        Assert.IsTrue((bool)Get(Inst("RivalShop"), "BranchOpen"), "branch 2 not saved");
        Assert.AreEqual(1, (int)Get(Get(T("StatsSystem"), "Life"), "storyDone"));
    }

    // Lifetime stats + medals, and the page that shows them
    [UnityTest, Timeout(60000)]
    public IEnumerator StatsAndMedalsPage()
    {
        yield return LoadScene();
        Call(Find("ShopProgress"), "RegisterServe", null, Result("Perfect"), 45, 5);
        object life = Get(T("StatsSystem"), "Life");
        Assert.AreEqual(1, (int)Get(life, "served"));
        Assert.IsTrue((bool)Call(T("StatsSystem"), "Has", "first_cup"), "no medal for the first cup");
        Component menus = Inst("GameMenus");
        Call(menus, "OpenStats");
        yield return null;
        Transform grid = (Transform)Get(menus, "medalGrid");
        int cells = 0;
        foreach (Transform t in grid) if (t.gameObject.activeSelf) cells++;
        int medals = ((Array)Get(T("StatsSystem"), "Medals")).Length;
        string text = ((TMPro.TMP_Text)Get(menus, "statsText")).text;
        Debug.Log($"[TEST] stats: {cells} medal cells of {medals}; page text {text.Length} chars");
        Assert.AreEqual(medals, cells, "not every medal is listed");
        StringAssert.Contains("เสิร์ฟ", text);
        Call(menus, "Back");
    }

    // Arrange mode: a plant can't go on the queue line, can go on free footpath, and the layout is saved
    [UnityTest, Timeout(60000)]
    public IEnumerator ArrangeDecorationsAndSaveTheLayout()
    {
        yield return LoadScene();
        Component dp = Inst("DecorPlacement");
        object plant = Call(dp, "Find", "plant_l");
        Assert.IsNotNull(plant, "the potted plant is not movable");
        var line = (Vector3[])Get(T("CustomerController"), "Slots");
        Assert.IsFalse((bool)Call(dp, "IsValid", plant, line[3]), "a plant may be put on the queue line");
        Vector3 free = new Vector3(-5.6f, line[0].y, 2.0f);                      // footpath right of the neighbour's stall
        Assert.IsTrue((bool)Call(dp, "IsValid", plant, free), "free footpath refused: " + free);
        Call(dp, "Begin");
        Assert.IsTrue((bool)Get(T("DecorPlacement"), "Active"));
        Call(dp, "End");
        Type placedT = T("DecorPlacement").GetNestedType("Placed");
        Array layout = Array.CreateInstance(placedT, 1);
        object pl = Activator.CreateInstance(placedT);
        Set(pl, "id", "plant_l"); Set(pl, "x", free.x); Set(pl, "z", free.z); Set(pl, "yaw", 30f);
        layout.SetValue(pl, 0);
        Call(dp, "Load", layout);
        Transform root = (Transform)Get(plant, "root");
        Assert.Less(Vector3.Distance(new Vector3(root.position.x, 0, root.position.z), new Vector3(free.x, 0, free.z)), 0.01f);
        Call(T("SaveSystem"), "SaveGame");
        Call(dp, "Load", new object[] { null });
        Assert.Greater(Vector3.Distance(root.position, new Vector3(free.x, root.position.y, free.z)), 0.5f, "Load(null) should reset the layout");
        Call(T("SaveSystem"), "LoadGame");
        Assert.Less(Vector3.Distance(new Vector3(root.position.x, 0, root.position.z), new Vector3(free.x, 0, free.z)), 0.01f, "layout not saved");
    }

    // Rain only over the open street (never inside the shops), the wet street reflects, cats keep their heads on
    [UnityTest, Timeout(120000)]
    public IEnumerator RainStaysOutsideAndTheStreetReflects()
    {
        yield return LoadScene();
        Component weather = Find("WeatherSystem");
        int quality = PlayerPrefs.GetInt("tb_quality", 2);
        try
        {
            Set(T("GameSettings"), "Quality", 2);
            Call(weather, "Force", 1f, 1f, false);
            ParticleSystem rain = (ParticleSystem)Get(weather, "rain");
            var parts = new ParticleSystem.Particle[4000];
            int total = 0, overShops = 0, onFootpath = 0, atJunction = 0;
            Vector3 worst = Vector3.zero;
            // rain follows the camera: look from the middle of the soi, then from the junction at the street end
            Type follow = T("RainFollow");
            GameObject probe = new GameObject("RainProbeCam");
            Camera probeCam = probe.AddComponent<Camera>();
            probeCam.enabled = false;
            follow.GetField("ViewOverride").SetValue(null, probeCam);
            foreach (Vector3 at in new[] { new Vector3(0f, 1.6f, 6f), new Vector3(-45.5f, 1.6f, 6f) })
            {
                probe.transform.position = at;
                rain.Clear();
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 3f)
                {
                    int n = rain.GetParticles(parts);
                    bool local = rain.main.simulationSpace == ParticleSystemSimulationSpace.Local;
                    for (int i = 0; i < n; i++)
                    {
                        if (parts[i].startLifetime - parts[i].remainingLifetime < 0.1f) continue;   // just born: the trigger kills it next update
                        Vector3 w = local ? rain.transform.TransformPoint(parts[i].position) : parts[i].position;
                        total++;
                        bool inRows = Mathf.Abs(w.x) < 41.4f && (w.z < 1.0f || w.z > 12.5f);   // our row (z < 1) / the far row (z > 12.5)
                        if (inRows) { overShops++; worst = w; }
                        if (Mathf.Abs(w.x) < 30f && w.z > 1.3f && w.z < 2.9f) onFootpath++;   // our footpath, past the awning
                        if (w.x < -41.6f && w.x > -49.6f && w.z > 0f && w.z < 12f) atJunction++;
                    }
                    yield return null;
                }
            }
            follow.GetField("ViewOverride").SetValue(null, null);
            UnityEngine.Object.Destroy(probe);
            Debug.Log($"[TEST] rain coverage: footpath {onFootpath}, junction {atJunction}");
            Assert.Greater(onFootpath, 50, "no rain on the open footpath");
            Assert.Greater(atJunction, 50, "no rain at the junction at the street end");
            Component planar = Find("PlanarReflection");
            Assert.IsNotNull(planar, "no planar reflection in the scene");
            bool gpu = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            bool reflecting = gpu ? (bool)Get(planar, "Rendering") : (bool)Get(planar, "Active");
            var sheets = (Renderer[])Get(planar, "surfaces");
            Debug.Log($"[TEST] rain: {total} drop samples, {overShops} over the shops (e.g. {worst}); reflection on {reflecting}, sheets {sheets.Length}");
            Assert.Greater(total, 500, "no rain falling");
            Assert.AreEqual(0, overShops, "rain falls inside / over the shops: " + worst);
            Assert.IsTrue(reflecting, "the wet street doesn't reflect");
            Assert.IsTrue(sheets.Length >= 3, "no reflection sheets on the road / footpaths");
            if (gpu)
            {
                Assert.IsNotNull(Get(planar, "Texture"), "no reflection texture");
                Assert.IsTrue(sheets[0].enabled, "reflection sheets not shown on the wet road");
            }

            Set(T("GameSettings"), "Quality", 0);
            yield return null;
            yield return null;
            Assert.IsFalse((bool)Get(planar, "Rendering") || (bool)Get(planar, "Active"), "reflection still on with Low graphics");
            Set(T("GameSettings"), "Quality", 2);
            Call(weather, "Force", 0f, 0f, false);
            yield return new WaitForSecondsRealtime(4f);
            Assert.IsFalse((bool)Get(planar, "Rendering") || (bool)Get(planar, "Active"), "reflection still on for a dry street");
        }
        finally
        {
            Call(weather, "Force", -1f, -1f, false);
            Set(T("GameSettings"), "Quality", quality);
        }

        // cats: head (and legs) ride on the body whatever the pose
        Component cat = (Component)UnityEngine.Object.FindFirstObjectByType(T("StrayCat"));
        Transform head = null, body = null;
        foreach (Transform t in cat.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Cat_Head")) head = t;
            if (t.name.StartsWith("Cat_Body")) body = t;
        }
        Assert.IsTrue(head != null && body != null && head.IsChildOf(body), "the cat's head isn't attached to its body");
        Vector3 rest = body.InverseTransformPoint(head.position);
        float drift = 0f;
        Time.timeScale = 4f;
        float t1 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t1 < 10f)
        {
            drift = Mathf.Max(drift, Vector3.Distance(body.InverseTransformPoint(head.position), rest));
            yield return null;
        }
        Time.timeScale = 1f;
        Debug.Log($"[TEST] cat: head drift from the body {drift * 100f:0.0} cm");
        Assert.Less(drift, 0.06f, "the cat's head drifts away from its body");
    }

    // Festival days: passers-by carry the festival's things and small groups celebrate on the far footpath
    [UnityTest, Timeout(90000)]
    public IEnumerator FestivalCrowdsCelebrate()
    {
        yield return LoadScene();
        Component ev = Inst("DailyEventSystem");
        Component clock = Find("TimeCyc");
        Call(clock, "SetTime", 12, 0);
        Call(ev, "RollForDay", 10);                                           // Songkran
        Assert.AreEqual("Songkran", Get(Get(ev, "Current"), "type").ToString());
        yield return null;
        yield return null;
        Component crowds = Inst("FestivalActivities");
        Assert.IsNotNull(crowds, "no festival crowds in the scene");
        int people = (int)Get(crowds, "PeopleOut");
        Debug.Log($"[TEST] festival: {people} people celebrating");
        Assert.GreaterOrEqual(people, 4, "nobody gathers for the festival");
        // they hold water guns / bowls and splash each other
        int carrying = 0;
        foreach (Component d in ((Component)crowds).GetComponentsInChildren(T("NPCAnimDriver")))
        {
            string c = (string)Get(d, "Carrying");
            if (c == "watergun" || c == "bowl") carrying++;
        }
        Assert.GreaterOrEqual(carrying, 4, "festival goers carry nothing");
        bool waved = false;
        float t0 = Time.realtimeSinceStartup;
        while (!waved && Time.realtimeSinceStartup - t0 < 8f)
        {
            foreach (Component d in ((Component)crowds).GetComponentsInChildren(T("NPCAnimDriver")))
                if (Get(d, "Current").ToString() == "Wave") waved = true;
            yield return null;
        }
        Assert.IsTrue(waved, "nobody splashes water");
        Assert.IsNotNull(Call(T("FestivalActivities"), "MakeProp", "krathong"), "no krathong prop");
        // passers-by pick festival props too
        int picked = 0;
        for (int i = 0; i < 50; i++) if (Call(T("FestivalActivities"), "PickCarry") != null) picked++;
        Assert.Greater(picked, 10, "passers-by never carry festival things");
        // an ordinary day: everyone goes home
        for (int day = 2; day < 9; day++)
        {
            Call(ev, "RollForDay", day);
            if (!(bool)Call(T("DailyEventSystem"), "IsFestival", Get(Get(ev, "Current"), "type"))) break;
        }
        yield return null;
        yield return null;
        Assert.AreEqual(0, (int)Get(crowds, "PeopleOut"), "festival crowd stays after the festival");

        // UI size setting: bigger UI = smaller reference resolution on the screen-space canvases
        Type gs = T("GameSettings");
        float before = (float)gs.GetProperty("UIScale").GetValue(null);
        try
        {
            gs.GetProperty("UIScale").SetValue(null, 1f);
            UnityEngine.UI.CanvasScaler hud = null;
            foreach (UnityEngine.UI.CanvasScaler cs in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.CanvasScaler>(FindObjectsSortMode.None))
                if (cs.GetComponent<Canvas>().renderMode != RenderMode.WorldSpace) { hud = cs; break; }
            Assert.IsNotNull(hud, "no screen canvas");
            Vector2 baseRes = hud.referenceResolution;
            gs.GetProperty("UIScale").SetValue(null, 1.25f);
            Assert.AreEqual(baseRes.x / 1.25f, hud.referenceResolution.x, 1f, "UI size setting does not scale the HUD");
            Assert.IsNotNull(Get(Find("GameMenus"), "uiScaleSlider"), "no UI size slider in the settings");
        }
        finally { gs.GetProperty("UIScale").SetValue(null, before); }
    }
}
