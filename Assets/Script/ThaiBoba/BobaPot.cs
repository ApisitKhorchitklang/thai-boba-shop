using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The pearl pot (SM_BobaPot). Pearls have to be cooked before they can be scooped:
//   Empty -> Cooking (cookSeconds) -> Fresh (freshHours of game time) -> Stale (hard, customers notice).
// A batch uses raw pearls from the stock (InventoryManager "boba") and gives that many scoops.
// The pot is emptied every night. A small floating label shows the state; the boiling sound follows it.
public class BobaPot : MonoBehaviour
{
    public enum State { Empty, Cooking, Fresh, Stale }

    public static BobaPot Instance { get; private set; }

    [Header("Cooking")]
    public float cookSeconds = 25f;            // scaled real seconds (a game minute = 1 s)
    public float freshHours = 2.5f;            // in-game hours before the pearls go hard
    public int batch = 30;                     // scoops per batch (50 with the big pot upgrade)
    public int bigPotBatch = 50;

    [Header("Label (set by the scene builder)")]
    public TMP_FontAsset font;

    public State Current { get; private set; } = State.Empty;
    public int Portions { get; private set; }
    public float CookProgress { get; private set; }          // 0..1 while cooking
    public float FreshLeftHours { get; private set; }
    public bool Ready => (Current == State.Fresh || Current == State.Stale) && Portions > 0;
    public bool IsCooking => Current == State.Cooking;

    GameObject pearls, water;
    Renderer[] pearlRenderers;
    MaterialPropertyBlock block;
    TimeCyc clock;
    TMP_Text label;
    Image fill;
    Transform labelRoot;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        Instance = this;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Pot_Pearls") pearls = t.gameObject;
            else if (t.name == "Pot_Water") water = t.gameObject;
        }
        pearlRenderers = pearls != null ? pearls.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        block = new MaterialPropertyBlock();
        BuildLabel();
        Refresh();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (clock != null) clock.OnNewDay -= NewDay;
    }

    void Start()
    {
        clock = FindFirstObjectByType<TimeCyc>();
        if (clock != null) clock.OnNewDay += NewDay;
    }

    void NewDay(int day) { Current = State.Empty; Portions = 0; Refresh(); }   // last night's pearls go in the bin

    int BatchSize => UpgradeShop.Instance != null && UpgradeShop.Instance.Has("bigger_pot") ? bigPotBatch : batch;

    // ---------------------------------------------------------------- actions (from IngredientStation)
    public bool StartCooking()
    {
        if (Current == State.Cooking) return false;
        InventoryManager inv = InventoryManager.Instance;
        int raw = inv != null ? Mathf.Min(inv.boba, BatchSize) : BatchSize;
        if (raw <= 0)
        {
            ShopHUD.ToastNow(Loc.T("ไข่มุกดิบหมด! สั่งเพิ่มที่คอมพิวเตอร์", "Out of raw pearls! Order more at the computer"));
            return false;
        }
        if (inv != null) inv.Add("boba", -raw);
        Portions = raw;
        Current = State.Cooking;
        CookProgress = 0f;
        GameAudio.Play(Sfx.Pour);
        ShopHUD.ToastNow(Loc.T($"เริ่มต้มไข่มุก {raw} ที่ · รอประมาณ {cookSeconds:0} วินาที", $"Cooking {raw} portions of pearls · about {cookSeconds:0} s"));
        Refresh();
        return true;
    }

    public void Discard()
    {
        Current = State.Empty;
        Portions = 0;
        GameAudio.Play(Sfx.Trash);
        ShopHUD.ToastNow(Loc.T("ทิ้งไข่มุกที่แข็งแล้ว · ต้มใหม่ได้เลย", "Threw out the hard pearls · cook a new batch"));
        Refresh();
    }

    // Takes one scoop; returns false when there is nothing to scoop. stale = the pearls had gone hard.
    public bool TryScoop(out bool stale)
    {
        stale = Current == State.Stale;
        if (!Ready) return false;
        Portions--;
        if (Portions <= 0) { Portions = 0; Current = State.Empty; }
        Refresh();
        return true;
    }

    // tests / debug
    public void ForceState(State s, int portions = 30)
    {
        Current = s;
        Portions = s == State.Empty ? 0 : portions;
        CookProgress = s == State.Cooking ? 0f : 1f;
        FreshLeftHours = s == State.Fresh ? freshHours : 0f;
        Refresh();
    }

    void Update()
    {
        if (Current == State.Cooking)
        {
            CookProgress += Time.deltaTime / Mathf.Max(0.1f, cookSeconds);
            if (CookProgress >= 1f)
            {
                CookProgress = 1f;
                Current = State.Fresh;
                FreshLeftHours = freshHours;
                GameAudio.Play(Sfx.Goal);
                ShopHUD.Toast(Loc.T("ไข่มุกสุกแล้ว! ตักได้เลย", "Pearls are ready! Start scooping"));
                Refresh();
            }
        }
        else if (Current == State.Fresh)
        {
            // game time: the clock runs 1 game minute per (1 / timeScale) s; paused clock (tutorial) = no ageing
            float gameMinutes = Time.deltaTime * (clock != null ? clock.timeScale : 1f);
            FreshLeftHours -= gameMinutes / 60f;
            if (FreshLeftHours <= 0f)
            {
                FreshLeftHours = 0f;
                Current = State.Stale;
                ShopHUD.Toast(Loc.T("ไข่มุกในหม้อแข็งแล้ว! ทิ้งแล้วต้มใหม่", "The pearls went hard! Throw them out and cook again"));
                Refresh();
            }
        }
        UpdateLabel();
    }

    void Refresh()
    {
        if (pearls != null) pearls.SetActive(Current == State.Fresh || Current == State.Stale);
        if (water != null) water.SetActive(Current == State.Cooking || Current == State.Empty);
        Color tint = Current == State.Stale ? new Color(0.55f, 0.5f, 0.45f) : Color.white;   // hard pearls look dull
        foreach (Renderer r in pearlRenderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, tint * (r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColor) ? r.sharedMaterial.GetColor(BaseColor) : Color.white));
            r.SetPropertyBlock(block);
        }
        UpdateLabel();
    }

    // ---------------------------------------------------------------- floating status label
    void BuildLabel()
    {
        GameObject go = new GameObject("PotStatus", typeof(Canvas));
        labelRoot = go.transform;
        labelRoot.SetParent(transform, false);
        labelRoot.localPosition = new Vector3(0f, 0.62f, 0f);
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        RectTransform rt = (RectTransform)labelRoot;
        rt.sizeDelta = new Vector2(420, 120);
        rt.localScale = Vector3.one * 0.0011f;

        Image bg = new GameObject("Bg", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        bg.transform.SetParent(labelRoot, false);
        Stretch((RectTransform)bg.transform, 0, 0);
        bg.color = new Color(0.24f, 0.14f, 0.08f, 0.82f);

        fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(labelRoot, false);
        RectTransform frt = (RectTransform)fill.transform;
        frt.anchorMin = new Vector2(0.04f, 0.1f); frt.anchorMax = new Vector2(0.96f, 0.28f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;

        label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.transform.SetParent(labelRoot, false);
        RectTransform lrt = (RectTransform)label.transform;
        lrt.anchorMin = new Vector2(0, 0.3f); lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(10, 0); lrt.offsetMax = new Vector2(-10, -4);
        if (font != null) label.font = font;
        label.fontSize = 34;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.enableWordWrapping = false;
        ThaiText.Enable(label);
    }

    static void Stretch(RectTransform rt, float l, float t)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void UpdateLabel()
    {
        if (label == null) return;
        if (font != null && label.font != font) label.font = font;
        string text; float f; Color col;
        switch (Current)
        {
            case State.Cooking:
                text = Loc.T($"กำลังต้มไข่มุก {CookProgress * 100f:0}%", $"Cooking pearls {CookProgress * 100f:0}%");
                f = CookProgress; col = new Color(1f, 0.75f, 0.25f);
                break;
            case State.Fresh:
                int h = Mathf.FloorToInt(FreshLeftHours), m = Mathf.FloorToInt((FreshLeftHours - h) * 60f);
                text = Loc.T($"ไข่มุกสด · {Portions} ที่ · {h}:{m:00} ชม.", $"Fresh · {Portions} left · {h}:{m:00} h");
                f = FreshLeftHours / Mathf.Max(0.01f, freshHours); col = Color.Lerp(new Color(0.95f, 0.45f, 0.2f), new Color(0.45f, 0.82f, 0.35f), f);
                break;
            case State.Stale:
                text = Loc.T($"ไข่มุกแข็งแล้ว! ({Portions} ที่)", $"Pearls went hard! ({Portions})");
                f = 1f; col = new Color(0.85f, 0.25f, 0.25f);
                break;
            default:
                text = Loc.T("หม้อว่าง · ต้มไข่มุกก่อน", "Pot empty · cook pearls");
                f = 0f; col = Color.gray;
                break;
        }
        if (label.text != text) label.text = text;
        fill.fillAmount = f;
        fill.color = col;
        Camera cam = Camera.main;
        if (cam != null) labelRoot.rotation = cam.transform.rotation;      // face the player
    }
}
