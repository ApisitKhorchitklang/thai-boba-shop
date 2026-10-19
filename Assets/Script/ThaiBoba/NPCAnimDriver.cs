using UnityEngine;
using UnityEngine.AI;

// Feeds NavMeshAgent speed into the NPC Animator ("Speed" float: Idle <-> Walk) and syncs the walk cycle speed.
// Upper-body gestures (phone, watch, sip, angry, wave, chop, shake) play on the controller's second layer,
// faded in only while one is active, with the matching prop (phone / takeaway cup / cleaver / shaker) in the right hand.
public class NPCAnimDriver : MonoBehaviour
{
    public enum Gesture { None = 0, Phone = 1, Watch = 2, Sip = 3, Angry = 4, Wave = 5, Chop = 6, Shake = 7 }

    [Tooltip("Agent speed (m/s) at which the walk clip plays at normal speed")]
    public float walkClipSpeed = 1.3f;

    NavMeshAgent agent;
    Animator animator;
    Gesture gesture;
    float layerWeight;
    float gestureUntil = -1f;
    GameObject prop;
    Gesture propFor;
    Color cupColor = new Color(0.93f, 0.45f, 0.1f);

    public Gesture Current => gesture;

    // walked by script with the agent off (customers inside the shop): speed for the walk cycle, < 0 = use the agent
    [System.NonSerialized] public float SpeedOverride = -1f;
    // on a stool (the controller's "Sit" state); gestures still play on the upper body
    [System.NonSerialized] public bool Sitting;
    bool hasSit;
    // the Animator doesn't move the Hips bone on these Generic rigs (only rotations come through), so the pelvis
    // drop onto the stool is applied here: the seated hips are sampled once from the Sit clip, then eased in / out
    Transform hips;
    Vector3 restHips, seatedHips;
    bool seatedHipsKnown;
    float sitBlend;

    static Material phoneMat, strawMat, lidMat, steelMat, woodMat;

    void Start()
    {
        agent = GetComponentInParent<NavMeshAgent>();
    }

    // seconds <= 0: until changed
    public void Play(Gesture g, float seconds = 0f)
    {
        gesture = g;
        gestureUntil = seconds > 0f ? Time.time + seconds : -1f;
    }

    public void SetCupColor(Color c) { cupColor = c; if (prop != null && propFor == Gesture.Sip) Destroy(prop); }

    void Update()
    {
        // CustomerAppearance enables one model on Start, so look up the active Animator lazily
        if (animator == null || !animator.isActiveAndEnabled)
        {
            animator = null;
            foreach (Animator a in GetComponentsInChildren<Animator>())
                if (a.isActiveAndEnabled) { animator = a; break; }
            if (animator == null) return;
            animator.applyRootMotion = false;
            if (prop != null) Destroy(prop);
            hasSit = false;
            hips = Find(animator.transform, "Hips");
            if (hips != null) restHips = hips.localPosition;
            seatedHipsKnown = false;
            sitBlend = 0f;
            foreach (AnimatorControllerParameter p in animator.parameters) if (p.name == "Sit") hasSit = true;
        }

        if (gestureUntil > 0f && Time.time > gestureUntil) { gesture = Gesture.None; gestureUntil = -1f; }

        float speed = SpeedOverride >= 0f ? SpeedOverride : agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        animator.SetFloat("Speed", speed);
        if (hasSit) animator.SetBool("Sit", Sitting);
        animator.speed = speed > 0.2f ? Mathf.Clamp(speed / walkClipSpeed, 0.6f, 1.8f) : 1f;

        if (animator.layerCount > 1)
        {
            animator.SetInteger("Gesture", (int)gesture);
            layerWeight = Mathf.MoveTowards(layerWeight, gesture != Gesture.None ? 1f : 0f, Time.deltaTime * 4f);
            animator.SetLayerWeight(1, layerWeight);
        }
        UpdateProp();
    }

    void LateUpdate()
    {
        if (hips == null || animator == null) return;
        if (Sitting && !seatedHipsKnown)
        {
            AnimationClip sit = null;
            foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips) if (c.name == "Sit") sit = c;
            if (sit == null) return;
            Quaternion keep = hips.localRotation;
            sit.SampleAnimation(animator.gameObject, 0f);           // the Animator rewrites every rotation next frame
            seatedHips = hips.localPosition;
            hips.localRotation = keep;
            seatedHipsKnown = true;
        }
        sitBlend = Mathf.MoveTowards(sitBlend, Sitting ? 1f : 0f, Time.deltaTime / 0.35f);    // same as the transition
        if (seatedHipsKnown) hips.localPosition = Vector3.Lerp(restHips, seatedHips, Mathf.SmoothStep(0f, 1f, sitBlend));
    }

    // ---------------------------------------------------------------- props in the right hand
    // Something carried while no gesture needs the hand: a festival prop (FestivalActivities.MakeProp)
    string carry;
    bool carryShown;

    public string Carrying => carry;

    public void Carry(string kind)
    {
        if (carry == kind) return;
        carry = kind;
        if (prop != null && carryShown) { Destroy(prop); prop = null; }
    }

    void UpdateProp()
    {
        Gesture want = gesture == Gesture.Phone || gesture == Gesture.Sip || gesture == Gesture.Chop || gesture == Gesture.Shake ? gesture : Gesture.None;
        bool wantCarry = want == Gesture.None && !string.IsNullOrEmpty(carry);
        if (prop != null && (propFor != want || carryShown != wantCarry)) { Destroy(prop); prop = null; }
        if ((want == Gesture.None && !wantCarry) || prop != null || animator == null) return;
        Transform hand = Find(animator.transform, "Hand.R"), fore = Find(animator.transform, "LowerArm.R");
        if (hand == null || fore == null) return;
        if (wantCarry)
        {
            prop = FestivalActivities.MakeProp(carry);
            if (prop == null) { carry = null; return; }
            propFor = Gesture.None;
            carryShown = true;
            Vector3 d = (hand.position - fore.position).normalized;
            prop.transform.SetParent(hand, true);
            prop.transform.position = hand.position + d * 0.07f;
            prop.transform.rotation = animator.transform.rotation;
            return;
        }
        carryShown = false;
        prop = want == Gesture.Phone ? MakePhone() : want == Gesture.Chop ? MakeCleaver() : want == Gesture.Shake ? MakeShaker() : MakeCup(cupColor);
        propFor = want;
        Vector3 dir = (hand.position - fore.position).normalized;
        prop.transform.SetParent(hand, true);
        prop.transform.position = hand.position + dir * 0.06f;
        Transform head = Find(animator.transform, "Head");
        if (want == Gesture.Phone && head != null)
            prop.transform.rotation = Quaternion.LookRotation((head.position - prop.transform.position).normalized, animator.transform.up);
        else
            prop.transform.rotation = animator.transform.rotation;
    }

    static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    public static Material Mat(ref Material m, Color c, float smooth)
    {
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        return m;
    }

    public static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    static GameObject MakePhone()
    {
        GameObject root = new GameObject("Prop_Phone");
        Part(PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(0.072f, 0.145f, 0.009f), Mat(ref phoneMat, new Color(0.05f, 0.05f, 0.07f), 0.85f));
        return root;
    }

    static GameObject MakeCleaver()
    {
        GameObject root = new GameObject("Prop_Cleaver");
        Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0f, 0.05f), new Vector3(0.026f, 0.03f, 0.1f), Mat(ref woodMat, new Color(0.35f, 0.2f, 0.1f), 0.3f));
        Part(PrimitiveType.Cube, root.transform, new Vector3(0f, -0.035f, 0.15f), new Vector3(0.004f, 0.09f, 0.16f), Mat(ref steelMat, new Color(0.75f, 0.77f, 0.8f), 0.85f));
        return root;
    }

    static GameObject MakeShaker()
    {
        GameObject root = new GameObject("Prop_Shaker");
        Material steel = Mat(ref steelMat, new Color(0.75f, 0.77f, 0.8f), 0.85f);
        Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.075f, 0.075f, 0.075f), steel);
        Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.09f, 0f), new Vector3(0.06f, 0.03f, 0.06f), steel);
        return root;
    }

    static readonly System.Collections.Generic.Dictionary<Color, Material> cupMats = new System.Collections.Generic.Dictionary<Color, Material>();

    GameObject MakeCup(Color drink)
    {
        GameObject root = new GameObject("Prop_Cup");
        if (!cupMats.TryGetValue(drink, out Material m))
        {
            Material tmp = null;
            m = Mat(ref tmp, Color.Lerp(drink, Color.white, 0.12f), 0.7f);
            cupMats[drink] = m;
        }
        Part(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.02f, 0), new Vector3(0.068f, 0.065f, 0.068f), m);
        Part(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.088f, 0), new Vector3(0.074f, 0.004f, 0.074f), Mat(ref lidMat, new Color(1f, 0.85f, 0.92f), 0.6f));
        GameObject straw = Part(PrimitiveType.Cylinder, root.transform, new Vector3(0.012f, 0.13f, 0), new Vector3(0.011f, 0.06f, 0.011f), Mat(ref strawMat, new Color(0.95f, 0.35f, 0.55f), 0.4f));
        straw.transform.localRotation = Quaternion.Euler(0, 0, -8f);
        return root;
    }

    void OnDestroy()
    {
        if (prop != null) Destroy(prop);
    }
}
