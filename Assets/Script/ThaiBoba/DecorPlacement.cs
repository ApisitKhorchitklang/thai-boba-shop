using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

// Arrange the shop yourself (จัดร้าน), started from the shop computer: aim at a plant, the bench, the table set,
// the promo sign or the dragon jar, pick it up, carry it anywhere on the footpath / shop front, turn it and put it
// down. Spots on the customers' line or their way out (and on top of other things) show red and can't be used.
// The layout is saved with the game.
public class DecorPlacement : MonoBehaviour
{
    public static DecorPlacement Instance { get; private set; }

    [Serializable]
    public class Movable
    {
        public string id;
        public Transform root;
        public float radius = 0.4f;
        [NonSerialized] public Vector3 homePos;
        [NonSerialized] public Quaternion homeRot;
    }

    [Serializable]
    public class Placed { public string id; public float x, z, yaw; }

    public Movable[] items = new Movable[0];
    [Tooltip("World X range things may stand in")]
    public Vector2 allowedX = new Vector2(-8f, 8f);
    [Tooltip("World Z range (footpath + shop front)")]
    public Vector2 allowedZ = new Vector2(-0.9f, 2.6f);
    public float clearance = 0.75f;
    [Tooltip("Customers' way out (after the counter): kept clear like the line")]
    public Transform[] exitPath;
    public Material markerMaterial;
    public float reach = 7f;

    [Header("UI")]
    public GameObject hintBar;
    public TMP_Text hintText;
    public GameObject touchButtons;

    enum Mode { Off, Choosing, Moving }
    Mode mode;
    Movable hover, held;
    bool valid;
    float yaw;
    Renderer marker;
    readonly List<Placed> layout = new List<Placed>();

    public static bool Active => Instance != null && Instance.mode != Mode.Off;

    void Awake()
    {
        Instance = this;
        foreach (Movable m in items)
            if (m.root != null) { m.homePos = m.root.position; m.homeRot = m.root.rotation; }
        if (hintBar != null) hintBar.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ---------------------------------------------------------------- mode
    public void Begin()
    {
        if (mode != Mode.Off) return;
        mode = Mode.Choosing;
        if (OrderPanel.IsOpen) OrderPanel.Instance.Close();
        if (hintBar != null) hintBar.SetActive(true);
        if (touchButtons != null) touchButtons.SetActive(!GameSettings.UsePCControls);
        GameAudio.Play(Sfx.Open);
        UpdateHint();
    }

    public void End()
    {
        if (held != null) PutBack();
        mode = Mode.Off;
        SetHover(null);
        if (marker != null) marker.enabled = false;
        if (hintBar != null) hintBar.SetActive(false);
        if (touchButtons != null) touchButtons.SetActive(false);
        GameAudio.Play(Sfx.Close);
    }

    // a movable counts only while something of it is visible (the table set / bench appear once bought)
    static bool Visible(Movable m)
    {
        if (m.root == null || !m.root.gameObject.activeInHierarchy) return false;
        foreach (Renderer r in m.root.GetComponentsInChildren<Renderer>()) if (r.enabled && !(r is ParticleSystemRenderer)) return true;
        return false;
    }

    // use (E / click / the Use button): pick up what you look at, or put down what you carry
    public void Use()
    {
        if (mode == Mode.Choosing && hover != null) PickUp(hover);
        else if (mode == Mode.Moving) Drop();
    }

    // throw (Q / right click / the Throw button): put the carried thing back where it was
    public void Cancel()
    {
        if (mode == Mode.Moving) { PutBack(); GameAudio.Play(Sfx.Close); }
        else End();
    }

    public void Rotate(float degrees = 45f)
    {
        if (mode == Mode.Moving) { yaw += degrees; GameAudio.Play(Sfx.Click); }
    }

    void PickUp(Movable m)
    {
        held = m;
        mode = Mode.Moving;
        yaw = m.root.eulerAngles.y;
        SetHover(null);
        Enable(m, false);
        GameAudio.Play(Sfx.Cup);
        UpdateHint();
    }

    void Drop()
    {
        if (!valid) { GameAudio.Play(Sfx.Error); ShopHUD.ToastNow(Loc.T("วางตรงนี้ไม่ได้ (ขวางคิว/ทางเดิน หรือชนของอื่น)", "Can't put it here (in the line / walkway, or on something)")); return; }
        Enable(held, true);
        Record(held);
        StatsSystem.Add(StatsSystem.Stat.DecorMove);
        GameAudio.Play(Sfx.Confirm);
        held = null;
        mode = Mode.Choosing;
        if (marker != null) marker.enabled = false;
        UpdateHint();
    }

    void PutBack()
    {
        if (held == null) return;
        Placed p = layout.Find(l => l.id == held.id);
        if (p != null) { held.root.position = new Vector3(p.x, held.root.position.y, p.z); held.root.rotation = Quaternion.Euler(0f, p.yaw, 0f); }
        else held.root.SetPositionAndRotation(held.homePos, held.homeRot);
        Enable(held, true);
        held = null;
        mode = Mode.Choosing;
        if (marker != null) marker.enabled = false;
        UpdateHint();
    }

    // colliders and NavMesh carving off while carried, so it doesn't block its own new spot
    static void Enable(Movable m, bool on)
    {
        foreach (Collider c in m.root.GetComponentsInChildren<Collider>(true)) c.enabled = on;
        foreach (NavMeshObstacle o in m.root.GetComponentsInChildren<NavMeshObstacle>(true)) o.enabled = on;
    }

    // ---------------------------------------------------------------- every frame
    void Update()
    {
        if (mode == Mode.Off) return;
        if (!GameMenus.GameRunning || ShopHUD.SummaryOpen) { End(); return; }
        Camera cam = Camera.main;
        if (cam == null) return;
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        if (GameSettings.UsePCControls && !GameMenus.IsOpen)
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) Rotate(Mathf.Sign(wheel) * 15f);
            if (Input.GetKeyDown(KeyCode.R)) Rotate(45f);
        }
        if (mode == Mode.Choosing) SetHover(Aimed(ray));
        else if (held != null) Carry(ray);
    }

    Movable Aimed(Ray ray)
    {
        Movable best = null;
        float bestD = reach;
        foreach (Movable m in items)
        {
            if (!Visible(m)) continue;
            Bounds b = BoundsOf(m.root);
            b.Expand(0.15f);
            if (b.IntersectRay(ray, out float d) && d < bestD) { bestD = d; best = m; }
        }
        return best;
    }

    void SetHover(Movable m)
    {
        if (hover == m) return;
        if (hover != null && hover.root != null) hover.root.localScale = Vector3.one;
        hover = m;
        UpdateHint();
    }

    void LateUpdate()
    {
        if (mode == Mode.Choosing && hover != null && hover.root != null)
            hover.root.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 8f));    // "this one"
    }

    void Carry(Ray ray)
    {
        float groundY = held.homePos.y;
        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
        if (!ground.Raycast(ray, out float d)) return;
        d = Mathf.Min(d, reach);
        Vector3 p = ray.GetPoint(d);
        p.y = groundY;
        held.root.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
        valid = IsValid(held, p);
        if (marker == null && markerMaterial != null) marker = NightFX.MakeQuad("PlacementMarker", transform, markerMaterial).GetComponent<Renderer>();
        if (marker != null)
        {
            marker.enabled = true;
            marker.transform.SetPositionAndRotation(p + Vector3.up * 0.03f, Quaternion.identity);
            marker.transform.localScale = Vector3.one * (held.radius * 2.6f);
            NightFX.SetColor(marker, valid ? new Color(0.2f, 0.9f, 0.3f, 0.9f) : new Color(1f, 0.15f, 0.1f, 0.9f));
        }
    }

    public bool IsValid(Movable m, Vector3 p)
    {
        if (p.x < allowedX.x || p.x > allowedX.y || p.z < allowedZ.x || p.z > allowedZ.y) return false;
        if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) return false;
        float keep = clearance + m.radius;
        Vector3[] line = CustomerController.Slots;
        if (line != null)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (FlatDist(p, line[i]) < keep) return false;
                if (i > 0 && SegDist(p, line[i - 1], line[i]) < keep) return false;
            }
            if (exitPath != null && line.Length > 0)
            {
                Vector3 prev = line[0];
                foreach (Transform t in exitPath)
                {
                    if (t == null) continue;
                    if (SegDist(p, prev, t.position) < keep) return false;
                    prev = t.position;
                }
            }
        }
        foreach (Movable o in items)
            if (o != m && Visible(o) && FlatDist(p, o.root.position) < m.radius + o.radius) return false;
        foreach (Collider c in Physics.OverlapSphere(p + Vector3.up * (m.radius * 0.8f + 0.3f), m.radius * 0.8f))
            if (!c.isTrigger && !c.transform.IsChildOf(m.root)) return false;                // walls, counter, pillars, the player
        return true;
    }

    static float FlatDist(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

    static float SegDist(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y = a.y = b.y = 0f;
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(p, a + ab * t);
    }

    static Bounds BoundsOf(Transform root)
    {
        Bounds b = new Bounds(root.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    void UpdateHint()
    {
        if (hintText == null) return;
        bool pc = GameSettings.UsePCControls;
        string t;
        if (mode == Mode.Moving)
            t = pc ? Loc.T("วาง: คลิก/E  ·  หมุน: ล้อเมาส์/R  ·  วางคืนที่เดิม: Q", "Place: click/E  ·  Turn: wheel/R  ·  Put back: Q")
                   : Loc.T("เล็งที่พื้นแล้วกด <b>ใช้</b> เพื่อวาง · <b>หมุน</b> · <b>โยน</b> = วางคืนที่เดิม", "Aim at the floor and tap <b>Use</b> to place · <b>Turn</b> · <b>Throw</b> = put back");
        else
            t = (hover != null ? "<b>" + Name(hover.id) + "</b>  ·  " : "") +
                (pc ? Loc.T("จัดร้าน: เล็งที่ของแล้วคลิก/E เพื่อหยิบ  ·  Esc เสร็จ", "Arrange: aim at a decoration and click/E to pick it up  ·  Esc when done")
                    : Loc.T("จัดร้าน: เล็งที่ของแล้วกด <b>ใช้</b> เพื่อหยิบ · กด <b>เสร็จ</b> เมื่อจัดเสร็จ", "Arrange: aim at a decoration and tap <b>Use</b> · tap <b>Done</b> when finished"));
        hintText.text = t;
    }

    static string Name(string id)
    {
        if (id.StartsWith("plant")) return Loc.T("กระถางต้นไม้", "Potted plant");
        switch (id)
        {
            case "promo": return Loc.T("ป้ายโปรโมชั่น", "Promo sign");
            case "dragon_jar": return Loc.T("โอ่งมังกร", "Dragon jar");
            case "bench": return Loc.T("ม้านั่งรอ", "Waiting bench");
            case "seating": return Loc.T("โต๊ะ + เก้าอี้", "Table set");
            default: return id;
        }
    }

    // ---------------------------------------------------------------- save / load
    void Record(Movable m)
    {
        layout.RemoveAll(l => l.id == m.id);
        layout.Add(new Placed { id = m.id, x = m.root.position.x, z = m.root.position.z, yaw = m.root.eulerAngles.y });
    }

    public Placed[] Capture() => layout.ToArray();

    public void Load(Placed[] saved)
    {
        if (mode != Mode.Off) End();
        layout.Clear();
        foreach (Movable m in items)
            if (m.root != null) m.root.SetPositionAndRotation(m.homePos, m.homeRot);
        if (saved == null) return;
        foreach (Placed p in saved)
        {
            Movable m = Array.Find(items, i => i.id == p.id);
            if (m == null || m.root == null) continue;
            m.root.SetPositionAndRotation(new Vector3(p.x, m.homePos.y, p.z), Quaternion.Euler(0f, p.yaw, 0f));
            layout.Add(p);
        }
    }

    public Movable Find(string id) => Array.Find(items, i => i.id == id);
}
