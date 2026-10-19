using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Festival days come alive: passers-by carry the festival's things (water guns and silver bowls at Songkran,
// krathongs on Loy Krathong night, red lanterns at Chinese New Year, jasmine garlands on Mother's Day, yellow flags
// for the vegetarian festival, snack bags at the night market), and small groups gather on the far footpath doing
// the festival: splashing each other, waving lanterns, showing off krathongs, chatting with garlands.
public class FestivalActivities : MonoBehaviour
{
    public static FestivalActivities Instance { get; private set; }

    public GameObject pedestrianPrefab;
    [Tooltip("The two ends of the far footpath (nobody shops there, so groups can stand without blocking the queue)")]
    public Transform endA;
    public Transform endB;
    [Tooltip("Groups along the footpath, and people in each")]
    public int groups = 3;
    public Vector2Int groupSize = new Vector2Int(2, 3);
    [Range(0f, 1f)] public float carryShare = 0.6f;

    readonly List<Group> active = new List<Group>();
    DailyEventSystem.EventType shownFor = DailyEventSystem.EventType.Normal;
    TimeCyc clock;
    ParticleSystem splash;
    static Material waterMat;

    class Group
    {
        public Vector3 centre;
        public readonly List<NPCAnimDriver> people = new List<NPCAnimDriver>();
        public readonly List<GameObject> roots = new List<GameObject>();
        public float timer;
        public int turn;
    }

    public int PeopleOut { get { int n = 0; foreach (Group g in active) n += g.roots.Count; return n; } }
    public DailyEventSystem.EventType ShownFor => shownFor;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }
    void Start() { clock = FindFirstObjectByType<TimeCyc>(); }

    // ---------------------------------------------------------------- what each festival looks like
    public static DailyEventSystem.EventType Today =>
        DailyEventSystem.Instance != null ? DailyEventSystem.Instance.Current.type : DailyEventSystem.EventType.Normal;

    // the hours the festival crowd is out (Loy Krathong and the night market are evening things)
    static bool Hours(DailyEventSystem.EventType t, int h)
    {
        switch (t)
        {
            case DailyEventSystem.EventType.LoyKrathong:
            case DailyEventSystem.EventType.NightMarket: return h >= 17 && h < 23;
            case DailyEventSystem.EventType.Songkran: return h >= 9 && h < 19;
            default: return h >= 8 && h < 21;
        }
    }

    public static string[] PropsFor(DailyEventSystem.EventType t)
    {
        switch (t)
        {
            case DailyEventSystem.EventType.Songkran: return new[] { "watergun", "bowl" };
            case DailyEventSystem.EventType.LoyKrathong: return new[] { "krathong" };
            case DailyEventSystem.EventType.ChineseNewYear: return new[] { "lantern", "oranges" };
            case DailyEventSystem.EventType.MothersDay: return new[] { "garland" };
            case DailyEventSystem.EventType.Vegetarian: return new[] { "flag" };
            case DailyEventSystem.EventType.NightMarket: return new[] { "snackbag" };
            default: return null;
        }
    }

    // what a passer-by carries today (null = nothing special); called by PedestrianWalker
    public static string PickCarry()
    {
        DailyEventSystem.EventType t = Today;
        string[] props = PropsFor(t);
        int h = Instance != null && Instance.clock != null ? Instance.clock.Hours : 12;
        if (props == null || !Hours(t, h)) return null;
        float share = Instance != null ? Instance.carryShare : 0.6f;
        return Random.value < share ? props[Random.Range(0, props.Length)] : null;
    }

    // ---------------------------------------------------------------- groups on the far footpath
    void Update()
    {
        if (clock == null) clock = FindFirstObjectByType<TimeCyc>();
        DailyEventSystem.EventType t = Today;
        bool on = PropsFor(t) != null && GameMenus.GameRunning && clock != null && Hours(t, clock.Hours);
        if (!on || t != shownFor) Clear();
        if (on && active.Count == 0) Gather(t);
        foreach (Group g in active) Animate(g, t);
    }

    void Clear()
    {
        foreach (Group g in active)
            foreach (GameObject r in g.roots) if (r != null) Destroy(r);
        active.Clear();
        shownFor = DailyEventSystem.EventType.Normal;
    }

    public void Gather(DailyEventSystem.EventType t)
    {
        Clear();
        if (pedestrianPrefab == null || endA == null || endB == null) return;
        shownFor = t;
        string[] props = PropsFor(t);
        for (int i = 0; i < groups; i++)
        {
            // spread along the footpath, a little toward the shophouses so walkers pass on the kerb side
            Vector3 along = Vector3.Lerp(endA.position, endB.position, (i + 0.5f) / groups + Random.Range(-0.06f, 0.06f));
            Vector3 toShops = Vector3.Cross(Vector3.up, (endB.position - endA.position).normalized);
            if (!NavMesh.SamplePosition(along + toShops * 0.6f, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
            Group g = new Group { centre = hit.position, timer = Random.Range(0.5f, 2f) };
            int n = Random.Range(groupSize.x, groupSize.y + 1);
            for (int k = 0; k < n; k++)
            {
                float a = (k / (float)n) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                Vector3 p = g.centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.55f;
                if (!NavMesh.SamplePosition(p, out NavMeshHit ph, 0.8f, NavMesh.AllAreas)) continue;
                GameObject person = Instantiate(pedestrianPrefab, ph.position, Quaternion.identity, transform);
                person.name = "FestivalGoer";
                PedestrianWalker w = person.GetComponent<PedestrianWalker>();
                if (w != null) DestroyImmediate(w);                    // they stay and celebrate
                NavMeshAgent ag = person.GetComponent<NavMeshAgent>();
                if (ag != null) { ag.isStopped = true; ag.avoidancePriority = 30; }
                Vector3 face = g.centre - ph.position;
                face.y = 0f;
                if (face.sqrMagnitude > 1e-4f) person.transform.rotation = Quaternion.LookRotation(face.normalized);
                NPCAnimDriver anim = person.GetComponentInChildren<NPCAnimDriver>();
                if (anim != null) { anim.Carry(props[Random.Range(0, props.Length)]); g.people.Add(anim); }
                g.roots.Add(person);
            }
            active.Add(g);
        }
    }

    // turn-taking: one person does the festival thing, the others react
    void Animate(Group g, DailyEventSystem.EventType t)
    {
        g.timer -= Time.deltaTime;
        if (g.timer > 0f || g.people.Count == 0) return;
        g.turn = (g.turn + 1) % g.people.Count;
        NPCAnimDriver doer = g.people[g.turn];
        if (doer == null) return;
        switch (t)
        {
            case DailyEventSystem.EventType.Songkran:
                doer.Play(NPCAnimDriver.Gesture.Wave, 1.2f);                       // throw the water
                Splash(doer.transform.position + Vector3.up * 1.3f, g.centre + Vector3.up * 1.1f);
                foreach (NPCAnimDriver o in g.people)
                    if (o != null && o != doer && Random.value < 0.5f) o.Play(NPCAnimDriver.Gesture.Angry, 0.9f);   // shake the water off, laughing
                g.timer = Random.Range(1.6f, 3.2f);
                break;
            case DailyEventSystem.EventType.ChineseNewYear:
            case DailyEventSystem.EventType.MothersDay:
                doer.Play(NPCAnimDriver.Gesture.Wave, 1.6f);                       // greeting, showing off the lantern / garland
                g.timer = Random.Range(2.5f, 4.5f);
                break;
            case DailyEventSystem.EventType.LoyKrathong:
                doer.Play(Random.value < 0.6f ? NPCAnimDriver.Gesture.Wave : NPCAnimDriver.Gesture.Phone, 2.5f);   // a photo of the krathong
                g.timer = Random.Range(3f, 5f);
                break;
            default:
                doer.Play(Random.value < 0.5f ? NPCAnimDriver.Gesture.Wave : NPCAnimDriver.Gesture.Phone, 2f);
                g.timer = Random.Range(3f, 6f);
                break;
        }
    }

    void Splash(Vector3 from, Vector3 to)
    {
        if (splash == null) splash = MakeSplash();
        splash.transform.position = from;
        Vector3 dir = to - from;
        if (dir.sqrMagnitude > 1e-4f) splash.transform.rotation = Quaternion.LookRotation(dir.normalized);
        splash.Emit(28);
    }

    ParticleSystem MakeSplash()
    {
        GameObject go = new GameObject("SongkranWater");
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = 0.6f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new Color(0.75f, 0.9f, 1f, 0.85f);
        main.gravityModifier = 1.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.04f;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (waterMat == null && sh != null) { waterMat = new Material(sh); waterMat.SetColor("_BaseColor", new Color(0.8f, 0.92f, 1f, 0.8f)); }
        if (waterMat != null) r.sharedMaterial = waterMat;
        return ps;
    }

    // ---------------------------------------------------------------- props (built from primitives, like the phone / cup)
    static Material red, gold, green, white, yellow, silver, orange, blue, paper, stick;

    public static GameObject MakeProp(string kind)
    {
        GameObject root = new GameObject("Prop_" + kind);
        Transform t = root.transform;
        switch (kind)
        {
            case "watergun":
                NPCAnimDriver.Part(PrimitiveType.Cube, t, new Vector3(0f, 0f, 0.08f), new Vector3(0.05f, 0.06f, 0.24f), M(ref blue, new Color(0.15f, 0.55f, 0.95f)));
                NPCAnimDriver.Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.07f, 0.04f), new Vector3(0.08f, 0.05f, 0.08f), M(ref orange, new Color(1f, 0.55f, 0.1f)));
                NPCAnimDriver.Part(PrimitiveType.Cube, t, new Vector3(0f, -0.06f, 0.0f), new Vector3(0.035f, 0.08f, 0.04f), M(ref blue, new Color(0.15f, 0.55f, 0.95f)));
                break;
            case "bowl":
                NPCAnimDriver.Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0f, 0.08f), new Vector3(0.2f, 0.035f, 0.2f), M(ref silver, new Color(0.82f, 0.84f, 0.86f), 0.9f));
                break;
            case "krathong":
                NPCAnimDriver.Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0f, 0.1f), new Vector3(0.18f, 0.03f, 0.18f), M(ref green, new Color(0.3f, 0.62f, 0.25f)));
                NPCAnimDriver.Part(PrimitiveType.Sphere, t, new Vector3(0f, 0.05f, 0.1f), new Vector3(0.1f, 0.06f, 0.1f), M(ref white, new Color(0.98f, 0.96f, 0.9f)));
                NPCAnimDriver.Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.1f, 0.1f), new Vector3(0.012f, 0.04f, 0.012f), M(ref yellow, new Color(1f, 0.85f, 0.2f)));
                break;
            case "lantern":
                NPCAnimDriver.Part(PrimitiveType.Cylinder, t, new Vector3(0f, -0.05f, 0.03f), new Vector3(0.006f, 0.06f, 0.006f), M(ref stick, new Color(0.3f, 0.2f, 0.1f)));
                NPCAnimDriver.Part(PrimitiveType.Sphere, t, new Vector3(0f, -0.17f, 0.03f), new Vector3(0.14f, 0.12f, 0.14f), M(ref red, new Color(0.85f, 0.08f, 0.08f)));
                NPCAnimDriver.Part(PrimitiveType.Cylinder, t, new Vector3(0f, -0.11f, 0.03f), new Vector3(0.06f, 0.008f, 0.06f), M(ref gold, new Color(0.95f, 0.75f, 0.2f), 0.7f));
                break;
            case "oranges":
                NPCAnimDriver.Part(PrimitiveType.Cube, t, new Vector3(0f, -0.08f, 0.04f), new Vector3(0.12f, 0.1f, 0.08f), M(ref red, new Color(0.85f, 0.08f, 0.08f)));
                for (int i = 0; i < 3; i++)
                    NPCAnimDriver.Part(PrimitiveType.Sphere, t, new Vector3(-0.035f + 0.035f * i, -0.02f, 0.04f), Vector3.one * 0.045f, M(ref orange, new Color(1f, 0.55f, 0.1f)));
                break;
            case "garland":
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * Mathf.PI * 2f;
                    NPCAnimDriver.Part(PrimitiveType.Sphere, t, new Vector3(Mathf.Cos(a) * 0.055f, -0.07f + Mathf.Sin(a) * 0.055f, 0.03f), Vector3.one * 0.026f,
                                       i % 5 == 0 ? M(ref red, new Color(0.85f, 0.08f, 0.08f)) : M(ref white, new Color(0.98f, 0.96f, 0.9f)));
                }
                break;
            case "flag":
                NPCAnimDriver.Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.12f, 0.03f), new Vector3(0.01f, 0.2f, 0.01f), M(ref stick, new Color(0.3f, 0.2f, 0.1f)));
                NPCAnimDriver.Part(PrimitiveType.Cube, t, new Vector3(0.07f, 0.26f, 0.03f), new Vector3(0.13f, 0.09f, 0.004f), M(ref yellow, new Color(1f, 0.85f, 0.2f)));
                break;
            case "snackbag":
                NPCAnimDriver.Part(PrimitiveType.Cube, t, new Vector3(0f, -0.1f, 0.03f), new Vector3(0.12f, 0.14f, 0.05f), M(ref paper, new Color(0.95f, 0.93f, 0.85f)));
                break;
            default:
                Object.Destroy(root);
                return null;
        }
        return root;
    }

    static Material M(ref Material m, Color c, float smooth = 0.35f) => NPCAnimDriver.Mat(ref m, c, smooth);
}
