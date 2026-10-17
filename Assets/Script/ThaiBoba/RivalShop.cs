using System;
using UnityEngine;

// The rival across the street (ชาไข่มุกเจ้าใหม่). From day 8 (and again three weeks after it closes) a new boba shop
// opens in the empty shophouse opposite: flower stands, balloons, a little queue of its own. While it is open it
// pulls customers away (fewer people stop at your shop). Win them back: perfect drinks, happy regulars and
// delivery orders chip away at its pull, angry customers feed it; a promo (flyers + a sign, bought at the shop
// computer) doubles the effect for the day. When the pull reaches zero it closes down: bonus, reputation, a medal.
// In the story's last chapter your second branch opens in that building.
public class RivalShop : MonoBehaviour
{
    public static RivalShop Instance { get; private set; }

    [Header("The building opposite (one is shown at a time)")]
    public GameObject closedFront;            // the empty shophouse ("ให้เช่า")
    public GameObject rivalFront;             // SM_Shop_Rival + wreaths
    public GameObject branchFront;            // SM_Shop_Branch2 (story ending)
    [Tooltip("People queuing at the rival (shown according to its pull)")]
    public GameObject[] rivalQueue;

    [Header("Rules")]
    public int firstDay = 8;
    public int returnAfter = 21;
    public float startPull = 0.6f;
    public int promoCost = 300;

    [Range(0f, 1f)] public float pull;
    public bool Open { get; private set; }
    public bool PromoToday { get; private set; }
    public int NextOpenDay { get; private set; }
    public bool BranchOpen { get; private set; }

    public static event Action<bool> OnOpenChanged;             // true = opened, false = beaten

    TimeCyc clock;
    int lastToastStep = -1;
    int lostToday;
    float lostCarry;

    // fewer customers stop at your shop while the rival is popular (and a promo pulls some back)
    public float SpawnMultiplier => (Open ? 1f - 0.4f * pull : 1f) * (PromoToday ? 1.2f : 1f);

    void Awake()
    {
        Instance = this;
        NextOpenDay = firstDay;
        Apply();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        clock = FindFirstObjectByType<TimeCyc>();
        if (clock != null) clock.OnNewDay += NewDay;
        ShopProgress.OnServed += Served;
        ShopProgress.OnCustomerAngry += Angry;
        ShopProgress.OnDaySummary += DayDone;
        RegularsSystem.OnFriendshipChanged += Friendship;
        DeliverySystem.OnTicketFinished += Delivered;
    }

    void OnDisable()
    {
        if (clock != null) clock.OnNewDay -= NewDay;
        ShopProgress.OnServed -= Served;
        ShopProgress.OnCustomerAngry -= Angry;
        ShopProgress.OnDaySummary -= DayDone;
        RegularsSystem.OnFriendshipChanged -= Friendship;
        DeliverySystem.OnTicketFinished -= Delivered;
    }

    bool Active => !ChallengeMode.Active;

    void NewDay(int day)
    {
        PromoToday = false;
        lostToday = 0;
        lostCarry = 0f;
        if (!Active || BranchOpen) return;
        if (!Open && day >= NextOpenDay) OpenUp();
    }

    public void OpenUp()
    {
        if (Open || BranchOpen) return;
        Open = true;
        pull = startPull;
        lastToastStep = Step(pull);
        Apply();
        GameAudio.Play(Sfx.Firecracker);
        ShopHUD.Toast(Loc.T("ร้านชาไข่มุกเจ้าใหม่เปิดฝั่งตรงข้าม! ลูกค้าบางส่วนเริ่มไปลองร้านนั้น · ทำแก้วให้เป๊ะเพื่อดึงลูกค้ากลับ (ซื้อโปรโมชั่นได้ที่คอมพิวเตอร์)",
                            "A new boba shop opened across the street! Some customers are trying it · perfect drinks win them back (promos at the shop computer)"));
        OnOpenChanged?.Invoke(true);
    }

    // ---------------------------------------------------------------- the tug of war
    void Nudge(float delta)
    {
        if (!Open || !Active) return;
        pull = Mathf.Clamp01(pull + delta);
        int step = Step(pull);
        if (step != lastToastStep)
        {
            bool better = step < lastToastStep;
            lastToastStep = step;
            if (pull > 0f)
                ShopHUD.Toast(better ? Loc.T($"ลูกค้ากลับมาร้านเรา! ร้านคู่แข่งเหลือแรงดึง {pull * 100f:0}%", $"Customers are coming back! The rival's pull is down to {pull * 100f:0}%")
                                     : Loc.T($"ร้านคู่แข่งแย่งลูกค้าไปมากขึ้น ({pull * 100f:0}%)", $"The rival is pulling more customers ({pull * 100f:0}%)"));
        }
        if (pull <= 0f) Beaten();
    }

    static int Step(float p) => Mathf.FloorToInt(p * 4f + 0.001f);

    void Served(CustomerController c, ServeResult r)
    {
        float k = PromoToday ? 2f : 1f;
        Nudge(r.quality == ServeQuality.Perfect ? -0.02f * k : -0.004f * k);
    }

    void Angry(CustomerController c) { Nudge(0.04f); }
    void Friendship(RegularsSystem.Regular r) { Nudge(-0.03f); }
    void Delivered(DeliverySystem.Ticket t, bool onTime) { Nudge(onTime ? -0.025f : 0.02f); }

    void DayDone(ShopProgress.DayStats s)
    {
        if (!Open || !Active) return;
        // a strong reputation keeps eating into it overnight; a bad day helps them
        if (ShopProgress.Instance != null && ShopProgress.Instance.reputation >= 4.2f) Nudge(-0.05f);
        if (s.angry >= 4) Nudge(0.05f);
    }

    void Beaten()
    {
        Open = false;
        pull = 0f;
        NextOpenDay = (clock != null ? clock.Days : 1) + returnAfter;
        Apply();
        StatsSystem.Add(StatsSystem.Stat.RivalWin);
        GameAudio.Play(Sfx.Chapter);
        if (ShopProgress.Instance != null)
        {
            ShopProgress.Instance.reputation = Mathf.Clamp(ShopProgress.Instance.reputation + 0.3f, 0f, 5f);
            ShopProgress.Instance.AddBonus(500, Loc.T("ร้านคู่แข่งปิดตัว! ลูกค้าทั้งซอยกลับมาหาเรา", "The rival closed down! The whole soi is back"));
        }
        OnOpenChanged?.Invoke(false);
    }

    // the shop computer's promo button: flyers + a "2nd cup" sign for today
    public bool BuyPromo()
    {
        if (!Open || PromoToday) return false;
        GameManager gm = GameManager.Instance;
        if (gm != null && !gm.TrySpend(promoCost))
        {
            GameAudio.Play(Sfx.Error);
            ShopHUD.ToastNow(Loc.T($"เงินไม่พอทำโปรโมชั่น ({promoCost} ฿)", $"Not enough money for a promo ({promoCost} ฿)"));
            return false;
        }
        if (ShopProgress.Instance != null) ShopProgress.Instance.RegisterExpense(promoCost);
        PromoToday = true;
        GameAudio.Play(Sfx.Confirm);
        ShopHUD.ToastNow(Loc.T("แจกใบปลิว + ติดป้ายโปรแล้ว! วันนี้ลูกค้าเพิ่มขึ้น และทุกแก้วเป๊ะดึงลูกค้ากลับได้ 2 เท่า",
                               "Flyers out and the promo sign up! More customers today, and every perfect drink wins twice as many back"), 2.6f);
        return true;
    }

    // Spawner: a customer who would have come went across the street instead (counted for the summary)
    public void CountLost(float expected)
    {
        if (!Open) return;
        lostCarry += expected;
        while (lostCarry >= 1f) { lostCarry -= 1f; lostToday++; }
    }

    public string SummaryLine()
    {
        if (!Open) return "";
        return Loc.T($"\n<size=90%><color=#7A3E9D>ร้านคู่แข่งฝั่งตรงข้าม: ดึงลูกค้า {pull * 100f:0}% · วันนี้เสียลูกค้าไปราว {lostToday} คน</color></size>",
                     $"\n<size=90%><color=#7A3E9D>Rival across the street: {pull * 100f:0}% pull · about {lostToday} customers lost today</color></size>");
    }

    // ---------------------------------------------------------------- story: the second branch takes the building
    public void OpenBranch()
    {
        Open = false;
        BranchOpen = true;
        pull = 0f;
        Apply();
    }

    void Update()
    {
        if (rivalQueue == null) return;
        int show = Open ? Mathf.RoundToInt(pull * rivalQueue.Length + 0.3f) : 0;
        for (int i = 0; i < rivalQueue.Length; i++)
            if (rivalQueue[i] != null && rivalQueue[i].activeSelf != (i < show)) rivalQueue[i].SetActive(i < show);
    }

    void Apply()
    {
        if (closedFront != null) closedFront.SetActive(!Open && !BranchOpen);
        if (rivalFront != null) rivalFront.SetActive(Open);
        if (branchFront != null) branchFront.SetActive(BranchOpen);
    }

    // ---------------------------------------------------------------- save / load
    [Serializable]
    public class State
    {
        public bool open, branch;
        public float pull;
        public int nextOpenDay = 8;
    }

    public State Capture() => new State { open = Open, branch = BranchOpen, pull = pull, nextOpenDay = NextOpenDay };

    public void Load(State s)
    {
        s = s ?? new State { nextOpenDay = firstDay };
        Open = s.open && !s.branch;
        BranchOpen = s.branch;
        pull = s.pull;
        NextOpenDay = Mathf.Max(1, s.nextOpenDay);
        PromoToday = false;
        lastToastStep = Step(pull);
        Apply();
    }
}
