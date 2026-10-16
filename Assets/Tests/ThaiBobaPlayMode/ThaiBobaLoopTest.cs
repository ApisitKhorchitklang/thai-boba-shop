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
    }

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
}
