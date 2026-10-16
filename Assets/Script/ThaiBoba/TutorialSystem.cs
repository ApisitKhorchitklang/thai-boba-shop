using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// First-day tutorial. Runs on day 1 of a new game (once; can be replayed from the pause menu).
// The step shown is worked out from the real game state every frame (what's in the cup vs the order), so the
// player can add things in any order, make mistakes and recover. A glowing arrow hovers over the next station.
// While it runs: exactly one customer comes (fixed easy order), nobody loses patience and the clock is paused.
public class TutorialSystem : MonoBehaviour
{
    public static TutorialSystem Instance { get; private set; }
    public const string DoneKey = "tb_tutorial_done";

    [Header("UI")]
    public GameObject panel;
    public TMP_Text stepText;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Button skipButton;
    public Button finishButton;

    [Header("World")]
    public GameObject arrowPrefab;
    public float arrowHeight = 0.12f;

    public enum Step { Look, WaitCustomer, TakeCup, Pour, Boba, Ice, Sweet, Seal, Serve, WrongCup, Done }
    public const int TotalSteps = 9;

    public bool Running { get; private set; }
    public Step Current { get; private set; }
    public static event Action<Step> OnStepChanged;

    TimeCyc clock;
    float savedTimeScale = 1f;
    RaycastAim aim;
    Transform player, cam;
    Vector3 startPos;
    float startYaw, lookTimer, doneTimer;
    bool lookDone, served, replayRequested;
    GameObject arrow;
    Transform arrowTarget;

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (Running) Cleanup();
    }

    void OnEnable() { ShopProgress.OnServed += Served; }
    void OnDisable() { ShopProgress.OnServed -= Served; }

    void Start()
    {
        clock = FindFirstObjectByType<TimeCyc>();
        aim = FindFirstObjectByType<RaycastAim>();
        MovementWithJoy move = FindFirstObjectByType<MovementWithJoy>();
        player = move != null ? move.transform : aim != null ? aim.transform : null;
        cam = Camera.main != null ? Camera.main.transform : null;
        if (skipButton != null) skipButton.onClick.AddListener(Skip);
        if (finishButton != null) finishButton.onClick.AddListener(Finish);
    }

    public static bool Completed => PlayerPrefs.GetInt(DoneKey, 0) == 1;

    // Pause-menu button: run the tutorial again from the next frame
    public void Replay()
    {
        PlayerPrefs.SetInt(DoneKey, 0);
        replayRequested = true;
    }

    void Begin()
    {
        Running = true;
        served = lookDone = false;
        lookTimer = 0f;
        if (player != null) startPos = player.position;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        startYaw = cam != null ? cam.eulerAngles.y : 0f;

        Spawner.TutorialOrder = new Order(1, true, "ThaiTea", DrinkMenu.NextSweetness(100));   // one press of the syrup
        Spawner.TutorialCustomerSpawned = CustomerController.Front != null;   // replay mid-day: teach on the current customer
        Spawner.TutorialActive = true;
        CustomerController.PatienceFrozen = true;
        if (clock != null) { savedTimeScale = clock.timeScale; clock.timeScale = 0f; }

        if (arrowPrefab != null && arrow == null)
        {
            arrow = Instantiate(arrowPrefab);
            arrow.name = "TutorialArrow";
            foreach (Collider c in arrow.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (Renderer r in arrow.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arrow.SetActive(false);
        }
        if (panel != null) panel.SetActive(true);
        SetStep(Step.Look, true);
    }

    void Cleanup()
    {
        Running = false;
        Spawner.TutorialActive = false;
        CustomerController.PatienceFrozen = false;
        if (clock != null && clock.timeScale == 0f) clock.timeScale = savedTimeScale;
        if (arrow != null) Destroy(arrow);
        if (panel != null) panel.SetActive(false);
    }

    public void Skip()
    {
        GameAudio.Play(Sfx.Click);
        PlayerPrefs.SetInt(DoneKey, 1);
        PlayerPrefs.Save();
        Cleanup();
        ShopHUD.Toast(Loc.T("ข้ามบทสอนแล้ว · เล่นอีกครั้งได้จากเมนูหยุดเกม", "Tutorial skipped · replay it from the pause menu"));
    }

    public void Finish()
    {
        GameAudio.Play(Sfx.Confirm);
        PlayerPrefs.SetInt(DoneKey, 1);
        PlayerPrefs.Save();
        Cleanup();
        ShopHUD.Toast(Loc.T("เปิดร้านแล้ว! ลูกค้าจะเริ่มทยอยมา", "The shop is open! Customers will start arriving"));
    }

    void Served(CustomerController c, ServeResult r)
    {
        if (Running) served = true;
    }

    void Update()
    {
        if (!Running)
        {
            bool firstDay = clock != null && clock.Days == 1 && !Completed;
            if (GameMenus.GameRunning && (firstDay || replayRequested))
            {
                replayRequested = false;
                Begin();
            }
            return;
        }
        if (!GameMenus.GameRunning) return;            // back at the main menu: wait

        // PC: the cursor is locked while playing, so the card's buttons have keys (shown on the buttons)
        if (!GameMenus.IsOpen)
        {
            if (Current == Step.Done && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) { Finish(); return; }
            if (Current != Step.Done && Input.GetKeyDown(KeyCode.Tab)) { Skip(); return; }
        }

        Step s = Evaluate();
        if (s != Current) SetStep(s, false);
        else Refresh();
        UpdateArrow();

        if (Current == Step.Done)
        {
            doneTimer += Time.unscaledDeltaTime;
            if (doneTimer > 12f) Finish();                 // don't block the player forever
        }
    }

    // ---------------------------------------------------------------- which step are we on?
    Step Evaluate()
    {
        if (served) return Step.Done;
        if (!lookDone)
        {
            lookTimer += Time.unscaledDeltaTime;
            bool moved = player != null && Vector3.Distance(player.position, startPos) > 1.2f;
            bool looked = cam != null && Mathf.Abs(Mathf.DeltaAngle(cam.eulerAngles.y, startYaw)) > 25f;
            if (moved || looked || lookTimer > 10f || CustomerController.Front != null && lookTimer > 4f) lookDone = true;
            else return Step.Look;
        }
        CustomerController front = CustomerController.Front;
        CupController cup = aim != null ? aim.HeldCup : null;
        if (front == null || front.order == null || (!front.AtCounter && cup == null)) return Step.WaitCustomer;
        Order o = front.order;
        if (cup == null) return Step.TakeCup;
        if (IsWrong(cup, o)) return Step.WrongCup;
        if (string.IsNullOrEmpty(cup.flavor)) return Step.Pour;
        if (cup.bobaCount < o.bobaCount) return Step.Boba;
        if (o.wantsIce && !cup.hasIce) return Step.Ice;
        if (cup.sweetness != o.sweetness) return Step.Sweet;
        if (cup.State != CupController.CupState.LidClosed) return Step.Seal;
        return Step.Serve;
    }

    static bool IsWrong(CupController cup, Order o)
    {
        if (!string.IsNullOrEmpty(cup.flavor) && cup.flavor != o.flavor) return true;
        if (cup.bobaCount > o.bobaCount) return true;
        if (cup.hasIce && !o.wantsIce) return true;
        if (cup.State == CupController.CupState.LidClosed && !cup.MatchesOrder(o)) return true;
        return false;
    }

    void SetStep(Step s, bool first)
    {
        Current = s;
        doneTimer = 0f;
        if (!first) GameAudio.Play(s == Step.WrongCup ? Sfx.Error : s == Step.Done ? Sfx.Goal : Sfx.Confirm);
        if (skipButton != null) skipButton.gameObject.SetActive(s != Step.Done);
        if (finishButton != null) finishButton.gameObject.SetActive(s == Step.Done);
        OnStepChanged?.Invoke(s);
        Refresh();
    }

    // ---------------------------------------------------------------- text
    void Refresh()
    {
        bool pc = GameSettings.UsePCControls;
        string use = pc ? Loc.T("คลิกซ้าย / กด E", "left-click / press E") : Loc.T("กดปุ่ม <b>ใช้</b>", "tap <b>Use</b>");
        string toss = pc ? Loc.T("คลิกขวา / กด Q", "Right-click / press Q") : Loc.T("กดปุ่ม <b>โยน</b>", "Tap <b>Throw</b>");
        CustomerController front = CustomerController.Front;
        Order o = front != null ? front.order : Spawner.TutorialOrder;
        CupController cup = aim != null ? aim.HeldCup : null;
        string tea = o != null && DrinkMenu.Get(o.flavor) != null ? DrinkMenu.Get(o.flavor).Name : Loc.T("ชา", "tea");
        string title, body;
        switch (Current)
        {
            case Step.Look:
                title = Loc.T("ยินดีต้อนรับสู่ร้านชานมไข่มุกของคุณ!", "Welcome to your bubble tea shop!");
                body = pc ? Loc.T("เดินด้วย <b>WASD</b> และขยับ<b>เมาส์</b>เพื่อมองรอบๆ ร้าน", "Walk with <b>WASD</b> and move the <b>mouse</b> to look around")
                          : Loc.T("ใช้<b>จอยซ้ายมือ</b>เดิน และ<b>ลากนิ้วทางขวา</b>ของจอเพื่อมองรอบๆ ร้าน", "Walk with the <b>left joystick</b> and <b>drag on the right</b> of the screen to look around");
                break;
            case Step.WaitCustomer:
                title = Loc.T("ลูกค้าคนแรกกำลังเดินมา", "Your first customer is on the way");
                body = Loc.T("รอที่หลังเคาน์เตอร์ ลูกค้าจะมาสั่งตรงหน้าร้าน · ระหว่างนี้นาฬิกาในเกมหยุดเดิน ไม่ต้องรีบ",
                             "Wait behind the counter, customers order at the front · the clock is paused for now, no rush");
                break;
            case Step.TakeCup:
                title = Loc.T("ลูกค้าสั่ง: ", "Order: ") + (o != null ? o.ToThaiString().Replace("\n", " · ") : "");
                body = Loc.T($"ออเดอร์จะอยู่ที่การ์ดด้านขวาด้วย · เริ่มจาก<b>หยิบแก้ว</b>ที่กองแก้ว แล้ว{use}",
                             $"The order is also on the card at the right · start by <b>taking a cup</b> from the stack: {use}");
                break;
            case Step.Pour:
                title = Loc.T("เท" + tea, "Pour the " + tea);
                body = Loc.T($"ไปที่ถังชาที่มีป้าย<b>{tea}</b> แล้ว{use} · ส่วนผสมใส่สลับลำดับได้",
                             $"Go to the urn labelled <b>{tea}</b> and {use} · ingredients can go in any order");
                break;
            case Step.Boba:
                title = Loc.T($"ตักไข่มุก {o.bobaCount} ช้อน", $"Add {o.bobaCount} scoop{(o.bobaCount > 1 ? "s" : "")} of pearls");
                body = Loc.T($"{use}ที่<b>หม้อไข่มุก</b> · กด 1 ครั้ง = 1 ช้อน (ดูจำนวนได้จาก popup)",
                             $"At the <b>pearl pot</b>, {use} · each press = 1 scoop (the popup shows the count)");
                break;
            case Step.Ice:
                title = Loc.T("ใส่น้ำแข็ง", "Add ice");
                body = Loc.T($"{use}ที่<b>ถังน้ำแข็ง</b>สีแดง", $"At the red <b>ice cooler</b>, {use}");
                break;
            case Step.Sweet:
                title = Loc.T("ปรับความหวานเป็น ", "Set the sweetness to ") + DrinkMenu.SweetTH(o.sweetness);
                string now = cup != null ? DrinkMenu.SweetTH(cup.sweetness) : "-";
                body = Loc.T($"{use}ที่<b>ขวดน้ำเชื่อม</b> ทุกครั้งที่กดจะเปลี่ยนระดับ · ตอนนี้: <b>{now}</b>",
                             $"At the <b>syrup bottles</b>, {use}; each press changes the level · now: <b>{now}</b>");
                break;
            case Step.Seal:
                title = Loc.T("ซีลฝาแก้ว", "Seal the cup");
                body = Loc.T($"เดินไปที่<b>เครื่องซีล</b>บนเคาน์เตอร์ แล้ว{use} · ซีลแล้วจะใส่อะไรเพิ่มไม่ได้",
                             $"Go to the <b>sealer</b> on the counter and {use} · nothing can be added after sealing");
                break;
            case Step.Serve:
                title = Loc.T("เสิร์ฟให้ลูกค้า!", "Serve the customer!");
                body = Loc.T($"{use}ที่<b>เคาน์เตอร์หน้าร้าน</b> · ทำไวและตรงออเดอร์ ได้ทิปเยอะขึ้น",
                             $"At the <b>front counter</b>, {use} · fast and exact drinks earn bigger tips");
                break;
            case Step.WrongCup:
                title = Loc.T("แก้วนี้ไม่ตรงออเดอร์", "This drink doesn't match the order");
                body = Loc.T($"{toss} เพื่อโยนแก้วทิ้ง (หรือทิ้งที่<b>ถังขยะ</b>) แล้วหยิบแก้วใหม่",
                             $"{toss} to throw it away (or use the <b>trash bin</b>), then take a new cup");
                break;
            default:
                title = Loc.T("เยี่ยมมาก! พร้อมเปิดร้านแล้ว", "Great job! You're ready to open");
                body = Loc.T("ลูกค้าจะทยอยมาเรื่อยๆ · ทำ<b>ภารกิจรายวัน</b>ทางซ้ายเพื่อรับโบนัส · ทำถูกติดๆ กันได้<b>คอมโบ</b> · ของใกล้หมดสั่งได้ที่<b>คอมพิวเตอร์</b>ข้างเคาน์เตอร์",
                             "Customers will keep coming · finish the <b>daily goals</b> on the left for bonuses · perfect drinks in a row build a <b>combo</b> · restock at the <b>computer</b> next to the counter");
                break;
        }
        SetButtonLabel(skipButton, pc ? Loc.T("ข้าม", "Skip") + " <size=80%>[Tab]</size>" : Loc.T("ข้าม", "Skip"));
        SetButtonLabel(finishButton, pc ? Loc.T("เริ่มขายเลย!", "Let's sell!") + " <size=80%>[Enter]</size>" : Loc.T("เริ่มขายเลย!", "Let's sell!"));
        if (stepText != null)
            stepText.text = Current == Step.Done ? Loc.T("เสร็จ!", "Done!") : Current == Step.WrongCup ? "!" : $"{(int)Current + 1}/{TotalSteps}";
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;
    }

    static void SetButtonLabel(Button b, string text)
    {
        if (b == null) return;
        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t == null || t.text == text) return;
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSizeMax > 0 ? Mathf.Max(t.fontSizeMax, 26f) : 26f;
        t.fontSizeMin = 16f;
        t.text = text;
    }

    // ---------------------------------------------------------------- arrow over the next station
    void UpdateArrow()
    {
        if (arrow == null) return;
        arrowTarget = TargetFor(Current);
        if (arrowTarget == null) { arrow.SetActive(false); return; }
        Bounds b = new Bounds(arrowTarget.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in arrowTarget.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        float bob = Mathf.Sin(Time.unscaledTime * 4f) * 0.04f;
        arrow.transform.position = new Vector3(b.center.x, b.max.y + arrowHeight + bob, b.center.z);
        arrow.transform.rotation = Quaternion.Euler(0f, Time.unscaledTime * 90f, 0f);
        float scale = Current == Step.WaitCustomer ? 1.6f : 1f;
        arrow.transform.localScale = Vector3.one * scale;
        arrow.SetActive(true);
    }

    Transform TargetFor(Step s)
    {
        CustomerController front = CustomerController.Front;
        switch (s)
        {
            case Step.WaitCustomer: return front != null ? front.transform : Find<ServeStation>();
            case Step.TakeCup: return Find<CupDispenser>();
            case Step.Pour: return Station(IngredientStation.Kind.Flavor, front != null && front.order != null ? front.order.flavor : "ThaiTea");
            case Step.Boba: return Station(IngredientStation.Kind.Boba, null);
            case Step.Ice: return Station(IngredientStation.Kind.Ice, null);
            case Step.Sweet: return Station(IngredientStation.Kind.Sweetness, null);
            case Step.Seal: return Find<SealerStation>();
            case Step.Serve: return Find<ServeStation>();
            case Step.WrongCup: return Find<TrashStation>();
            default: return null;
        }
    }

    static Transform Find<T>() where T : Component
    {
        T c = FindFirstObjectByType<T>();
        return c != null ? c.transform : null;
    }

    static Transform Station(IngredientStation.Kind kind, string flavor)
    {
        Transform best = null;
        foreach (IngredientStation s in FindObjectsByType<IngredientStation>(FindObjectsSortMode.None))
        {
            if (s.kind != kind || flavor != null && s.flavorId != flavor) continue;
            if (best == null || s.name.StartsWith("Urn_")) best = s.transform;    // the big urns are easier to spot
        }
        return best;
    }
}
