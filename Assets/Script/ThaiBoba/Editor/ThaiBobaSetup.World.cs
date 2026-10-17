using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Street ends: T-junctions with side roads that run into dark tunnels; traffic routes; audio.
public static partial class ThaiBobaSetup
{
    // Blender-space lane layout (Unity = P(x, y, z)); traffic keeps left like in Thailand
    const float NearLaneY = -5.75f;     // main street, heading +X (Blender)
    const float FarLaneY = -8.4f;       // main street, heading -X
    const float CrossX = 45.5f;         // side roads at x = ±45.5
    const float TunnelNorthY = 18f, TunnelSouthY = -30f;
    const float DeepNorth = 34f, DeepSouth = -46f;   // where vehicles appear / vanish inside the tunnels

    static void BuildJunctions(Transform env, Transform back)
    {
        foreach (int side in new[] { -1, 1 })
        {
            float cx = side * CrossX;
            for (float y = TunnelSouthY + 2f; y <= TunnelNorthY - 2f; y += 4f)
                NoWalk(Place("SM_RoadTile", cx, y, -0.15f, 90, env, Col.Box));
            Place("SM_TunnelPortal", cx, TunnelNorthY, -0.15f, 0, back, Col.Mesh);
            Place("SM_TunnelPortal", cx, TunnelSouthY, -0.15f, 180, back, Col.Mesh);
            float outerX = side * 49.6f, innerX = side * 41.6f;
            for (float y = TunnelSouthY + 4f; y <= TunnelNorthY - 4f; y += 8f)
                Place("SM_RetainingWall", outerX, y, -0.15f, side > 0 ? -90 : 90, back, Col.Box);
            foreach (float y in new[] { 7f, 15f, -18f, -26f })
                Place("SM_RetainingWall", innerX, y, -0.15f, side > 0 ? 90 : -90, back, Col.Box);
        }
    }

    static List<Vector3> Pts(params (float x, float y)[] bl)
    {
        var l = new List<Vector3>();
        foreach (var (x, y) in bl) l.Add(P(x, y, -0.15f));
        return l;
    }

    // A -> corner -> B as a smooth quadratic curve
    static List<Vector3> Turn((float x, float y) a, (float x, float y) c, (float x, float y) b, int steps = 7)
    {
        var l = new List<Vector3>();
        Vector3 A = P(a.x, a.y, -0.15f), C = P(c.x, c.y, -0.15f), B = P(b.x, b.y, -0.15f);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            l.Add((1 - t) * (1 - t) * A + 2 * (1 - t) * t * C + t * t * B);
        }
        return l;
    }

    static TrafficRoute Route(Transform parent, string name, float weight, bool passesShop, params List<Vector3>[] parts)
    {
        var all = new List<Vector3>();
        foreach (List<Vector3> p in parts)
            foreach (Vector3 v in p)
                if (all.Count == 0 || (all[all.Count - 1] - v).sqrMagnitude > 0.0001f) all.Add(v);
        GameObject go = new GameObject("Route_" + name);
        go.transform.SetParent(parent, false);
        TrafficRoute r = go.AddComponent<TrafficRoute>();
        r.points = all.ToArray();
        r.weight = weight;
        r.passesShop = passesShop;
        return r;
    }

    static TrafficRoute[] BuildRoutes(Transform parent)
    {
        float E = CrossX, W = -CrossX;
        float eN = E - 1.5f, eS = E + 1.5f;     // east side road: northbound lane / southbound lane
        float wN = W - 1.5f, wS = W + 1.5f;     // west side road
        // entries onto the near lane (heading +X) from the west side road
        var entryNear = new[]
        {
            ("WN", Pts((wS, DeepNorth), (wS, NearLaneY + 4f)), Turn((wS, NearLaneY + 4f), (wS, NearLaneY), (wS + 4f, NearLaneY))),
            ("WS", Pts((wN, DeepSouth), (wN, NearLaneY - 4f)), Turn((wN, NearLaneY - 4f), (wN, NearLaneY), (wN + 4f, NearLaneY))),
        };
        var exitNear = new[]
        {
            ("EN", Turn((eN - 4f, NearLaneY), (eN, NearLaneY), (eN, NearLaneY + 4f)), Pts((eN, NearLaneY + 4f), (eN, DeepNorth))),
            ("ES", Turn((eS - 4f, NearLaneY), (eS, NearLaneY), (eS, NearLaneY - 4f)), Pts((eS, NearLaneY - 4f), (eS, DeepSouth))),
        };
        // entries onto the far lane (heading -X) from the east side road
        var entryFar = new[]
        {
            ("EN", Pts((eS, DeepNorth), (eS, FarLaneY + 4f)), Turn((eS, FarLaneY + 4f), (eS, FarLaneY), (eS - 4f, FarLaneY))),
            ("ES", Pts((eN, DeepSouth), (eN, FarLaneY - 4f)), Turn((eN, FarLaneY - 4f), (eN, FarLaneY), (eN - 4f, FarLaneY))),
        };
        var exitFar = new[]
        {
            ("WS", Turn((wS + 4f, FarLaneY), (wS, FarLaneY), (wS, FarLaneY - 4f)), Pts((wS, FarLaneY - 4f), (wS, DeepSouth))),
            ("WN", Turn((wN + 4f, FarLaneY), (wN, FarLaneY), (wN, FarLaneY + 4f)), Pts((wN, FarLaneY + 4f), (wN, DeepNorth))),
        };
        var routes = new List<TrafficRoute>();
        foreach (var en in entryNear)
            foreach (var ex in exitNear)
                routes.Add(Route(parent, en.Item1 + "_to_" + ex.Item1, 1f, true, en.Item2, en.Item3, ex.Item2, ex.Item3));
        foreach (var en in entryFar)
            foreach (var ex in exitFar)
                routes.Add(Route(parent, en.Item1 + "_to_" + ex.Item1, 1f, true, en.Item2, en.Item3, ex.Item2, ex.Item3));
        // straight through on the side roads (tunnel to tunnel)
        routes.Add(Route(parent, "E_south", 0.35f, false, Pts((eS, DeepNorth), (eS, DeepSouth))));
        routes.Add(Route(parent, "E_north", 0.35f, false, Pts((eN, DeepSouth), (eN, DeepNorth))));
        routes.Add(Route(parent, "W_south", 0.35f, false, Pts((wS, DeepNorth), (wS, DeepSouth))));
        routes.Add(Route(parent, "W_north", 0.35f, false, Pts((wN, DeepSouth), (wN, DeepNorth))));
        return routes.ToArray();
    }

    // ------------------------------------------------------------------ neighbours at night
    // Windows + signs of the other shophouses and condos glow after dark; each open shop lights its footpath.
    public static void BuildShopGlow(StreetRefs refs)
    {
        var renderers = new List<Renderer>();
        var lights = new List<Light>();
        Color[] tints = { new Color(1f, 0.8f, 0.55f), new Color(0.92f, 0.95f, 1f), new Color(1f, 0.88f, 0.7f) };
        int k = 0;
        foreach (Transform t in refs.root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            bool shop = n.StartsWith("SM_Shop_") || n.StartsWith("SM_Shophouse_Closed");
            if (!shop && !n.StartsWith("SM_Condo")) continue;
            if (PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject) != t.gameObject) continue;
            renderers.AddRange(t.GetComponentsInChildren<Renderer>(true));
            if (!n.StartsWith("SM_Shop_") || n.Contains("Closed")) continue;
            GameObject lg = new GameObject("ShopfrontLight");
            lg.transform.SetParent(t, false);
            lg.transform.localPosition = P(0, -0.9f, 2.7f);            // under the awning, in front of the glass
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 7f;
            l.intensity = 2.6f;
            l.color = tints[k++ % tints.Length];
            l.shadows = LightShadows.None;
            l.enabled = false;
            lights.Add(l);
        }
        ShopNightGlow glow = refs.root.AddComponent<ShopNightGlow>();
        glow.renderers = renderers.ToArray();
        glow.shopLights = lights.ToArray();
        Debug.Log($"[ThaiBoba] shop night glow: {renderers.Count} renderers, {lights.Count} shopfront lights");
    }

    // ------------------------------------------------------------------ weather
    public static void BuildWeather(StreetRefs refs, FxSet fx)
    {
        Transform root = refs.root.transform;
        WeatherSystem weather = Object.FindFirstObjectByType<WeatherSystem>();
        Transform wx = new GameObject("Weather").transform;
        wx.SetParent(root, false);

        // rain falls over the open street (awning edge of our row, z 1.2, to the far row's awnings, z 12.2);
        // the shops and the covered footpath stay dry. Splashes hit the same strip.
        GameObject rain = (GameObject)PrefabUtility.InstantiatePrefab(fx.rain, wx);
        rain.transform.localPosition = new Vector3(0, 16f, 6.7f);
        GameObject splash = (GameObject)PrefabUtility.InstantiatePrefab(fx.rainSplash, wx);
        splash.transform.localPosition = new Vector3(0, 0.02f, 6.7f);

        // lightning: a cold directional flash
        GameObject lg = new GameObject("LightningFlash");
        lg.transform.SetParent(wx, false);
        lg.transform.rotation = Quaternion.Euler(60f, 140f, 0f);
        Light flash = lg.AddComponent<Light>();
        flash.type = LightType.Directional;
        flash.color = new Color(0.8f, 0.85f, 1f);
        flash.intensity = 0f;
        flash.shadows = LightShadows.None;
        flash.enabled = false;

        if (weather != null)
        {
            weather.rain = rain.GetComponent<ParticleSystem>();
            weather.rainSplash = splash.GetComponent<ParticleSystem>();
            weather.lightning = flash;
        }

        // clouds drifting high above the soi
        CloudLayer layer = wx.gameObject.AddComponent<CloudLayer>();
        var clouds = new List<Transform>();
        Random.State old = Random.state;
        Random.InitState(42);
        string[] kinds = { "SM_Cloud_A", "SM_Cloud_B", "SM_Cloud_C" };
        for (int i = 0; i < 22; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), d = Random.Range(25f, 150f);
            GameObject c = Place(kinds[i % 3], 0, 0, 0, Random.Range(0f, 360f), wx, Col.None);
            c.transform.position = new Vector3(Mathf.Cos(a) * d, Random.Range(55f, 80f), Mathf.Sin(a) * d);
            foreach (Renderer r in c.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            clouds.Add(c.transform);
        }
        Random.state = old;
        layer.clouds = clouds.ToArray();

        // real reflections on the wet street: a mirrored camera + transparent sheets on the road and both footpaths
        string reflMatPath = FxDir + "M_WetReflection.mat";
        Material reflMat = AssetDatabase.LoadAssetAtPath<Material>(reflMatPath);
        Shader reflShader = Shader.Find("ThaiBoba/WetReflection");
        if (reflMat == null && reflShader != null)
        {
            System.IO.Directory.CreateDirectory(FxDir);
            reflMat = new Material(reflShader);
            AssetDatabase.CreateAsset(reflMat, reflMatPath);              // the asset also keeps the shader in player builds
        }
        if (reflMat != null)
        {
            // asphalt, not a mirror: a darker, slightly blue reflection that fades out looking straight down
            reflMat.SetColor("_Tint", new Color(0.72f, 0.76f, 0.86f));
            reflMat.SetFloat("_Strength", 0.62f);
            reflMat.SetFloat("_Distort", 0.03f);
            EditorUtility.SetDirty(reflMat);
        }
        PlanarReflection planar = wx.gameObject.AddComponent<PlanarReflection>();
        planar.planeHeight = -0.15f;
        var sheets = new List<Renderer>();
        // (Unity z) road between the kerbs, our footpath, the far footpath; just above each surface
        foreach (var (name, z0, z1, y) in new[] { ("WetSheet_Road", 2.95f, 10.45f, -0.138f), ("WetSheet_NearPath", 0.05f, 2.9f, 0.012f), ("WetSheet_FarPath", 10.5f, 13.35f, 0.012f) })
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(wx, false);
            q.transform.localPosition = new Vector3(0f, y, (z0 + z1) * 0.5f);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);       // lying flat, facing up
            q.transform.localScale = new Vector3(84f, z1 - z0, 1f);
            q.layer = 4;                                                     // "Water": left out of its own reflection
            MeshRenderer mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = reflMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            NoWalk(q);
            sheets.Add(mr);
        }
        planar.surfaces = sheets.ToArray();

        // wet road / footpaths + puddles
        WetSurfaces wet = wx.gameObject.AddComponent<WetSurfaces>();
        var surfaces = new List<Renderer>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.transform.parent != null && r.transform.parent != root ? r.transform.parent.name + "/" + r.name : r.name;
            if (n.Contains("SM_RoadTile") || n.Contains("SM_Sidewalk")) surfaces.Add(r);
        }
        wet.surfaces = surfaces.ToArray();
        var puddles = new List<Transform>();
        var spots = new (float x, float y, string k)[]
        {
            (-3f, -1.9f, "SM_Puddle_A"), (4.2f, -1.2f, "SM_Puddle_B"), (12f, -2.1f, "SM_Puddle_A"), (-14f, -1.6f, "SM_Puddle_B"),
            (-24f, -2.2f, "SM_Puddle_A"), (26f, -1.4f, "SM_Puddle_B"), (1.5f, -4.0f, "SM_Puddle_A"), (-8f, -4.3f, "SM_Puddle_B"),
            (9f, -9.6f, "SM_Puddle_A"), (-18f, -9.2f, "SM_Puddle_B"), (19f, -4.6f, "SM_Puddle_A"), (-31f, -9.4f, "SM_Puddle_A"),
            (33f, -4.1f, "SM_Puddle_B"), (-2f, -11.6f, "SM_Puddle_A"), (15f, -12.2f, "SM_Puddle_B"), (-12f, -11.9f, "SM_Puddle_A"),
        };
        foreach (var (x, y, k) in spots)
        {
            float z = y < -3.1f && y > -10.3f ? -0.145f : 0.005f;           // road surface is 15 cm lower
            GameObject p = Place(k, x, y, z, Random.Range(0f, 180f), wx, Col.None);
            NoWalk(p);
            foreach (Renderer r in p.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            puddles.Add(p.transform);
        }
        wet.puddles = puddles.ToArray();
    }

    // ------------------------------------------------------------------ audio
    const string AudioDir = "Assets/Audio/ThaiBoba/";

    static AudioClip Clip(string name) =>
        AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + name + ".ogg") ?? AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + name + ".wav");

    public static GameAudio CreateAudio(StreetRefs refs)
    {
        GameObject go = new GameObject("GameAudio");
        GameAudio a = go.AddComponent<GameAudio>();
        void Add(Sfx id, float vol, float maxSec, params string[] clips)
        {
            var e = new GameAudio.Entry { id = id, volume = vol, maxSeconds = maxSec };
            var list = new List<AudioClip>();
            foreach (string c in clips) { AudioClip ac = Clip(c); if (ac != null) list.Add(ac); }
            e.clips = list.ToArray();
            a.effects.Add(e);
        }
        Add(Sfx.Click, 0.6f, 0, "ui_click");
        Add(Sfx.Open, 0.6f, 0, "ui_open");
        Add(Sfx.Close, 0.6f, 0, "ui_close");
        Add(Sfx.Confirm, 0.7f, 0, "ui_confirm");
        Add(Sfx.Error, 0.6f, 0, "ui_error");
        Add(Sfx.Cup, 0.6f, 0, "sfx_cup");
        Add(Sfx.Boba, 0.8f, 0, "sfx_boba");
        Add(Sfx.Ice, 0.8f, 0, "sfx_ice");
        Add(Sfx.Sweet, 0.6f, 0, "sfx_sweet");
        Add(Sfx.Pour, 0.7f, 1.3f, "sfx_pour");
        Add(Sfx.SealStart, 0.8f, 0, "sfx_seal_start");
        Add(Sfx.SealDone, 0.8f, 0, "sfx_seal_done");
        Add(Sfx.Trash, 0.7f, 0, "sfx_trash");
        Add(Sfx.Coins, 0.8f, 0, "sfx_coins");
        Add(Sfx.Tip, 0.6f, 0, "sfx_tip");
        Add(Sfx.Combo, 0.6f, 0, "sfx_combo");
        Add(Sfx.Angry, 0.6f, 0, "sfx_angry");
        Add(Sfx.Reject, 0.6f, 0, "sfx_reject");
        Add(Sfx.Bless, 0.7f, 0, "sfx_bless");
        Add(Sfx.LevelUp, 0.7f, 0, "jingle_levelup");
        Add(Sfx.Goal, 0.7f, 0, "jingle_goal");
        Add(Sfx.DayEnd, 0.7f, 0, "jingle_dayend");
        Add(Sfx.Start, 0.6f, 0, "jingle_start");
        Add(Sfx.Step, 0.35f, 0, "sfx_step0", "sfx_step1", "sfx_step2", "sfx_step3", "sfx_step4");
        Add(Sfx.Thunder, 1f, 0, "sfx_thunder");
        Add(Sfx.Camera, 0.8f, 0, "sfx_camera");
        // synthesized in ArtSource/Audio/synth_sfx.py
        Add(Sfx.Blender, 0.85f, 0, "sfx_blender");
        Add(Sfx.Meow, 0.75f, 0, "sfx_meow", "sfx_meow2");
        Add(Sfx.Chop, 0.6f, 0, "sfx_chop", "sfx_chop2");
        Add(Sfx.Firecracker, 1f, 0, "sfx_firecracker");
        Add(Sfx.AppOrder, 0.75f, 0, "sfx_app_order");
        Add(Sfx.Shake, 0.6f, 0, "sfx_shake");
        Add(Sfx.HornMoto, 0.7f, 0, "sfx_horn_moto");
        Add(Sfx.Medal, 0.75f, 0, "jingle_medal");
        Add(Sfx.Chapter, 0.75f, 0, "jingle_chapter");
        a.music = Clip("music_shop");
        a.trafficLoop = Clip("amb_traffic");
        a.rainLoop = Clip("amb_rain");
        a.boilingLoop = Clip("amb_boiling");
        a.birdsLoop = Clip("amb_birds");
        a.nightLoop = Clip("amb_night");
        a.marketLoop = Clip("amb_market");
        Transform pot = Marker("BoilingPot", refs.root.transform, 0.5f, 6.5f, 0);
        pot.localPosition += Vector3.up * 1.2f;
        a.boilingPoint = pot;
        a.crowdPoint = Marker("CrowdPoint", refs.root.transform, -1.2f, -1.0f, 0);
        a.crowdPoint.localPosition += Vector3.up * 1.4f;
        return a;
    }
}
