using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine.TextCore;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Tools > Thai Boba Shop: turns the Blender models in Assets/Models/ThaiBobaShop into a playable street.
public static partial class ThaiBobaSetup
{
    const string ModelDir = "Assets/Models/ThaiBobaShop/";
    const string PrefabDir = "Assets/Prefabs/ThaiBoba/";
    const string ScenePath = "Assets/Scenes/ThaiBobaStreet.unity";

    enum Col { None, Box, Mesh }

    // ------------------------------------------------------------------ prefabs
    [MenuItem("Tools/Thai Boba Shop/1. Create Cup + Customer Prefabs")]
    public static void CreatePrefabsMenu()
    {
        CreateCupPrefab();
        CreateCustomerPrefab();
    }

    public static GameObject CreateCupPrefab()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "Cup_Modular.fbx");
        if (model == null)
        {
            Debug.LogError("Cup_Modular.fbx not found in " + ModelDir);
            return null;
        }
        Directory.CreateDirectory(PrefabDir);

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = "Cup_ThaiBoba";
        go.tag = "Item";
        CapsuleCollider col = go.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.09f, 0);
        col.radius = 0.045f;
        col.height = 0.18f;
        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        go.AddComponent<CupController>();
        go.AddComponent<CupVisual>();
        // sloshing liquid (LiquidWobble shader); the material asset also keeps the shader in player builds
        string liquidMatPath = PrefabDir + "M_LiquidWobble.mat";
        Material liquidMat = AssetDatabase.LoadAssetAtPath<Material>(liquidMatPath);
        Shader liquidShader = Shader.Find("ThaiBoba/LiquidWobble");
        if (liquidMat == null && liquidShader != null)
        {
            liquidMat = new Material(liquidShader);
            AssetDatabase.CreateAsset(liquidMat, liquidMatPath);
        }
        if (liquidMat != null) go.AddComponent<LiquidSlosh>().liquidMaterial = liquidMat;
        else Debug.LogWarning("ThaiBoba/LiquidWobble shader not found: cups get no slosh");

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "Cup_ThaiBoba.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    public static GameObject CreateCustomerPrefab()
    {
        Directory.CreateDirectory(PrefabDir);
        GameObject root = new GameObject("Customer_ThaiBoba");
        CapsuleCollider cc = root.AddComponent<CapsuleCollider>();
        cc.center = new Vector3(0, 0.85f, 0);
        cc.radius = 0.3f;
        cc.height = 1.7f;
        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.25f;
        agent.height = 1.7f;
        agent.speed = 1.7f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        CustomerController controller = root.AddComponent<CustomerController>();
        EnsureUISprites();                                                      // new Illustrator PNGs must be Sprites before we load them
        CustomerEmote emote = root.AddComponent<CustomerEmote>();               // emote bubbles over their heads
        emote.love = S("emote_love"); emote.happy = S("emote_happy"); emote.meh = S("emote_meh");
        emote.angry = S("emote_angry"); emote.camera = S("emote_camera");
        AddNpcModels(root.transform, CustomerModels);

        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "PatienceBar";
        Object.DestroyImmediate(bar.GetComponent<Collider>());
        bar.transform.SetParent(root.transform, false);
        bar.transform.localPosition = new Vector3(0, 1.95f, 0);
        bar.transform.localScale = new Vector3(1f, 0.06f, 0.06f);

        GameObject speech = new GameObject("Speech");
        speech.transform.SetParent(root.transform, false);
        speech.transform.localPosition = new Vector3(0, 2.35f, 0);
        TextMeshPro tmp = speech.AddComponent<TextMeshPro>();
        tmp.fontSize = 2.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(3.2f, 1f);
        tmp.color = Color.white;
        tmp.text = "";
        TMP_FontAsset thai = EnsureThaiFont();
        if (thai != null) tmp.font = thai;
        ThaiText.Enable(tmp);

        controller.speechText = tmp;
        controller.patienceBar = bar.transform;
        controller.moodRenderer = bar.GetComponent<Renderer>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "Customer_ThaiBoba.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Rigged characters from BlenderSource/thai_npc.py (SEAT_* are their seated, static variants)
    const string CharDir = "Assets/Models/ThaiBobaShop/Characters/";
    const string AnimDir = "Assets/Prefabs/ThaiBoba/Anim/";
    static readonly string[] CustomerModels =
        { "NPC_Student", "NPC_SchoolBoy", "NPC_UniStudent", "NPC_Office", "NPC_Tourist", "NPC_Auntie", "NPC_Vendor",
          "NPC_Rider", "NPC_Influencer", "NPC_Grandpa" };                 // Grandpa = regular "Uncle Chai"
    static readonly string[] PedestrianModels =
        { "NPC_Student", "NPC_SchoolBoy", "NPC_UniStudent", "NPC_Office", "NPC_Tourist", "NPC_Auntie", "NPC_Vendor",
          "NPC_Rider", "NPC_Worker", "NPC_Jogger", "NPC_Grandpa", "NPC_Uncle" };

    // ------------------------------------------------------------------ traffic
    static readonly (string model, float weight)[] Vehicles =
    {
        ("SM_Car_Taxi_Pink", 3f), ("SM_Car_Taxi_GreenYellow", 3f), ("SM_Traffic_TukTuk", 2.5f),
        ("SM_Traffic_Scooter", 4f), ("SM_Car_Pickup", 2.5f), ("SM_Car_Songthaew", 1.5f), ("SM_Bus", 0.7f),
    };

    static FxSet currentFx;

    // A model that carries "<part>_LOD1" renderers (decimated copies from the Blender scripts) gets a LODGroup:
    // LOD0 = full detail, LOD1 = the decimated part + everything it does not replace (wheels, riders), then culled.
    static void SetupLods(GameObject go, float lod1At, float cullAt)
    {
        if (go == null) return;
        Renderer[] all = go.GetComponentsInChildren<Renderer>(true);
        var low = new List<Renderer>();
        var replaced = new HashSet<string>();
        foreach (Renderer r in all)
        {
            string b = r.name.Split('.')[0];
            if (b.EndsWith("_LOD1")) { low.Add(r); replaced.Add(b.Substring(0, b.Length - 5)); }
        }
        if (low.Count == 0) return;
        var high = new List<Renderer>();
        foreach (Renderer r in all)
        {
            if (r is ParticleSystemRenderer || low.Contains(r)) continue;
            high.Add(r);
            if (!replaced.Contains(r.name.Split('.')[0])) low.Add(r);
        }
        LODGroup g = go.GetComponent<LODGroup>();
        if (g == null) g = go.AddComponent<LODGroup>();
        g.SetLODs(new[] { new LOD(lod1At, high.ToArray()), new LOD(cullAt, low.ToArray()) });
        g.RecalculateBounds();
    }

    // seated characters (SEAT_*.fbx from thai_npc.py) for the vehicles' Seat_* markers
    static readonly Dictionary<string, string[]> SeatedRiders = new Dictionary<string, string[]>
    {
        { "SM_Traffic_Scooter", new[] { "SEAT_Rider" } },
        { "SM_Traffic_TukTuk", new[] { "SEAT_TukDriver" } },
        { "SM_Car_Songthaew", new[] { "SEAT_PassengerA", "SEAT_PassengerB", "SEAT_PassengerC" } },
    };

    // Puts the seated characters on a vehicle model: Seat_Driver gets the first, Seat_Passenger<k> the k+1-th
    static void SeatRiders(GameObject vehicle, string model)
    {
        if (!SeatedRiders.TryGetValue(model, out string[] who)) return;
        foreach (Transform t in vehicle.GetComponentsInChildren<Transform>(true))
        {
            string nm = t.name.Split('.')[0];                              // Blender adds .001 when names repeat across assets
            int k = nm == "Seat_Driver" ? 0 : nm.StartsWith("Seat_Passenger") && int.TryParse(nm.Substring(14), out int n) ? n : -1;
            if (k < 0 || k >= who.Length) continue;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CharDir + who[k] + ".fbx");
            if (asset == null) { Debug.LogWarning("Missing seated model " + who[k]); continue; }
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, t);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            Animator anim = inst.GetComponent<Animator>();
            if (anim != null) Object.DestroyImmediate(anim);
        }
    }

    static GameObject VehiclePrefab(string model)
    {
        Directory.CreateDirectory(PrefabDir + "Traffic/");
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + model + ".fbx");
        if (asset == null) return null;
        GameObject root = new GameObject("Traffic_" + model.Replace("SM_", ""));
        GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
        SeatRiders(body, model);
        SetupLods(body, 0.14f, 0.008f);
        root.AddComponent<TrafficCar>();
        root.AddComponent<WheelSpin>();                                    // wheels roll with the speed
        AddVehicleLights(root, model);                                     // headlights, tail lights, wet-road reflections
        // pass-by + horn: two-wheelers and tuk-tuks get the motorbike recordings, the rest the car one
        VehicleSound vs = root.AddComponent<VehicleSound>();
        // tuk-tuks: their own putt-putt pass-by and the tinny two-beep horn; scooters: a small meep; cars: real horns
        bool tuk = model.Contains("TukTuk"), scooter = model.Contains("Scooter");
        vs.passBy = tuk ? new[] { Clip("sfx_tuk_pass") } : scooter ? new[] { Clip("sfx_moto_pass"), Clip("sfx_moto_pass2") } : new[] { Clip("sfx_car_pass") };
        vs.horns = tuk ? new[] { Clip("sfx_horn_tuk"), Clip("sfx_horn_tuk2") } : scooter ? new[] { Clip("sfx_horn_moto") } : new[] { Clip("sfx_horn_car"), Clip("sfx_horn_short") };
        vs.pitch = model.Contains("Bus") ? 0.72f : model.Contains("Songthaew") || model.Contains("Pickup") ? 0.9f : 1f;
        vs.hornChance = model.Contains("TukTuk") || model.Contains("Taxi") ? 0.2f : 0.1f;
        vs.volume = model.Contains("Bus") ? 0.95f : 0.8f;
        if (currentFx != null && currentFx.exhaust != null)
        {
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (Renderer rr in root.GetComponentsInChildren<Renderer>()) b.Encapsulate(rr.bounds);
            AddChildFx(currentFx.exhaust, root.transform, new Vector3(b.extents.x * 0.4f, 0.35f, b.min.z));
        }
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "Traffic/" + root.name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Two lanes along the soi, driving on the left (near lane toward -X, far lane toward +X in Unity)
    public static TrafficSpawner BuildTraffic(Transform parent)
    {
        Transform t = new GameObject("Traffic").transform;
        t.SetParent(parent, false);
        TrafficSpawner spawner = t.gameObject.AddComponent<TrafficSpawner>();
        spawner.routes = BuildRoutes(t);
        var prefabs = new List<GameObject>();
        var weights = new List<float>();
        foreach (var (model, w) in Vehicles)
        {
            GameObject p = VehiclePrefab(model);
            if (p == null) continue;
            prefabs.Add(p);
            weights.Add(w);
        }
        spawner.vehiclePrefabs = prefabs.ToArray();
        spawner.weights = weights.ToArray();
        return spawner;
    }

    static readonly string[] GestureClips = { "Phone", "Watch", "Sip", "Angry", "Wave", "Chop", "Shake" };   // Gesture = index + 1
    static readonly HashSet<string> UpperBodyBones = new HashSet<string>
        { "Spine", "Chest", "Neck", "Head", "Shoulder.L", "Shoulder.R", "UpperArm.L", "UpperArm.R", "LowerArm.L", "LowerArm.R", "Hand.L", "Hand.R" };

    // Idle <-> Walk (Speed) on the base layer, upper-body gestures (Gesture int) on a masked second layer,
    // all from the clips inside the character's own FBX
    static UnityEditor.Animations.AnimatorController NpcController(string model)
    {
        Directory.CreateDirectory(AnimDir);
        string path = AnimDir + model + ".controller";
        AssetDatabase.DeleteAsset(path);
        AnimationClip walk = null, idle = null, sit = null;
        var gestures = new Dictionary<string, AnimationClip>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(CharDir + model + ".fbx"))
            if (o is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                if (clip.name == "Walk") walk = clip;
                else if (clip.name == "Idle") idle = clip;
                else if (clip.name == "Sit") sit = clip;
                else gestures[clip.name] = clip;
            }
        var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Gesture", AnimatorControllerParameterType.Int);
        var sm = ctrl.layers[0].stateMachine;
        var idleState = sm.AddState("Idle");
        idleState.motion = idle;
        var walkState = sm.AddState("Walk");
        walkState.motion = walk;
        sm.defaultState = idleState;
        var toWalk = idleState.AddTransition(walkState);
        toWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, 0.2f, "Speed");
        toWalk.hasExitTime = false;
        toWalk.duration = 0.15f;
        var toIdle = walkState.AddTransition(idleState);
        toIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, 0.2f, "Speed");
        toIdle.hasExitTime = false;
        toIdle.duration = 0.2f;
        if (walk == null || idle == null) Debug.LogWarning("Missing Walk/Idle clip in " + model);
        // customers sitting at the tables with their drink ("Sit" bool from NPCAnimDriver)
        ctrl.AddParameter("Sit", AnimatorControllerParameterType.Bool);
        if (sit != null)
        {
            var sitState = sm.AddState("Sit");
            sitState.motion = sit;
            var toSit = sm.AddAnyStateTransition(sitState);
            toSit.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Sit");
            toSit.hasExitTime = false;
            toSit.duration = 0.35f;
            toSit.canTransitionToSelf = false;
            var up = sitState.AddTransition(idleState);
            up.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "Sit");
            up.hasExitTime = false;
            up.duration = 0.35f;
        }

        // gesture layer: weight driven by NPCAnimDriver (0 when no gesture, so the empty state never overrides)
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CharDir + model + ".fbx");
        var mask = new AvatarMask { name = model + "_Upper" };
        Transform[] all = asset.GetComponentsInChildren<Transform>(true);
        mask.transformCount = all.Length;
        for (int i = 0; i < all.Length; i++)
        {
            mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(all[i], asset.transform));
            mask.SetTransformActive(i, UpperBodyBones.Contains(all[i].name));
        }
        AssetDatabase.AddObjectToAsset(mask, ctrl);
        var gsm = new UnityEditor.Animations.AnimatorStateMachine { name = "Gestures", hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(gsm, ctrl);
        var none = gsm.AddState("None");
        gsm.defaultState = none;
        for (int k = 0; k < GestureClips.Length; k++)
        {
            if (!gestures.TryGetValue(GestureClips[k], out AnimationClip clip)) continue;
            var st = gsm.AddState(GestureClips[k]);
            st.motion = clip;
            var go = gsm.AddAnyStateTransition(st);
            go.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, k + 1, "Gesture");
            go.hasExitTime = false;
            go.duration = 0.2f;
            go.canTransitionToSelf = false;
            var back = st.AddTransition(none);
            back.AddCondition(UnityEditor.Animations.AnimatorConditionMode.NotEqual, k + 1, "Gesture");
            back.hasExitTime = false;
            back.duration = 0.25f;
        }
        ctrl.AddLayer(new UnityEditor.Animations.AnimatorControllerLayer
        {
            name = "Gestures", stateMachine = gsm, avatarMask = mask, defaultWeight = 0f,
            blendingMode = UnityEditor.Animations.AnimatorLayerBlendingMode.Override
        });
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // "Visual" child holding every NPC model; CustomerAppearance keeps one, NPCWalkBob animates it
    static void AddNpcModels(Transform root, string[] models)
    {
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root, false);
        foreach (string m in models)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CharDir + m + ".fbx");
            if (asset == null) continue;
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, visual.transform);
            inst.name = m;
            Animator anim = inst.GetComponent<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = NpcController(m);
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            SetupLods(inst, 0.11f, 0.012f);
        }
        visual.AddComponent<CustomerAppearance>();
        visual.AddComponent<NPCAnimDriver>();
        visual.AddComponent<NPCWeather>().umbrellaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "SM_HandUmbrella.fbx");
    }

    // Passer-by NPC that walks along the sidewalk (Spawner.npcPrefab)
    public static GameObject CreatePedestrianPrefab()
    {
        Directory.CreateDirectory(PrefabDir);
        GameObject root = new GameObject("Pedestrian_ThaiBoba");
        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.25f;
        agent.height = 1.7f;
        agent.avoidancePriority = 70;
        root.AddComponent<PedestrianWalker>();
        AddNpcModels(root.transform, PedestrianModels);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "Pedestrian_ThaiBoba.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------ street
    // Blender (x, y, z) / yaw → Unity (-x, z, -y) / -yaw  (matches the FBX export settings)
    static Vector3 P(float bx, float by, float bz) { return new Vector3(-bx, bz, -by); }

    static GameObject Place(string model, float bx, float by, float bz, float rz, Transform parent, Col col)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + model + ".fbx");
        if (asset == null)
        {
            Debug.LogWarning("Missing model " + model);
            return null;
        }
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.localPosition = P(bx, by, bz);
        go.transform.localRotation = Quaternion.Euler(0, -rz, 0);
        SetupLods(go, 0.14f, 0.008f);
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (col == Col.Mesh) mf.gameObject.AddComponent<MeshCollider>();
            else if (col == Col.Box) mf.gameObject.AddComponent<BoxCollider>();
        }
        return go;
    }

    static Transform Child(GameObject go, string name)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static Transform Marker(string name, Transform parent, float bx, float by, float rz)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = P(bx, by, 0);
        t.localRotation = Quaternion.Euler(0, -rz, 0);
        return t;
    }

    // Keeps NPCs on the near footpath: road, ground slab and the far side are left out of the NavMesh
    static GameObject NoWalk(GameObject go)
    {
        if (go != null) go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        return go;
    }

    static readonly string[] NearRow = { "SM_Shop_ChickenRiceHome", "SM_Shophouse_Closed", "SM_Shop_Salon", "SM_Shop_Mart",
                                         "SM_Shop_ClosedPlain", "SM_Shop_Coffee", "SM_Shop_Phone", "SM_Shop_Pharmacy" };
    static readonly string[] NearRowLeft = { "SM_Shop_Pharmacy", "SM_Shop_Phone", "SM_Shop_ClosedPlain", "SM_Shop_Coffee",
                                             "SM_Shop_Mart", "SM_Shophouse_Closed", "SM_Shop_Salon", "SM_Shop_ChickenRice" };
    static readonly string[] FarRow = { "SM_Shop_Mart", "SM_Shop_Coffee", "SM_Shophouse_Closed", "SM_Shop_Salon",
                                        "SM_Shop_ChickenRice", "SM_Shop_ClosedPlain", "SM_Shop_Pharmacy", "SM_Shop_Phone" };

    // Both sides of the soi, background condos, closed street ends, lamps, string lights, trees and stalls.
    static void BuildNeighbourhood(Transform env, StreetRefs r)
    {
        // opposite side: its own footpath (walkable, but the road between stays out of the NavMesh)
        Transform far = new GameObject("FarSide").transform;
        far.SetParent(env, false);
        Transform back = new GameObject("Skyline (no walk)").transform;
        back.SetParent(env, false);
        NoWalk(back.gameObject);
        Transform deco = new GameObject("StreetDecor").transform;
        deco.SetParent(env, false);

        const float half = 40f;
        NoWalk(Place("SM_Ground", 0, 0, 0, 0, env, Col.Box));
        for (int k = 1; k <= 8; k++)
        {
            Place(NearRow[(k - 1) % NearRow.Length], k * 4.6f, 0, 0, 0, env, Col.Mesh);
            GameObject left = Place(NearRowLeft[(k - 1) % NearRowLeft.Length], -k * 4.6f, 0, 0, 0, env, Col.Mesh);
            if (k == 1) r.neighbourLeft = left;
        }
        for (int k = -8; k <= 8; k++)                     // opposite row faces the shop (+Y in Blender)
            Place(FarRow[(k + 8) % FarRow.Length], k * 4.6f, -13.4f, 0, 180, far, Col.Mesh);
        for (int i = 0; i < 42; i++)
        {
            Place("SM_Sidewalk", -41 + i * 2, -1.5f, 0, 0, env, Col.Box);
            Place("SM_Sidewalk", -41 + i * 2, -11.9f, 0, 180, far, Col.Box);
        }
        for (int i = 0; i <= 20; i++)
            NoWalk(Place("SM_RoadTile", -half + i * 4, -6.7f, -0.15f, 0, env, Col.Box));
        // closed street ends + skyline
        BuildJunctions(env, back);   // side roads + tunnels at both ends of the street
        string[] condos = { "SM_Condo_A", "SM_Condo_C", "SM_Condo_B", "SM_Condo_A", "SM_Condo_C" };
        for (int i = 0; i < condos.Length; i++)
        {
            Place(condos[i], -32 + i * 16, 9.5f, 0, 0, back, Col.None);            // behind the shop row, facing the street
            Place(condos[(i + 2) % condos.Length], -32 + i * 16, -24f, 0, 180, back, Col.None);
        }
        // street furniture
        for (int i = -3; i <= 3; i++)
        {
            float x = i * 11.5f + 5.75f;
            GameObject lights = Place("SM_StringLights", x - 2.9f, -0.3f, 0, 0, deco, Col.None);
            NoWalk(lights);
        }
        // power poles every 8 m on both kerbs, from one corner of the soi to the other (footpaths end at x = ±41,
        // the side roads start beyond). Each pole's wires reach 8 m along +X (Blender); the last pole on each row
        // is turned round so its wires run back along the street instead of hanging over the junction.
        Transform poles = new GameObject("PowerLines").transform;
        poles.SetParent(env, false);
        for (float x = -34f; x <= 38.1f; x += 8f)      // grid keeps clear of the spirit house (x -4) and the shop door
        {
            bool nearEnd = x > 37f, farEnd = x < -33f;
            Place("SM_PowerPole", x, -2.9f, 0, nearEnd ? 180 : 0, poles, Col.None);
            Place("SM_PowerPole", x, -10.5f, 0, farEnd ? 0 : 180, poles, Col.None);
        }
        // street lamps between the poles
        foreach (float x in new[] { -30f, -14f, 10f, 26f })
        {
            r.lamps.Add(Place("SM_StreetLamp", x, -2.75f, 0, 0, deco, Col.None));
            r.lamps.Add(Place("SM_StreetLamp", x, -10.65f, 0, 180, far, Col.None));
        }
        foreach (float x in new[] { -24f, -8f, 28f })
            Place("SM_StreetTree", x, -12.6f, 0, 0, far, Col.None);
        // hawker stalls on the far footpath (on the near side they would block the customers' walkway)
        foreach (float x in new[] { -16f, 6f, 18f })   // close to the shopfronts so people can walk past
            r.steamSpots.Add(Place("SM_FoodStall", x, -12.9f, 0, 180, far, Col.None).transform);
        Transform farPath = new GameObject("FarFootpath").transform;
        farPath.SetParent(env, false);
        r.farEnds = new[] { Marker("FarEnd_Left", farPath, -39.5f, -11.6f, 0), Marker("FarEnd_Right", farPath, 39.5f, -11.6f, 0) };
        foreach (Transform e in r.farEnds) e.gameObject.AddComponent<PedestrianEnd>().group = 1;
    }

    public class StreetRefs
    {
        public List<GameObject> lamps = new List<GameObject>();
        public List<Transform> steamSpots = new List<Transform>();
        public Transform[] farEnds;
        public GameObject root;
        public ServeStation serve;
        public Transform orderPoint, enterPoint, exitPoint;
        public Transform[] queueSlots;
        public Transform[] spawnPoints, exitWaypoints;
        public GameObject fan;
        public List<GameObject> seating = new List<GameObject>();
        public GameObject neighbourLeft;                                   // replaced by the expansion when bought
        public Dictionary<string, GameObject> decor = new Dictionary<string, GameObject>();
        public Dictionary<string, List<GameObject>> upgradeShow = new Dictionary<string, List<GameObject>>();   // shown once that upgrade is bought
        public List<DecorPlacement.Movable> movables = new List<DecorPlacement.Movable>();                     // the player can move these

        public GameObject Movable(string id, GameObject go, float radius)
        {
            if (go != null) movables.Add(new DecorPlacement.Movable { id = id, root = go.transform, radius = radius });
            return go;
        }

        public void Upgrade(string id, GameObject go)
        {
            if (go == null) return;
            if (!upgradeShow.TryGetValue(id, out List<GameObject> list)) upgradeShow[id] = list = new List<GameObject>();
            list.Add(go);
        }
    }

    [MenuItem("Tools/Thai Boba Shop/2. Build Street Set In Current Scene")]
    public static void BuildStreetMenu()
    {
        StreetRefs refs = BuildStreet();
        EnsureSystems(refs);
        Selection.activeGameObject = refs.root;
        EditorSceneManager.MarkSceneDirty(refs.root.scene);
        Debug.Log("Thai street built. Bake the NavMeshSurface on 'ThaiBobaStreet', then point your Spawner at the markers under it.");
    }

    public static StreetRefs BuildStreet()
    {
        GameObject cup = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Cup_ThaiBoba.prefab");
        if (cup == null) cup = CreateCupPrefab();

        StreetRefs r = new StreetRefs();
        GameObject root = new GameObject("ThaiBobaStreet");
        Undo.RegisterCreatedObjectUndo(root, "Build Thai Boba Street");
        r.root = root;
        Transform env = new GameObject("Environment").transform;
        env.SetParent(root.transform, false);
        Transform shop = new GameObject("ShopInterior").transform;
        shop.SetParent(root.transform, false);
        Transform props = new GameObject("StreetProps").transform;
        props.SetParent(root.transform, false);

        // buildings, sidewalk, road
        GameObject ourShell = Place("SM_Shophouse_BobaShop", 0, 0, 0, 0, env, Col.Mesh);
        BuildNeighbourhood(env, r);
        // our shell and the pharmacy next door make way for the knocked-through shop when the expansion is bought
        GameObject before = new GameObject("BeforeExpansion");
        before.transform.SetParent(env, false);
        if (ourShell != null) ourShell.transform.SetParent(before.transform, true);
        if (r.neighbourLeft != null) r.neighbourLeft.transform.SetParent(before.transform, true);
        r.neighbourLeft = before;

        // shop interior + drink stations
        GameObject counter = Place("SM_Counter", 0, 1.4f, 0, 0, shop, Col.Box);
        r.serve = counter.AddComponent<ServeStation>();

        // Mesh collider for the table + shelf: a box around it would swallow the pot/urns/syrups standing on it
        GameObject back = Place("SM_BackCounter", 0, 6.48f, 0, 0, shop, Col.None);
        foreach (MeshFilter mf in back.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.name.StartsWith("Tin_")) mf.gameObject.AddComponent<BoxCollider>();
            else mf.gameObject.AddComponent<MeshCollider>();
        }
        foreach (Transform t in back.GetComponentsInChildren<Transform>())
            if (t.name.StartsWith("Tin_"))
            {
                IngredientStation s = t.gameObject.AddComponent<IngredientStation>();
                s.kind = IngredientStation.Kind.Flavor;
                s.flavorId = t.name.Substring(4);
            }

        GameObject urns = Place("SM_TeaUrnSet", -0.6f, 6.5f, 0.9f, 0, shop, Col.Box);
        foreach (Transform t in urns.GetComponentsInChildren<Transform>())
            if (t.name.StartsWith("Urn_"))
            {
                IngredientStation s = t.gameObject.AddComponent<IngredientStation>();
                s.kind = IngredientStation.Kind.Flavor;
                s.flavorId = t.name.Substring(4);
            }

        GameObject potGo = Place("SM_BobaPot", 0.5f, 6.5f, 0.9f, 0, shop, Col.Box);
        potGo.AddComponent<IngredientStation>().kind = IngredientStation.Kind.Boba;
        potGo.AddComponent<BobaPot>().font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Thai/Kanit-Medium SDF.asset");   // pearls must be cooked
        Place("SM_SyrupBottles", 1.0f, 6.5f, 0.9f, 0, shop, Col.Box).AddComponent<IngredientStation>().kind = IngredientStation.Kind.Sweetness;
        Place("SM_IceCooler", 1.7f, 5.2f, 0, 0, shop, Col.Box).AddComponent<IngredientStation>().kind = IngredientStation.Kind.Ice;
        GameObject sealer = Place("SM_CupSealer", 0.75f, 1.5f, 1.0f, 180, shop, Col.Box);
        sealer.AddComponent<SealerStation>();
        Place("SM_CupStack", 0.35f, 1.55f, 1.0f, 0, shop, Col.Box).AddComponent<CupDispenser>().cupPrefab = cup;
        Place("SM_TrashBin", -1.6f, 5.9f, 0, 0, shop, Col.Box).AddComponent<TrashStation>();
        Place("SM_MenuBoard", 0, 6.79f, 2.15f, 0, shop, Col.None);
        Place("SM_POS", -0.75f, 1.35f, 1.0f, 0, shop, Col.None);
        Place("SM_DrinkDisplay", -0.1f, 1.3f, 1.0f, 0, shop, Col.None);
        Place("SM_Shaker", 0.15f, 6.4f, 0.9f, 0, shop, Col.None);
        Place("SM_DrinkFridge", -1.85f, 4.6f, 0, 90, shop, Col.Box).AddComponent<IngredientStation>().kind = IngredientStation.Kind.Foam;   // cheese foam topping
        GameObject seasonCart = Place("SM_SeasonCart", -1.85f, 3.75f, 0, 90, shop, Col.Box);  // seasonal menu: blender + marshmallows
        foreach (Transform t in seasonCart.GetComponentsInChildren<Transform>())
        {
            if (t.name == "Cart_Blender") t.gameObject.AddComponent<IngredientStation>().kind = IngredientStation.Kind.Blend;
            else if (t.name == "Cart_Marshmallow") t.gameObject.AddComponent<IngredientStation>().kind = IngredientStation.Kind.Marshmallow;
            else if (t.name == "Cart_Body")
            {
                // the body's box would wrap the blender and the jar standing on it (and swallow the E press): cart only up to the top
                BoxCollider bc = t.GetComponent<BoxCollider>();
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (bc != null && mf != null && mf.sharedMesh != null)
                {
                    Bounds b = mf.sharedMesh.bounds;
                    bc.size = new Vector3(b.size.x, 0.82f, b.size.z);
                    bc.center = new Vector3(b.center.x, b.min.y + 0.41f, b.center.z);
                }
            }
        }
        // topping bar against the right wall (konjac / pandan jelly / pudding, unlocked with the shop level)
        GameObject toppingBar = Place("SM_ToppingBar", 1.78f, 4.15f, 0, -90, shop, Col.None);
        foreach (Transform t in toppingBar.GetComponentsInChildren<Transform>())
        {
            if (t.name.StartsWith("Tub_"))
            {
                t.gameObject.AddComponent<BoxCollider>();
                IngredientStation s = t.gameObject.AddComponent<IngredientStation>();
                s.kind = IngredientStation.Kind.Topping;
                s.flavorId = t.name.Substring(4).Split('.')[0];
            }
            else if (t.name.StartsWith("Bar_Body"))
            {
                BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();           // table only, the tubs take the E press
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Bounds b = mf.sharedMesh.bounds;
                    bc.size = new Vector3(b.size.x, 0.82f, b.size.z);
                    bc.center = new Vector3(b.center.x, b.min.y + 0.41f, b.center.z);
                }
            }
        }
        BuildDecor(r, shop, env);                                          // decorations / helper / expansion (hidden until bought)
        // the shop computer for ordering supplies: left wall beside the counter, screen facing into the shop
        Place("SM_OrderComputer", -1.8f, 2.75f, 0, 90, shop, Col.Box).AddComponent<SupplyComputer>();
        r.fan = Place("SM_StandingFan", 1.7f, 3.0f, 0, 200, shop, Col.None);
        Transform blades = Child(r.fan, "Fan_Blades");
        if (blades != null) blades.gameObject.AddComponent<Spinner>().axis = Vector3.forward;

        // upgrades you can see (hidden until bought): speakers, neon, the turbo kit, the delivery scooter
        r.Upgrade("speaker", Place("SM_Speaker", -1.9f, 0.9f, 3.4f, 150, shop, Col.None));
        r.Upgrade("speaker", Place("SM_Speaker", 1.9f, 0.9f, 3.4f, -150, shop, Col.None));
        r.Upgrade("neon", Place("SM_NeonSign", 2.15f, 0, 4.4f, 0, env, Col.None));
        r.Upgrade("neon", Place("SM_NeonWall", -2.09f, 3.3f, 2.15f, 90, shop, Col.None));
        if (sealer != null) r.Upgrade("fast_sealer", Place("SM_TurboKit", 0, 0, 0, 0, sealer.transform, Col.None));
        GameObject scooter = Place("SM_Scooter", -1.75f, -2.45f, 0, 90, props, Col.None);
        if (scooter != null)
        {
            scooter.name = "DeliveryScooter";
            Place("SM_DeliveryBox", 0, 0.75f, 0.86f, 0, scooter.transform, Col.None);
            r.Upgrade("delivery", scooter);
        }

        // street life
        Obstacle(r.Movable("plant_l", Place("SM_PottedPlant", -1.8f, -0.4f, 0, 0, props, Col.None), 0.3f), new Vector3(0.5f, 1f, 0.5f));
        Obstacle(r.Movable("plant_r", Place("SM_PottedPlant", 1.8f, -0.4f, 0, 40, props, Col.None), 0.3f), new Vector3(0.5f, 1f, 0.5f));
        Obstacle(r.Movable("promo", Place("SM_PromoSign", -0.8f, 0.0f, 0, 12, props, Col.None), 0.35f), new Vector3(0.65f, 1f, 0.45f));
        GameObject spirit = Place("SM_SpiritHouse", -4.0f, -1.0f, 0, 10, props, Col.Box);
        spirit.AddComponent<SpiritHouseOffering>();
        Obstacle(spirit, new Vector3(0.7f, 1.5f, 0.7f));
        Obstacle(r.Movable("dragon_jar", Place("SM_DragonJar", -5.2f, -0.6f, 0, 0, props, Col.Box), 0.4f), new Vector3(0.75f, 1f, 0.75f));
        Place("SM_PalmTree", -6.6f, -1.2f, 0, 40, props, Col.None);
        r.seating.Add(Place("SM_SteelTable", 2.6f, -1.7f, 0, 0, props, Col.Box));
        r.seating.Add(Place("SM_PlasticStool", 2.0f, -1.9f, 0, 0, props, Col.Box));
        r.seating.Add(Place("SM_PlasticStool", 3.2f, -1.6f, 0, 30, props, Col.Box));
        r.seating.Add(Place("SM_PlasticStool", 2.6f, -1.0f, 0, 10, props, Col.Box));
        foreach (GameObject g in r.seating) Obstacle(g, new Vector3(0.45f, 1f, 0.45f));            // bought later: carve at runtime
        for (int i = 1; i < r.seating.Count; i++)                                                    // customers sit here with their drink
            if (r.seating[i] != null && r.seating[0] != null) r.seating[i].AddComponent<SeatSpot>().table = r.seating[0].transform;
        r.seating[0].GetComponent<UnityEngine.AI.NavMeshObstacle>().size = new Vector3(0.85f, 1f, 0.85f);
        r.seating.Add(Place("SM_MarketUmbrella", 2.6f, -1.7f, 0, 0, props, Col.None));
        // the table set moves as one piece in arrange mode
        GameObject seatingSet = new GameObject("SeatingSet");
        seatingSet.transform.SetParent(props, false);
        seatingSet.transform.localPosition = P(2.6f, -1.7f, 0);
        foreach (GameObject g in r.seating) if (g != null) g.transform.SetParent(seatingSet.transform, true);
        r.Movable("seating", seatingSet, 0.9f);
        // Keep the near footpath clear: the cart goes on the far side, the bin stands right at the curb
        Place("SM_StreetCart", 3.0f, -12.4f, 0, 0, props, Col.Box);
        Place("SM_TrashBin", 7.4f, -2.75f, 0, 0, props, Col.Box);
        Place("SM_SoiSign", -7.6f, -2.75f, 0, 0, props, Col.None);

        // customer path markers (queue grows from the counter toward the street)
        Transform markers = new GameObject("CustomerPath").transform;
        markers.SetParent(root.transform, false);
        r.orderPoint = Marker("OrderPoint", markers, 0, 0.55f, 180);
        r.enterPoint = Marker("EnterPoint", markers, 0, -2.2f, 0);
        // the line: at the counter, one step back, then along the shopfront to the left (clear of the road,
        // the footpath lane and the way out); customers leave to the right
        var q = new List<Transform> { r.orderPoint };
        foreach (var (qx, qy) in new[] { (-0.12f, -0.32f), (-0.72f, -1.02f), (-1.43f, -1.07f), (-2.18f, -1.1f), (-2.93f, -1.13f),
                                         (-3.55f, -1.72f), (-4.3f, -1.95f), (-5.05f, -1.95f) })
            q.Add(Marker("Queue_" + q.Count, markers, qx, qy, 0));
        r.queueSlots = q.ToArray();
        r.exitPoint = Marker("Exit_1", markers, 1.62f, -1.05f, 0);
        r.spawnPoints = new[] { Marker("Spawn_Left", markers, -39.5f, -2.0f, 0), Marker("Spawn_Right", markers, 39.5f, -2.0f, 0) };   // ends of the soi
        foreach (Transform sp in r.spawnPoints) sp.gameObject.AddComponent<PedestrianEnd>();
        r.exitWaypoints = new[] { Marker("Leave_1", markers, 2.0f, -2.55f, 0), Marker("Leave_2", markers, 39.5f, -2.3f, 0) };   // walk off to the end of the soi

        NavMeshSurface surface = root.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        return r;
    }

    // ------------------------------------------------------------------ systems
    public static GameObject EnsureSystems(StreetRefs refs)
    {
        GameObject sys = GameObject.Find("ThaiBobaSystems");
        if (sys == null) sys = new GameObject("ThaiBobaSystems");
        // GameManager/InventoryManager call DontDestroyOnLoad, so they get their own object
        // (otherwise the per-scene systems below would be dragged into DontDestroyOnLoad too)
        GameObject persistent = GameObject.Find("GameManager");
        if (Object.FindFirstObjectByType<GameManager>() == null || Object.FindFirstObjectByType<InventoryManager>() == null)
            if (persistent == null) persistent = new GameObject("GameManager");
        if (Object.FindFirstObjectByType<GameManager>() == null) persistent.AddComponent<GameManager>();
        if (Object.FindFirstObjectByType<InventoryManager>() == null) persistent.AddComponent<InventoryManager>();
        if (sys.GetComponent<ShopProgress>() == null) sys.AddComponent<ShopProgress>();
        if (sys.GetComponent<DailyEventSystem>() == null) sys.AddComponent<DailyEventSystem>();
        if (sys.GetComponent<DailyGoals>() == null) sys.AddComponent<DailyGoals>();
        if (sys.GetComponent<WeatherSystem>() == null) sys.AddComponent<WeatherSystem>();
        if (sys.GetComponent<RegularsSystem>() == null) sys.AddComponent<RegularsSystem>();
        if (sys.GetComponent<ChallengeMode>() == null) sys.AddComponent<ChallengeMode>();
        DecorShop decorShop = sys.GetComponent<DecorShop>();
        if (decorShop == null) decorShop = sys.AddComponent<DecorShop>();
        decorShop.items = DecorShop.Defaults();
        foreach (DecorShop.Item it in decorShop.items)
            if (refs != null && refs.decor.TryGetValue(it.id, out GameObject shown)) it.show = shown;
        if (refs != null) decorShop.Get("expand").hide = refs.neighbourLeft;
        if (sys.GetComponent<ComputerTerminal>() == null) sys.AddComponent<ComputerTerminal>();
        if (sys.GetComponent<ThaiBobaBootstrap>() == null) sys.AddComponent<ThaiBobaBootstrap>();
        UpgradeShop shop = sys.GetComponent<UpgradeShop>();
        if (shop == null) shop = sys.AddComponent<UpgradeShop>();

        if (refs != null)
        {
            if (refs.fan != null) shop.Find("fan").enableWhenOwned = new[] { refs.fan };
            shop.Find("seating").enableWhenOwned = refs.seating.ToArray();
            if (refs.fan != null) refs.fan.SetActive(false);
            foreach (GameObject g in refs.seating) if (g != null) g.SetActive(false);
            foreach (var kv in refs.upgradeShow)
            {
                UpgradeShop.Upgrade u = shop.Find(kv.Key);
                if (u != null) u.enableWhenOwned = kv.Value.ToArray();
                foreach (GameObject g in kv.Value) if (g != null) g.SetActive(false);
            }
        }
        return sys;
    }

    // ------------------------------------------------------------------ demo scene
    [MenuItem("Tools/Thai Boba Shop/Build Full Demo Scene (ThaiBobaStreet.unity)")]
    public static void BuildDemoScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        CreateCupPrefab();
        GameObject customerPrefab = CreateCustomerPrefab();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.75f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.62f, 0.6f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.33f, 0.3f);

        // Sun driven by the existing TimeCyc day clock
        GameObject sun = new GameObject("Sun (TimeCyc)");
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        light.color = new Color(1f, 0.95f, 0.86f);
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50, -30, 0);
        TimeCyc timeCyc = sun.AddComponent<TimeCyc>();
        timeCyc.rotateSun = false;
        DayNightCycle dayNight = sun.AddComponent<DayNightCycle>();
        dayNight.clock = timeCyc;
        dayNight.sun = light;
        RenderSettings.sun = light;
        if (RenderSettings.skybox == null)
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        timeCyc.timeScale = 2f;
        timeCyc.startHour = 7;

        StreetRefs refs = BuildStreet();
        EnsureSystems(refs);

        // Shop lights
        foreach (float by in new[] { 2f, 4.5f })
        {
            GameObject l = new GameObject("ShopLight");
            l.transform.SetParent(refs.root.transform, false);
            l.transform.localPosition = P(0, by, 3.1f);
            Light pl = l.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.range = 6f;
            pl.intensity = 2f;
            pl.color = new Color(1f, 0.96f, 0.9f);
        }

        // Player (staff) behind the counter, facing the street
        GameObject player = new GameObject("Player");
        player.transform.position = P(0, 3.4f, 0) + Vector3.up * 1f;
        CharacterController ch = player.AddComponent<CharacterController>();
        ch.height = 1.7f;
        ch.radius = 0.3f;
        player.AddComponent<MovementWithJoy>().speed = 3f;
        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0, 0.65f, 0);
        Camera cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 65f;
        camGo.AddComponent<AudioListener>();
        CameraMobile look = camGo.AddComponent<CameraMobile>();
        SerializedObject lookSo = new SerializedObject(look);
        lookSo.FindProperty("PlayerBody").objectReferenceValue = player.transform;
        lookSo.ApplyModifiedPropertiesWithoutUndo();
        Transform hand = new GameObject("HandPos").transform;
        hand.SetParent(camGo.transform, false);
        hand.localPosition = new Vector3(0.22f, -0.2f, 0.45f);
        RaycastAim aim = camGo.AddComponent<RaycastAim>();
        aim.handPos = hand;
        aim.Player = player.transform;
        aim.CameraMain = camGo.transform;
        aim.layerMask = ~(1 << 2); // everything except Ignore Raycast
        player.AddComponent<DesktopPlayerInput>();

        // Customers
        Spawner spawner = new GameObject("CustomerSpawner").AddComponent<Spawner>();
        spawner.customerPrefab = customerPrefab;
        spawner.spawnPoints = refs.spawnPoints;
        spawner.enterArea = refs.enterPoint;
        spawner.orderArea = refs.orderPoint;
        spawner.exitArea = refs.exitPoint;
        spawner.customerWaypoints = refs.exitWaypoints;
        spawner.queueSlots = refs.queueSlots;
        spawner.timeCyc = timeCyc;
        spawner.spawnInterval = 6f;
        spawner.openHour = 7;
        spawner.npcPrefab = CreatePedestrianPrefab();
        AmbientPedestrianSpawner farWalkers = new GameObject("FarSidePedestrians").AddComponent<AmbientPedestrianSpawner>();
        farWalkers.pedestrianPrefab = spawner.npcPrefab;
        farWalkers.endA = refs.farEnds[0];
        farWalkers.endB = refs.farEnds[1];
        FestivalActivities crowds = new GameObject("FestivalCrowds").AddComponent<FestivalActivities>();
        crowds.pedestrianPrefab = spawner.npcPrefab;
        crowds.endA = refs.farEnds[0];
        crowds.endB = refs.farEnds[1];
        FxSet fx = CreateFxPrefabs();
        currentFx = fx;
        BuildTraffic(refs.root.transform);
        AddEffects(refs, cam, fx);
        BuildWeather(refs, fx);
        BuildShopGlow(refs);
        BuildLivingSoi(refs, GameObject.Find("ThaiBobaSystems"), fx);

        ShopHUD hud = CreateHUD();
        hud.player = aim;
        timeCyc.timerText = lastClockText;
        timeCyc.day = lastDayText;
        CreateAudio(refs);
        CreateMenus(hud, cam, player);
        GraphicsQuality gq = new GameObject("GraphicsQuality").AddComponent<GraphicsQuality>();
        gq.postProcessing = Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>();
        gq.sun = light;

        // NavMesh for customers
        NavMeshSurface surface = refs.root.GetComponent<NavMeshSurface>();
        surface.BuildNavMesh();
        if (surface.navMeshData != null)
        {
            string navPath = "Assets/Scenes/ThaiBobaStreet_NavMesh.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);
        }

        // Saved the way the game boots: main menu only. GameMenus shows the gameplay HUD once a game starts,
        // so the Game view in edit mode no longer stacks the HUD under the menu.
        GameMenus bootMenus = Object.FindFirstObjectByType<GameMenus>();
        if (bootMenus != null && bootMenus.gameplayUI != null)
            foreach (GameObject g in bootMenus.gameplayUI) if (g != null) g.SetActive(false);

        TidyHierarchy();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();

        // The game boots into this scene (main menu) and reloads it for "back to main menu"
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(sc => sc.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("ThaiBoba demo scene saved to " + ScenePath);

        if (Application.isBatchMode)
            CaptureShots(cam);
    }

    // Renders a few views to BlenderSource/renders/unity_*.png (used to verify the import in batch mode)
    static void CaptureShots(Camera cam)
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../BlenderSource/renders"));
        Directory.CreateDirectory(dir);
        Transform parent = cam.transform.parent;
        var shots = new (string name, Vector3 pos, Vector3 look)[]
        {
            ("unity_street", new Vector3(-7f, 4.2f, 10.5f), new Vector3(-0.2f, 2.2f, 0f)),
            ("unity_player_view", cam.transform.position, cam.transform.position + new Vector3(0, -0.3f, 2f)),
            ("unity_counter", new Vector3(-0.7f, 1.65f, 1.3f), new Vector3(0.1f, 1.15f, -2.6f)),
        };
        cam.transform.SetParent(null, true);
        RenderTexture rt = new RenderTexture(1600, 900, 24);
        Texture2D tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);

        // Temporary NPCs posed mid-walk on the sidewalk, to check the rigged characters import correctly
        List<GameObject> temp = new List<GameObject>();
        string[] all = { "NPC_SchoolBoy", "NPC_UniStudent", "NPC_Worker", "NPC_Vendor", "NPC_Jogger", "NPC_Grandpa", "NPC_Rider" };
        for (int i = 0; i < all.Length; i++)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CharDir + all[i] + ".fbx");
            if (asset == null) continue;
            GameObject go = (GameObject)Object.Instantiate(asset, P(-4.2f + i * 1.3f, -2.1f, 0), Quaternion.Euler(0, i % 2 == 0 ? 180 : 90, 0));
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(CharDir + all[i] + ".fbx"))
                if (o is AnimationClip clip && clip.name == "Walk") clip.SampleAnimation(go, 0.2f + i * 0.1f);
            temp.Add(go);
        }
        // Temporary traffic, oriented exactly like TrafficCar.Init does (LookRotation along the lane)
        TrafficSpawner traffic = Object.FindFirstObjectByType<TrafficSpawner>();
        if (traffic != null)
        {
            for (int i = 0; i < traffic.vehiclePrefabs.Length * 2; i++)
            {
                TrafficRoute route = traffic.routes[i % traffic.routes.Length];
                GameObject v = (GameObject)Object.Instantiate(traffic.vehiclePrefabs[i % traffic.vehiclePrefabs.Length]);
                float f = i < traffic.vehiclePrefabs.Length ? 0.45f + 0.04f * i : (i % 2 == 0 ? 0.9f : 0.13f);
                v.GetComponent<TrafficCar>().Init(route.points, route.Length * f, 0f);
                temp.Add(v);
            }
        }
        shots = new (string name, Vector3 pos, Vector3 look)[]
        {
            shots[0], shots[1], shots[2],
            ("unity_traffic", new Vector3(-9f, 5.5f, 11.2f), new Vector3(2f, 0.6f, 4.5f)),
            ("unity_npcs", new Vector3(3.2f, 1.45f, 3.9f), new Vector3(0.2f, 0.95f, 2.1f)),
            ("unity_junction", new Vector3(-33f, 6.5f, 6.7f), new Vector3(-45.5f, 1.5f, -14f)),
            ("unity_tunnel", new Vector3(-45.5f, 4.5f, 3f), new Vector3(-45.5f, 2f, -18f)),
            ("unity_tunnel_south", new Vector3(-45.5f, 4.5f, 12f), new Vector3(-45.5f, 2f, 30f)),
            ("unity_computer", new Vector3(-0.3f, 1.65f, -3.9f), new Vector3(1.75f, 0.95f, -2.75f)),
        };
        foreach (var s in shots)
        {
            cam.transform.position = s.pos;
            cam.transform.rotation = Quaternion.LookRotation(s.look - s.pos);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, s.name + ".png"), tex.EncodeToPNG());
        }

        // Sloshing drinks: an upright cup, one tilted 25 deg (surface must stay level) and one mid-slosh
        GameObject cupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Cup_ThaiBoba.prefab");
        if (cupPrefab != null && cupPrefab.GetComponent<LiquidSlosh>() != null)
        {
            Material lm = cupPrefab.GetComponent<LiquidSlosh>().liquidMaterial;
            Vector3 basePos = new Vector3(30f, 50f, 30f);            // empty sky, away from the street
            var cups = new List<GameObject>();
            var mats = new List<Material>();
            var setups = new[] { (0f, Vector2.zero, "ThaiTea"), (25f, Vector2.zero, "Matcha"), (0f, new Vector2(0.45f, 0f), "Taro") };
            for (int i = 0; i < setups.Length; i++)
            {
                var (tiltDeg, sl, flavor) = setups[i];
                GameObject c = (GameObject)Object.Instantiate(cupPrefab);
                c.transform.SetPositionAndRotation(basePos + new Vector3((i - 1) * 0.13f, 0, 0), Quaternion.Euler(0, 0, tiltDeg));
                foreach (Transform t in c.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("Liquid_")) t.gameObject.SetActive(t.name == "Liquid_" + flavor);
                    if (t.name == "Cup_Ice") t.gameObject.SetActive(true);
                    if (t.name.StartsWith("Cup_Pearls"))                          // 3, 2, 1 scoops left to right
                    {
                        int scoop = t.name == "Cup_Pearls" ? 1 : t.name[t.name.Length - 1] - '0';
                        t.gameObject.SetActive(scoop <= 3 - i);
                    }
                    if (t.name == "Cup_SealFilm" || t.name == "Cup_Straw") t.gameObject.SetActive(false);
                }
                Renderer lr = c.transform.Find("Liquid_" + flavor)?.GetComponent<Renderer>();
                if (lr != null)
                {
                    Material m = new Material(lm);
                    Color col = lr.sharedMaterial.GetColor("_BaseColor");
                    m.SetColor("_BaseColor", col);
                    m.SetColor("_TopColor", Color.Lerp(col, Color.white, 0.22f));
                    m.SetColor("_FoamColor", Color.Lerp(col, Color.white, 0.6f));
                    float top = lr.GetComponent<MeshFilter>().sharedMesh.bounds.max.y - 0.006f;
                    m.SetVector("_FillPoint", lr.transform.TransformPoint(new Vector3(0, top, 0)));
                    m.SetVector("_Slope", new Vector4(sl.x, sl.y, 0, 0));
                    lr.sharedMaterial = m;
                    mats.Add(m);
                }
                cups.Add(c);
            }
            cam.transform.position = basePos + new Vector3(0, 0.2f, -0.38f);
            cam.transform.rotation = Quaternion.LookRotation(basePos + new Vector3(0, 0.08f, 0) - cam.transform.position);
            float fov = cam.fieldOfView;
            cam.fieldOfView = 40f;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, "unity_liquid.png"), tex.EncodeToPNG());
            cam.fieldOfView = fov;
            foreach (GameObject c in cups) Object.DestroyImmediate(c);
            foreach (Material m in mats) Object.DestroyImmediate(m);
        }

        // Everything from the decorate tab switched on: plants, bunting, lights, cat, bench, helper, expansion next door
        DecorShop decorPreview = Object.FindFirstObjectByType<DecorShop>();
        if (decorPreview != null)
        {
            foreach (DecorShop.Item it in decorPreview.items) { if (it.show != null) it.show.SetActive(true); if (it.hide != null) it.hide.SetActive(false); }
            foreach (var (label, pos, look) in new[]
            {
                ("unity_decor", new Vector3(2.3f, 2.3f, 9.2f), new Vector3(2.3f, 1.7f, 0.5f)),
                ("unity_decor_inside", new Vector3(0.7f, 1.7f, -2.2f), new Vector3(-2.1f, 1.45f, -4.1f)),   // mural on the right wall (Unity x = -Blender x)
            })
            {
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation(look - pos);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, label + ".png"), tex.EncodeToPNG());
            }
            foreach (DecorShop.Item it in decorPreview.items) { if (it.show != null) it.show.SetActive(false); if (it.hide != null) it.hide.SetActive(true); }
        }

        // Every upgrade bought: speakers, turbo sealer, neon (front + wall), seating, delivery scooter
        UpgradeShop upgradePreview = Object.FindFirstObjectByType<UpgradeShop>();
        if (upgradePreview != null)
        {
            var shown = new List<GameObject>();
            foreach (UpgradeShop.Upgrade u in upgradePreview.upgrades)
                if (u.enableWhenOwned != null) foreach (GameObject g in u.enableWhenOwned) if (g != null && !g.activeSelf) { g.SetActive(true); shown.Add(g); }
            foreach (var (label, pos, look) in new[]
            {
                ("unity_upgrades_street", new Vector3(0.3f, 2.4f, 7.0f), new Vector3(-0.3f, 2.6f, 0f)),
                ("unity_upgrades_inside", new Vector3(0.4f, 1.75f, -4.6f), new Vector3(0f, 1.9f, 0f)),
                ("unity_upgrades_wall", new Vector3(-0.6f, 1.7f, -2.2f), new Vector3(2.1f, 2.1f, -3.6f)),
            })
            {
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation(look - pos);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, label + ".png"), tex.EncodeToPNG());
            }
            foreach (GameObject g in shown) g.SetActive(false);
        }

        // Day / sunset / night from the same viewpoint, driven by DayNightCycle exactly like in play mode
        DayNightCycle dn = Object.FindFirstObjectByType<DayNightCycle>();
        NightLights nl = Object.FindFirstObjectByType<NightLights>();
        if (dn != null)
        {
            Material originalSky = RenderSettings.skybox;
            Material tempSky = originalSky != null ? new Material(originalSky) : null;
            RenderSettings.skybox = tempSky;
            dn.SetSkyMaterial(tempSky);
            foreach (var (label, hour) in new[] { ("1200", 12f), ("1815", 18.25f), ("2030", 20.5f) })
            {
                dn.Apply(hour);
                bool night = hour >= nl.onHour || hour < nl.offHour;
                foreach (Light l in nl.lights) l.enabled = night;
                ShopNightGlow sg = Object.FindFirstObjectByType<ShopNightGlow>();
                if (sg != null) { if (night) sg.Preview(true); else sg.RestoreOriginals(); }
                cam.transform.position = new Vector3(-9f, 5.5f, 11.2f);
                cam.transform.rotation = Quaternion.LookRotation(new Vector3(2f, 0.6f, 4.5f) - cam.transform.position);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, "unity_time_" + label + ".png"), tex.EncodeToPNG());
            }
            // still 20:30: street-level look at the lit shopfronts on both sides
            foreach (var (label, pos, look) in new[]
            {
                ("unity_night_shops", new Vector3(-8f, 1.7f, 9.6f), new Vector3(-20f, 1.6f, 11.8f)),
                ("unity_night_street", new Vector3(14f, 2.2f, 6.7f), new Vector3(-10f, 1.2f, 6.2f)),
            })
            {
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation(look - pos);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, label + ".png"), tex.EncodeToPNG());
            }
            foreach (Light l in nl.lights) l.enabled = false;
            ShopNightGlow sgDay = Object.FindFirstObjectByType<ShopNightGlow>();
            if (sgDay != null) sgDay.RestoreOriginals();

            // Rainy afternoon: wet glossy road, rain + splashes simulated in edit mode, dimmed sun
            WeatherSystem ws = Object.FindFirstObjectByType<WeatherSystem>();
            WetSurfaces wet = Object.FindFirstObjectByType<WetSurfaces>();
            if (ws != null && wet != null)
            {
                dn.Apply(16.5f);
                Light sunL = RenderSettings.sun;
                float sunI = sunL != null ? sunL.intensity : 0f;
                if (sunL != null) sunL.intensity *= 0.35f;
                Color amb = RenderSettings.ambientLight;
                RenderSettings.ambientLight = amb * 0.6f;
                var restore = new List<(Renderer r, Material[] m)>();
                foreach (Renderer r in wet.surfaces)
                {
                    restore.Add((r, r.sharedMaterials));
                    Material[] ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++)
                    {
                        Material m = new Material(ms[i]);
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * (1f - wet.wetDarken));
                        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", wet.wetSmoothness);
                        ms[i] = m;
                    }
                    r.sharedMaterials = ms;
                }
                foreach (ParticleSystem ps in new[] { ws.rain, ws.rainSplash })
                {
                    if (ps == null) continue;
                    var em = ps.emission;
                    em.rateOverTimeMultiplier = ps == ws.rain ? ws.maxRainRate : ws.maxSplashRate;
                    ps.Simulate(2.5f, true, true);
                }
                foreach (var (label, pos, look) in new[]
                {
                    ("unity_rain", new Vector3(-9f, 5.5f, 11.2f), new Vector3(2f, 0.6f, 4.5f)),
                    ("unity_rain_street", new Vector3(6f, 1.7f, 6.7f), new Vector3(-6f, 0.4f, 6.2f)),
                })
                {
                    cam.transform.position = pos;
                    cam.transform.rotation = Quaternion.LookRotation(look - pos);
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(dir, label + ".png"), tex.EncodeToPNG());
                }
                foreach (ParticleSystem ps in new[] { ws.rain, ws.rainSplash })
                {
                    if (ps == null) continue;
                    ps.Clear(true);
                    var em = ps.emission;
                    em.rateOverTimeMultiplier = 0f;
                }
                foreach (var (r, m) in restore) { foreach (Material t in r.sharedMaterials) Object.DestroyImmediate(t); r.sharedMaterials = m; }
                if (sunL != null) sunL.intensity = sunI;
                RenderSettings.ambientLight = amb;
            }

            // power lines running the whole soi, both kerbs
            dn.Apply(12f);
            cam.transform.position = new Vector3(47f, 6f, 6.7f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(-30f, 3.5f, 6.7f) - cam.transform.position);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, "unity_powerlines.png"), tex.EncodeToPNG());

            foreach (Light l in nl.lights) l.enabled = false;
            ShopNightGlow sg2 = Object.FindFirstObjectByType<ShopNightGlow>();
            if (sg2 != null) sg2.RestoreOriginals();
            RenderSettings.skybox = originalSky;
            dn.SetSkyMaterial(null);
            dn.Apply(7.5f);
            Object.DestroyImmediate(tempSky);
        }
        foreach (GameObject g in temp) Object.DestroyImmediate(g);
        cam.transform.SetParent(parent, true);
        cam.transform.localPosition = new Vector3(0, 0.65f, 0);
        cam.transform.localRotation = Quaternion.identity;

        // UI shots: gameplay HUD with sample values, end-of-day summary, main menu and settings
        ShopHUD hud = Object.FindFirstObjectByType<ShopHUD>();
        GameMenus menus = Object.FindFirstObjectByType<GameMenus>();
        void Shot(string file, Camera c, params Canvas[] canvases)
        {
            foreach (Canvas cv in canvases) { cv.renderMode = RenderMode.ScreenSpaceCamera; cv.worldCamera = c; cv.planeDistance = 0.5f; }
            Canvas.ForceUpdateCanvases();
            c.targetTexture = rt;
            c.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
            c.targetTexture = null;
            foreach (Canvas cv in canvases) cv.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        if (hud != null)
        {
            Canvas hc = hud.GetComponent<Canvas>();
            hud.moneyText.text = "1,250";
            hud.reputationText.text = "3.4";
            for (int i = 0; i < hud.reputationStars.Length; i++) hud.reputationStars[i].color = i < 3 ? Color.white : new Color(1, 1, 1, 0.25f);
            hud.levelText.text = "เลเวล 2";
            hud.xpFill.fillAmount = 0.4f;
            hud.comboText.text = "คอมโบ x3";
            hud.eventText.text = "ร้อนตับแตก 39°C\n<size=72%>คนแห่ซื้อน้ำเย็น ทุกแก้วต้องใส่น้ำแข็ง</size>";
            hud.goalsText.text = "(2/3) ขายชาไทย 3 แก้ว  +40 ฿\n[เสร็จ] คอมโบให้ถึง x3  +70 ฿\nไม่มีลูกค้าเดินหนีทั้งวัน  +80 ฿";
            hud.orderTitle.text = "ป้าข้างบ้าน";
            hud.orderText.text = "ชาไทย หวานน้อย\nไข่มุก 1 ช้อน · ใส่น้ำแข็ง";
            hud.patienceFill.fillAmount = 0.65f;
            hud.patienceFill.color = new Color(0.6f, 0.75f, 0.3f);
            hud.promptText.text = "[E]  ตักไข่มุก (+1 ช้อน)";
            hud.heldCupText.text = "ในมือ: ชาไทย · ไข่มุก 1 · มีน้ำแข็ง · หวานน้อย · ยังไม่ซีล";
            hud.toastText.text = "+45 ฿  ทิป +12 ฿  คอมโบ x3!";
            hud.cupsText.text = "48"; hud.bobaText.text = "197"; hud.iceText.text = "499"; hud.teaText.text = "298";
            if (lastClockText != null) lastClockText.text = "14:35";
            if (lastDayText != null) lastDayText.text = "วันที่ 3";
            Transform game = hud.transform.Find("Gameplay");
            game.gameObject.SetActive(true);
            Transform pcHints = game.Find("PCHints");
            if (pcHints != null) pcHints.gameObject.SetActive(false);
            Shot("unity_hud.png", cam, hc);
            TutorialSystem tutorial = hud.GetComponent<TutorialSystem>();
            if (tutorial != null && tutorial.panel != null)
            {
                tutorial.stepText.text = "5/9";
                tutorial.titleText.text = "ตักไข่มุก 1 ช้อน";
                tutorial.bodyText.text = "กดปุ่ม <b>ใช้</b>ที่<b>หม้อไข่มุก</b> · กด 1 ครั้ง = 1 ช้อน (ดูจำนวนได้จาก popup)";
                tutorial.finishButton.gameObject.SetActive(false);
                tutorial.panel.SetActive(true);
                Shot("unity_tutorial.png", cam, hc);
                tutorial.panel.SetActive(false);
                tutorial.finishButton.gameObject.SetActive(true);
                tutorial.stepText.text = tutorial.titleText.text = tutorial.bodyText.text = "";
            }
            Transform touch = game.Find("TouchControls");
            touch.gameObject.SetActive(false);
            if (pcHints != null) pcHints.gameObject.SetActive(true);
            Shot("unity_hud_pc.png", cam, hc);
            touch.gameObject.SetActive(true);
            hud.summaryTitle.text = "ปิดร้าน · สรุปยอดวันที่ 3";
            hud.summaryText.text = "เสิร์ฟ  <b>18</b> แก้ว   (เป๊ะ 14 · พอใช้ 4)\nลูกค้าเดินหนี  1     แก้วโดนปฏิเสธ  2\nคอมโบสูงสุด  x6\n\n" +
                                   "ยอดขาย  <color=#4E8A3C>+812 ฿</color>\nทิป  <color=#4E8A3C>+164 ฿</color>\nโบนัส  <color=#4E8A3C>+90 ฿</color>\n" +
                                   "ค่าใช้จ่าย  <color=#C23A66>-120 ฿</color>\n<size=125%><b>กำไรสุทธิ  946 ฿</b></size>";
            hud.summaryPanel.SetActive(true);
            Shot("unity_summary.png", cam, hc);
            hud.summaryPanel.SetActive(false);
            foreach (TMP_Text t in hud.GetComponentsInChildren<TMP_Text>(true))
                if (t == hud.toastText || t == hud.promptText || t == hud.heldCupText) t.text = "";
        }
        if (menus != null && menus.menuCamera != null)
        {
            Camera mc = menus.menuCamera;
            Canvas menuCanvas = menus.GetComponent<Canvas>();
            if (menus.continueLabel != null) menus.continueLabel.text = "เล่นต่อ  ·  วันที่ 3";
            mc.transform.position = new Vector3(-5f, 5.2f, 9.5f);
            mc.transform.LookAt(new Vector3(-1.5f, 2f, 0f));
            Shot("unity_menu_main.png", mc, menuCanvas);
            menus.mainMenu.SetActive(false);
            menus.settingsPanel.SetActive(true);
            Shot("unity_menu_settings.png", mc, menuCanvas);
            menus.settingsPanel.SetActive(false);
            menus.pausePanel.SetActive(true);
            Shot("unity_menu_pause.png", mc, menuCanvas);
            menus.pausePanel.SetActive(false);
            menus.mainMenu.SetActive(true);
            if (menus.continueLabel != null) menus.continueLabel.text = "เล่นต่อ";
        }
        cam.targetTexture = null;
        RenderTexture.active = null;
        Debug.Log("Captured Unity preview shots to " + dir);
    }

    // ------------------------------------------------------------------ Thai font
    const string ThaiFontPath = "Assets/Fonts/Thai/Sarabun-SemiBold.ttf";
    const string ThaiFontAssetPath = "Assets/Fonts/Thai/Sarabun-SemiBold SDF.asset";

    // Creates (once) a dynamic TMP font asset from Sarabun and registers it as a global TMP fallback
    public static TMP_FontAsset EnsureThaiFont()
    {
        TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThaiFontAssetPath);
        if (fa == null)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(ThaiFontPath);
            if (font == null) return null;
            fa = CreateFontAsset(font, ThaiFontAssetPath);
        }
        List<TMP_FontAsset> fallbacks = TMP_Settings.fallbackFontAssets;
        if (fallbacks != null && !fallbacks.Contains(fa))
        {
            fallbacks.Add(fa);
            EditorUtility.SetDirty(TMP_Settings.instance);
        }
        // New text components should get Thai mark positioning too
        List<OTL_FeatureTag> features = TMP_Settings.fontFeatures;
        if (features != null)
            foreach (OTL_FeatureTag f in new[] { OTL_FeatureTag.mark, OTL_FeatureTag.mkmk })
                if (!features.Contains(f)) { features.Add(f); EditorUtility.SetDirty(TMP_Settings.instance); }
        AssetDatabase.SaveAssets();
        return fa;
    }

    static TMP_FontAsset CreateFontAsset(Font font, string path)
    {
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, 72, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                                                         2048, 2048, AtlasPopulationMode.Dynamic, true);
        fa.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(fa, path);
        if (fa.material != null) AssetDatabase.AddObjectToAsset(fa.material, fa);
        foreach (Texture2D t in fa.atlasTextures) if (t != null) AssetDatabase.AddObjectToAsset(t, fa);
        // Pre-bake the Thai block + Latin so text shows instantly (dynamic still adds anything missing)
        string chars = "";
        for (int cp = 0x0E01; cp <= 0x0E5B; cp++) chars += (char)cp;
        for (int cp = 0x20; cp <= 0x7E; cp++) chars += (char)cp;
        fa.TryAddCharacters(chars + "฿×·▶");
        AssetDatabase.SaveAssets();
        return fa;
    }

    [MenuItem("Tools/Thai Boba Shop/4. Setup Thai Font (Sarabun, or the selected .ttf)")]
    public static void SetupThaiFont()
    {
        Font selected = Selection.activeObject as Font;
        TMP_FontAsset fa;
        if (selected != null)
        {
            fa = CreateFontAsset(selected, Path.ChangeExtension(AssetDatabase.GetAssetPath(selected), null) + " SDF.asset");
            List<TMP_FontAsset> fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks != null && !fallbacks.Contains(fa)) { fallbacks.Add(fa); EditorUtility.SetDirty(TMP_Settings.instance); }
        }
        else fa = EnsureThaiFont();
        AssetDatabase.SaveAssets();
        Debug.Log(fa != null ? "Thai TMP font ready: " + AssetDatabase.GetAssetPath(fa) : "No Thai font found in Assets/Fonts/Thai");
    }
}
