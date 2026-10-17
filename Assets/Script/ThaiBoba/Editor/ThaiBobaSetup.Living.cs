using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// The living soi: the neighbour's chicken-rice stall, stray cats, the rival / second branch across the street,
// festival decor, firecrackers, delivery riders, the barista, night reflections, free decor placement, story mode.
// Things that move or come and go live under "LivingSoi" (outside the NavMesh bake).
public static partial class ThaiBobaSetup
{
    // additive soft glow (headlight pools, flares, wet reflections, placement marker)
    static Material GlowMaterial()
    {
        string path = FxDir + "M_FX_Glow_Add.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Directory.CreateDirectory(FxDir);
        m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetTexture("_BaseMap", SoftTexture());
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
        m.SetColor("_BaseColor", Color.white);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static RuntimeAnimatorController LoadController(string model) =>
        AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + model + ".controller") ?? NpcController(model);

    // a standing NPC: root (position / yaw) > Visual (NPCAnimDriver) > the rigged character
    static GameObject NpcInstance(string model, Transform parent, Vector3 localPos, float yaw, bool obstacle = false)
    {
        GameObject root = new GameObject(model + "_Npc");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CharDir + model + ".fbx");
        if (asset != null)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, visual.transform);
            inst.name = model;
            Animator a = inst.GetComponent<Animator>();
            if (a == null) a = inst.AddComponent<Animator>();
            a.runtimeAnimatorController = LoadController(model);
            a.applyRootMotion = false;
            a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            SetupLods(inst, 0.11f, 0.012f);
        }
        visual.AddComponent<NPCAnimDriver>();
        if (obstacle)
        {
            NavMeshObstacle o = root.AddComponent<NavMeshObstacle>();
            o.shape = NavMeshObstacleShape.Capsule;
            o.radius = 0.3f;
            o.height = 1.7f;
            o.center = new Vector3(0f, 0.85f, 0f);
            o.carving = true;
        }
        return root;
    }

    // Unity yaw for something at Blender (x0, y0) looking at Blender (x1, y1)
    static float YawTo(float x0, float y0, float x1, float y1)
    {
        Vector3 d = P(x1, y1, 0) - P(x0, y0, 0);
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    static Transform Mark(string name, Transform parent, float bx, float by, float bz)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = P(bx, by, bz);
        return t;
    }

    static TextMeshPro WorldLabel(Transform parent, Vector3 local, float size)
    {
        GameObject g = new GameObject("Speech");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = local;
        TextMeshPro tmp = g.AddComponent<TextMeshPro>();
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(3.2f, 1f);
        tmp.color = Color.white;
        tmp.text = "";
        TMP_FontAsset thai = EnsureThaiFont();
        if (thai != null) tmp.font = thai;
        ThaiText.Enable(tmp);
        return tmp;
    }

    // headlight / tail-light positions, measured from the lamp materials of a vehicle model (root space)
    static void MeasureLights(GameObject root, out Vector3[] heads, out Vector3[] tails)
    {
        var lens = new List<Vector3>();
        var tail = new List<Vector3>();
        foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.name.Contains("_LOD1")) continue;
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Mesh m = mf.sharedMesh;
            Material[] mats = mr.sharedMaterials;
            Vector3[] v = m.vertices;
            for (int s = 0; s < m.subMeshCount && s < mats.Length; s++)
            {
                string n = mats[s] != null ? mats[s].name : "";
                bool isLens = n.StartsWith("M_lamp_lens") || n.StartsWith("M_headlight"), isTail = n.StartsWith("M_lamp_tail");
                if (!isLens && !isTail) continue;
                foreach (int i in m.GetIndices(s))
                    (isLens ? lens : tail).Add(root.transform.InverseTransformPoint(mr.transform.TransformPoint(v[i])));
            }
        }
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            Bounds rb = new Bounds(root.transform.InverseTransformPoint(r.bounds.center), r.bounds.size);
            if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
        }
        float cut = b.size.z * 0.25f;
        heads = Cluster(lens.FindAll(p => p.z > b.center.z + cut));
        tails = Cluster(tail.FindAll(p => p.z < b.center.z - cut));
    }

    static Vector3[] Cluster(List<Vector3> pts)
    {
        if (pts.Count == 0) return new Vector3[0];
        var left = pts.FindAll(p => p.x < -0.15f);
        var right = pts.FindAll(p => p.x > 0.15f);
        Vector3 Mean(List<Vector3> l) { Vector3 s = Vector3.zero; foreach (Vector3 p in l) s += p; return s / l.Count; }
        if (left.Count > 0 && right.Count > 0) return new[] { Mean(left), Mean(right) };
        return new[] { Mean(pts) };
    }

    static void AddVehicleLights(GameObject root, string model)
    {
        MeasureLights(root, out Vector3[] heads, out Vector3[] tails);
        if (heads.Length == 0 && tails.Length == 0) return;
        VehicleLights vl = root.AddComponent<VehicleLights>();
        vl.glowMaterial = GlowMaterial();
        vl.headlights = heads;
        vl.taillights = tails;
        bool small = model.Contains("Scooter");
        vl.poolLength = small ? 4.5f : 7f;
        vl.poolWidth = small ? 1.8f : 3.2f;
    }

    static GameObject FarBuildingAt(Transform root, string model, float bx, float by)
    {
        Vector3 want = P(bx, by, 0);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == model && Vector3.Distance(t.position, want) < 0.5f) return t.gameObject;
        return null;
    }

    public static void BuildLivingSoi(StreetRefs refs, GameObject sys, FxSet fx)
    {
        Transform street = refs.root.transform;
        Transform living = new GameObject("LivingSoi").transform;
        Material glow = GlowMaterial();
        Transform shopInterior = street.Find("ShopInterior");

        // ---------------------------------------------------------------- the chicken-rice stall next door
        GameObject stall = Place("SM_ChickenRiceStall", 3.3f, -0.55f, 0, 0, street, Col.None);
        GameObject shutter = Place("SM_RollShutter", 4.6f, 0, 0, 0, street, Col.None);
        if (stall != null)
        {
            Transform body = Child(stall, "Stall_Body"), food = Child(stall, "Stall_Food"), tarp = Child(stall, "Stall_Tarp");
            if (body != null) body.gameObject.AddComponent<BoxCollider>();
            if (food != null && fx != null && fx.steam != null) AddChildFx(fx.steam, food, P(-0.35f, 0.18f, 1.2f));
            NeighbourStall ns = stall.AddComponent<NeighbourStall>();
            ns.food = food != null ? food.gameObject : null;
            ns.tarp = tarp != null ? tarp.gameObject : null;
            ns.shutterDoor = shutter != null ? Child(shutter, "Shutter_Door") : null;
            ns.chopPoint = Mark("ChopPoint", living, 3.75f, -0.42f, 0.95f);
            ns.vendor = NpcInstance("NPC_Uncle", living, P(3.75f, 0.05f, 0), 0f);          // right behind the chopping board
            // riders wait just inside the open shop front, off the footpath (carving the walkway would cut the soi in two)
            GameObject r1 = NpcInstance("NPC_Rider", living, P(5.1f, 0.75f, 0), YawTo(5.1f, 0.75f, 3.6f, -0.4f));
            GameObject r2 = NpcInstance("NPC_Rider", living, P(5.9f, 0.95f, 0), YawTo(5.9f, 0.95f, 4.6f, 3.0f));
            foreach (GameObject r in new[] { r1, r2 }) r.AddComponent<IdleGestures>().phoneShare = 0.8f;
            GameObject parked = Place("SM_Traffic_Scooter", 6.1f, -3.4f, 0, 90, living, Col.None);
            ns.lunchRiders = new[] { r1, r2, parked };
            foreach (GameObject g in ns.lunchRiders) if (g != null) g.SetActive(false);
        }

        // ---------------------------------------------------------------- stray cats
        GameObject catAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "SM_Cat.fbx");
        if (catAsset != null)
        {
            Transform spotRoot = new GameObject("CatSpots").transform;
            spotRoot.SetParent(living, false);
            var spots = new List<Transform>();
            float[,] sp = { { 2.9f, -1.95f }, { -4.0f, -0.3f }, { 4.3f, -1.5f }, { -5.9f, -1.3f }, { 7.0f, -2.2f }, { -6.6f, -0.7f }, { 9.5f, -1.0f } };
            for (int i = 0; i < sp.GetLength(0); i++) spots.Add(Mark("CatSpot" + i, spotRoot, sp[i, 0], sp[i, 1], 0));
            Color[] furs = { new Color(0.92f, 0.55f, 0.22f), new Color(0.13f, 0.12f, 0.12f), new Color(0.6f, 0.6f, 0.62f) };
            float[,] start = { { 2.9f, -1.9f }, { -4.2f, -0.35f }, { 4.4f, -1.5f } };
            for (int k = 0; k < 3; k++)
            {
                GameObject cat = new GameObject("StrayCat_" + k);
                cat.transform.SetParent(living, false);
                cat.transform.localPosition = P(start[k, 0], start[k, 1], 0);
                cat.transform.localRotation = Quaternion.Euler(0f, k * 120f, 0f);
                NavMeshAgent ag = cat.AddComponent<NavMeshAgent>();
                ag.radius = 0.15f;
                ag.height = 0.35f;
                ag.speed = 0.75f;
                ag.avoidancePriority = 99;
                GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(catAsset, cat.transform);
                m.transform.localPosition = Vector3.zero;
                CapsuleCollider cc = cat.AddComponent<CapsuleCollider>();
                cc.direction = 2;
                cc.radius = 0.16f;
                cc.height = 0.55f;
                cc.center = new Vector3(0f, 0.2f, 0f);
                CustomerEmote em = cat.AddComponent<CustomerEmote>();
                em.height = 0.6f;
                em.size = 0.24f;
                em.love = S("emote_love"); em.happy = S("emote_happy"); em.meh = S("emote_meh"); em.angry = S("emote_angry"); em.camera = S("emote_camera");
                StrayCat sc = cat.AddComponent<StrayCat>();
                sc.spots = spots.ToArray();
                sc.fur = furs[k];
                sc.walkSpeed = 0.65f + 0.15f * k;
            }
        }

        // ---------------------------------------------------------------- across the street: empty / rival / branch 2
        RivalShop rival = new GameObject("RivalShop").AddComponent<RivalShop>();
        rival.transform.SetParent(living, false);
        rival.closedFront = FarBuildingAt(street, "SM_Shophouse_Closed", 9.2f, -13.4f);
        Transform far = street.Find("Environment/FarSide") ?? street;
        GameObject rivalGroup = new GameObject("RivalFront");
        rivalGroup.transform.SetParent(far, false);
        Place("SM_Shop_Rival", 9.2f, -13.4f, 0, 180, rivalGroup.transform, Col.Mesh);
        foreach (float wx in new[] { 7.6f, 10.8f })
        {
            GameObject w = Place("SM_OpeningWreath", wx, -12.5f, 0, 180, rivalGroup.transform, Col.None);
            Obstacle(w, new Vector3(0.8f, 1.8f, 0.8f));
        }
        GameObject rivalStaff = NpcInstance("NPC_Influencer", rivalGroup.transform, P(9.2f, -14.75f, 0), 180f);
        rivalStaff.AddComponent<IdleGestures>().phoneShare = 0.3f;
        rival.rivalFront = rivalGroup;
        GameObject branchGroup = new GameObject("Branch2Front");
        branchGroup.transform.SetParent(far, false);
        Place("SM_Shop_Branch2", 9.2f, -13.4f, 0, 180, branchGroup.transform, Col.Mesh);
        NpcInstance("NPC_Student", branchGroup.transform, P(9.2f, -14.75f, 0), 180f).AddComponent<IdleGestures>().phoneShare = 0.2f;
        rival.branchFront = branchGroup;
        var queue = new List<GameObject>();
        string[] qm = { "NPC_Student", "NPC_Office", "NPC_Tourist" };
        for (int i = 0; i < 3; i++)
        {
            GameObject q = NpcInstance(qm[i], living, P(8.5f + i * 0.7f, -12.7f - (i == 1 ? 0.05f : 0f), 0), 180f, true);
            q.AddComponent<IdleGestures>();
            q.SetActive(false);
            queue.Add(q);
        }
        rival.rivalQueue = queue.ToArray();
        rivalGroup.SetActive(false);
        branchGroup.SetActive(false);

        // ---------------------------------------------------------------- story: the first cart at the corner
        GameObject cart = Place("SM_BobaCart", -9.6f, -0.5f, 0, 0, street, Col.Box);
        if (cart != null) cart.AddComponent<FirstCartMemento>();

        // ---------------------------------------------------------------- festival decor (hidden until the day)
        DailyEventSystem events = sys.GetComponent<DailyEventSystem>();
        GameObject Festive(string name, string model, float nearY, float farY, float z)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(street, false);
            for (int k = -3; k <= 3; k++)
            {
                Place(model, k * 4.6f, nearY, z, 0, g.transform, Col.None);
                Place(model, k * 4.6f, farY, z, 180, g.transform, Col.None);
            }
            g.SetActive(false);
            return g;
        }
        if (events != null)
        {
            events.chineseNewYearDecor = Festive("ChineseNewYearDecor", "SM_RedLanterns", -0.75f, -12.65f, 3.0f);
            events.mothersDayDecor = Festive("MothersDayDecor", "SM_MotherBunting", -1.5f, -11.9f, 2.66f);
            events.vegetarianDecor = Festive("VegetarianDecor", "SM_JayFlags", -1.5f, -11.9f, 2.66f);
        }
        FestivalFX ffx = new GameObject("FestivalFX").AddComponent<FestivalFX>();
        ffx.transform.SetParent(living, false);
        ffx.firecrackerSpots = new[] { Mark("Firecracker_A", living, -3.0f, -3.3f, 0.05f), Mark("Firecracker_B", living, 4.0f, -3.3f, 0.05f), Mark("Firecracker_C", living, 0.8f, -3.6f, 0.05f) };
        GameObject sparks = FxPrefab("FX_Firecracker", (ps, r) =>
        {
            var main = ps.main;
            main.duration = 3.2f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.1f));
            main.gravityModifier = 0.6f;
            main.maxParticles = 600;
            var em = ps.emission; em.rateOverTime = 120f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.25f;
        });
        GameObject sparkInst = (GameObject)PrefabUtility.InstantiatePrefab(sparks, living);
        ffx.firecrackerSparks = sparkInst.GetComponent<ParticleSystem>();

        // ---------------------------------------------------------------- delivery: pick-up rack, rider, scooter
        GameObject rack = Place("SM_PickupRack", 1.08f, 1.32f, 1.0f, 0, shopInterior != null ? shopInterior : street, Col.Box);
        DeliverySystem ds = new GameObject("Delivery").AddComponent<DeliverySystem>();
        ds.transform.SetParent(living, false);
        if (rack != null)
        {
            rack.AddComponent<PickupRack>();
            var slots = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                Transform s = new GameObject("Slot" + i).transform;
                s.SetParent(rack.transform, false);
                s.localPosition = P(-0.105f + i * 0.105f, 0f, 0.022f);
                slots.Add(s);
            }
            ds.rackSlots = slots.ToArray();
        }
        GameObject scooterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "SM_Traffic_Scooter.fbx");
        if (scooterAsset != null)
        {
            GameObject sc = new GameObject("DeliveryScooter");
            sc.transform.SetParent(living, false);
            GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(scooterAsset, sc.transform);
            SeatRiders(body, "SM_Traffic_Scooter");
            sc.AddComponent<WheelSpin>();
            AudioSource src = sc.AddComponent<AudioSource>();
            src.clip = Clip("sfx_moto_pass");
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 3f;
            src.maxDistance = 32f;
            src.dopplerLevel = 0f;
            ds.scooter = sc.transform;
            foreach (Transform t in sc.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("SEAT_Rider")) { ds.scooterRider = t.gameObject; break; }
            sc.SetActive(false);
        }
        GameObject rider = NpcInstance("NPC_Rider", living, P(3.4f, -2.6f, 0), 0f);
        rider.name = "DeliveryRider";
        NavMeshAgent ra = rider.AddComponent<NavMeshAgent>();
        ra.radius = 0.25f;
        ra.height = 1.7f;
        ra.speed = 1.6f;
        ra.avoidancePriority = 30;
        ds.rider = ra;
        rider.SetActive(false);
        ds.parkPoint = Mark("Delivery_Park", living, 3.4f, -3.45f, 0);
        ds.arriveFrom = Mark("Delivery_From", living, -32f, -3.45f, 0);
        ds.leaveTo = Mark("Delivery_To", living, 40f, -3.45f, 0);
        ds.riderWait = Mark("Delivery_Wait", living, 1.45f, 0.45f, 0);

        // ---------------------------------------------------------------- the barista at the left end of the counter
        GameObject barista = new GameObject("Barista");
        barista.transform.SetParent(living, false);
        barista.transform.localPosition = P(-0.95f, 2.05f, 0);
        BaristaStaff bs = barista.AddComponent<BaristaStaff>();
        GameObject bBody = NpcInstance("NPC_UniStudent", barista.transform, Vector3.zero, 0f);
        CapsuleCollider bcc = bBody.AddComponent<CapsuleCollider>();
        bcc.center = new Vector3(0f, 0.85f, 0f);
        bcc.radius = 0.25f;
        bcc.height = 1.7f;
        bs.body = bBody;
        DecorShop shopItems = sys.GetComponent<DecorShop>();
        if (shopItems != null && shopItems.Get("barista") != null) shopItems.Get("barista").show = bBody;
        bs.speech = WorldLabel(barista.transform, new Vector3(0f, 2.15f, 0f), 2.0f);
        bBody.SetActive(false);

        // ---------------------------------------------------------------- wet-night reflections of lamps and signs
        NightReflections nr = new GameObject("NightReflections").AddComponent<NightReflections>();
        nr.transform.SetParent(living, false);
        nr.glowMaterial = glow;
        var srcs = new List<Transform>();
        var cols = new List<Color>();
        foreach (Transform t in street.GetComponentsInChildren<Transform>(true))
            if (t.name == "LampLight") { srcs.Add(t); cols.Add(new Color(1f, 0.72f, 0.4f)); }
        (float x, float y, Color c)[] signs =
        {
            (0f, -0.2f, new Color(1f, 0.35f, 0.65f)), (4.6f, -0.2f, new Color(1f, 0.3f, 0.25f)), (-4.6f, -0.2f, new Color(0.35f, 1f, 0.5f)),
            (0f, -13.2f, new Color(0.4f, 0.65f, 1f)), (4.6f, -13.2f, new Color(1f, 0.85f, 0.4f)), (-4.6f, -13.2f, new Color(1f, 0.9f, 0.4f)),
            (2.15f, -0.3f, new Color(0.3f, 0.95f, 1f)),
        };
        foreach (var s in signs) { srcs.Add(Mark("SignGlow", living, s.x, s.y, 3.9f)); cols.Add(s.c); }
        nr.sources = srcs.ToArray();
        nr.colors = cols.ToArray();

        // ---------------------------------------------------------------- systems
        if (sys.GetComponent<StatsSystem>() == null) sys.AddComponent<StatsSystem>();
        StoryMode story = sys.GetComponent<StoryMode>() ?? sys.AddComponent<StoryMode>();
        story.firstCart = cart;
        DecorPlacement dp = sys.GetComponent<DecorPlacement>() ?? sys.AddComponent<DecorPlacement>();
        dp.items = refs.movables.ToArray();
        dp.exitPath = refs.exitWaypoints != null && refs.exitWaypoints.Length > 0 ? new[] { refs.exitPoint, refs.exitWaypoints[0] } : new[] { refs.exitPoint };
        dp.markerMaterial = glow;
    }
}
