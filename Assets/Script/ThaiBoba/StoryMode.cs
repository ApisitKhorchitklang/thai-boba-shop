using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The story of the shop (เนื้อเรื่อง): from a pink cart at the mouth of the soi, to this rented shophouse, to a
// second branch across the street. Five chapters, each with a few goals (about a week of play each); finishing a
// chapter tells the next bit of the story and pays a reward. The last one: save up and open branch 2 (the building
// opposite, where the rival used to be). After the ending the branch sends a little profit every evening.
// Not active in the daily challenge.
public class StoryMode : MonoBehaviour
{
    public static StoryMode Instance { get; private set; }

    [Header("UI (story popup)")]
    public GameObject panel;
    public TMP_Text chapterText, titleText, bodyText, goalsText;
    public Button continueButton;
    public TMP_Text hudChip;                    // "บทที่ 2 · 1/3" on the HUD (a button that opens the panel)

    [Header("World")]
    public GameObject firstCart;                // the old cart parked at the mouth of the soi

    public int branchCost = 12000;
    public int branchIncome = 250;

    [Serializable]
    public class State
    {
        public int chapter;                         // 0..4, 5 = finished
        public int served, perfect, profit, deliveries, bestDayPerfect;
        public bool rivalBeaten, seenIntro;
    }

    public State S { get; private set; } = new State();

    class Goal
    {
        public string th, en;
        public Func<State, float> have;
        public float need;
        public bool money;
        public string Text(State s)
        {
            float h = Mathf.Min(have(s), need);
            bool done = h >= need;
            string count = need > 1f ? (money ? $" ({h:N0}/{need:N0} ฿)" : $" ({h:0.#}/{need:0.#})") : "";
            return done ? "<color=#4E8A3C>• " + Loc.T(th, en) + count + "  <b>" + Loc.T("สำเร็จ", "done") + "</b></color>" : "• " + Loc.T(th, en) + count;
        }
    }

    class Chapter
    {
        public string titleTH, titleEN, introTH, introEN, outroTH, outroEN;
        public Goal[] goals;
        public int reward;
    }

    static int Level => ShopProgress.Instance != null ? ShopProgress.Instance.level : 1;
    static float Rep => ShopProgress.Instance != null ? ShopProgress.Instance.reputation : 0f;
    static int Money => GameManager.Instance != null ? GameManager.Instance.money : 0;
    static int BestHearts { get { int b = 0; if (RegularsSystem.Instance != null) foreach (var r in RegularsSystem.Instance.regulars) b = Mathf.Max(b, r.hearts); return b; } }
    static int TodayProfit => ShopProgress.Instance != null ? ShopProgress.Instance.today.Profit : 0;
    static int TodayPerfect => ShopProgress.Instance != null ? ShopProgress.Instance.today.perfect : 0;
    static bool BranchBought => RivalShop.Instance != null && RivalShop.Instance.BranchOpen;

    static readonly Chapter[] Chapters =
    {
        new Chapter
        {
            titleTH = "บทที่ 1 · จากรถเข็นสู่ห้องแถว", titleEN = "Chapter 1 · From a cart to a shophouse",
            introTH = "สามปีที่ขายชานมจากรถเข็นสีชมพูตรงปากซอย ฝนตกก็เปียก แดดออกก็ร้อน… วันนี้ในที่สุดก็เช่าห้องแถวได้เป็นของตัวเอง! รถเข็นคันแรกยังจอดอยู่ปากซอยเป็นที่ระลึก\n\nเจ้าของตึกให้เวลาพิสูจน์ตัวเองหนึ่งสัปดาห์ ว่าร้านนี้ไปรอดจริงๆ",
            introEN = "Three years of selling milk tea from a pink cart at the mouth of the soi, soaked in the rain, baked in the sun… and today you finally rent a shophouse of your own! The first cart is still parked at the corner, for luck.\n\nThe landlord gives you a week to prove the shop can make it.",
            outroTH = "เจ้าของตึกแวะมาชิมชาไทยแก้วหนึ่ง ยิ้มแล้วบอกว่า \"ต่อสัญญาให้อีกปีนะ\" ร้านเล็กๆ ในซอยเริ่มมีคนรู้จักแล้ว",
            outroEN = "The landlord drops by for a Thai tea, smiles and says \"I'll renew the lease for another year.\" The little shop is starting to get noticed.",
            reward = 300,
            goals = new[]
            {
                new Goal { th = "เสิร์ฟให้ครบ 40 แก้ว", en = "Serve 40 drinks", have = s => s.served, need = 40 },
                new Goal { th = "กำไรรวม 2,000 ฿", en = "Make 2,000 ฿ profit", have = s => s.profit + Mathf.Max(0, TodayProfit), need = 2000, money = true },
                new Goal { th = "ชื่อเสียง 3.5 ดาว", en = "Reach a 3.5 star rating", have = s => Rep, need = 3.5f },
            }
        },
        new Chapter
        {
            titleTH = "บทที่ 2 · ขาประจำของซอย", titleEN = "Chapter 2 · Regulars of the soi",
            introTH = "ร้านชานมจะอยู่ได้ก็เพราะขาประจำ น้องนักเรียน ป้าข้างบ้าน พี่ไรเดอร์… จำออเดอร์ \"แก้วเดิม\" ของทุกคนให้ได้ แล้วแต่งร้านให้น่านั่งขึ้นอีกหน่อย",
            introEN = "A boba shop lives on its regulars: the students, the auntie next door, the riders… remember everyone's \"usual\" and make the shop a bit nicer to hang out at.",
            outroTH = "ป้าศรีเล่าให้ทั้งซอยฟังว่า \"ร้านนี้จำได้ด้วยนะว่าป้าชอบหวานน้อย\" ลูกค้าใหม่เริ่มตามมาเพราะคำบอกต่อ",
            outroEN = "Auntie Sri tells the whole soi \"they even remember I like it less sweet!\" New customers start coming by word of mouth.",
            reward = 500,
            goals = new[]
            {
                new Goal { th = "ลูกค้าประจำสนิทถึง 3/5", en = "A regular at friendship 3/5", have = s => BestHearts, need = 3 },
                new Goal { th = "ทำแก้วเป๊ะ 15 แก้วในวันเดียว", en = "15 perfect drinks in one day", have = s => Mathf.Max(s.bestDayPerfect, TodayPerfect), need = 15 },
                new Goal { th = "ตกแต่งร้าน (ความสวย 2)", en = "Decorate the shop (beauty 2)", have = s => DecorShop.Instance != null ? DecorShop.Instance.Beauty : 0, need = 2 },
            }
        },
        new Chapter
        {
            titleTH = "บทที่ 3 · ศึกชานมฝั่งตรงข้าม", titleEN = "Chapter 3 · The boba war across the street",
            introTH = "ห้องแถวว่างฝั่งตรงข้ามติดป้ายผ้าใหญ่ \"ชาไข่มุกเจ้าใหม่ เปิดแล้ว!\" พวงหรีดดอกไม้เรียงหน้าร้าน ลูกค้าเริ่มเดินข้ามถนนไปลอง… ถึงเวลาพิสูจน์ว่าร้านไหนคือเจ้าถิ่น",
            introEN = "The empty shophouse across the street now has a huge banner: \"New boba shop, now open!\" Flower stands line the front and customers start crossing over to try it… time to show who rules this soi.",
            outroTH = "ร้านฝั่งตรงข้ามปิดเงียบ เหลือแค่ป้าย \"ให้เช่า\" ลูกค้าบอกว่า \"สุดท้ายก็ต้องร้านนี้แหละ\"",
            outroEN = "The shop across the street goes quiet, just a \"for rent\" sign left. Your customers say \"in the end, it has to be this place.\"",
            reward = 800,
            goals = new[]
            {
                new Goal { th = "เอาชนะร้านคู่แข่ง (แรงดึงเหลือ 0%)", en = "Beat the rival (pull down to 0%)", have = s => s.rivalBeaten ? 1 : 0, need = 1 },
                new Goal { th = "ร้านเลเวล 4", en = "Shop level 4", have = s => Level, need = 4 },
            }
        },
        new Chapter
        {
            titleTH = "บทที่ 4 · ทีมของเรา", titleEN = "Chapter 4 · Our team",
            introTH = "ทำคนเดียวเริ่มไม่ไหวแล้ว ลูกค้าต่อคิวยาว แอปเดลิเวอรี่ก็เด้งไม่หยุด ถึงเวลาหาคนมาช่วย และเปิดรับออเดอร์ทางแอปให้เต็มตัว",
            introEN = "You can't do it alone any more: the line is long and the delivery app keeps pinging. Time to hire some help and take app orders for real.",
            outroTH = "ร้านเดินเครื่องเต็มที่ มีพนักงานช่วย มีไรเดอร์แวะรับออเดอร์ทั้งวัน ความฝันต่อไป… สาขาที่สอง!",
            outroEN = "The shop is running at full speed, with a barista and riders picking up all day. The next dream… a second branch!",
            reward = 1000,
            goals = new[]
            {
                new Goal { th = "จ้างพนักงานชงชา (คอมพิวเตอร์ > พนักงาน)", en = "Hire a barista (computer > staff)", have = s => DecorShop.Instance != null && DecorShop.Instance.Has("barista") ? 1 : 0, need = 1 },
                new Goal { th = "ส่งเดลิเวอรี่ทันเวลา 5 ออเดอร์", en = "Deliver 5 app orders on time", have = s => s.deliveries, need = 5 },
                new Goal { th = "ชื่อเสียง 4.3 ดาว", en = "Reach a 4.3 star rating", have = s => Rep, need = 4.3f },
            }
        },
        new Chapter
        {
            titleTH = "บทที่ 5 · สาขาที่สอง", titleEN = "Chapter 5 · The second branch",
            introTH = "ห้องแถวฝั่งตรงข้ามยังว่างอยู่… ถ้าเก็บเงินได้พอ ที่นั่นจะเป็น \"บ้านชานมไข่มุก สาขา 2\" เปิดสาขาได้ที่คอมพิวเตอร์ในร้าน",
            introEN = "The shophouse across the street is still empty… save enough and it becomes \"Baan Boba, branch 2\". Open it from the shop computer.",
            outroTH = "ป้ายใหม่ \"บ้านชานมไข่มุก สาขา 2\" สว่างอยู่ฝั่งตรงข้าม จากรถเข็นคันเล็กปากซอย วันนี้มีสองร้านในซอยเดียวกันแล้ว ขอบคุณที่เล่นจนจบนะ!\n\nสาขา 2 จะส่งกำไรให้ทุกเย็น เล่นต่อได้ตามใจเลย",
            outroEN = "A new sign glows across the street: \"Baan Boba, branch 2\". From a little cart at the corner to two shops in the same soi. Thank you for playing to the end!\n\nBranch 2 sends you a share of its profit every evening. Keep playing as long as you like.",
            reward = 0,
            goals = new[]
            {
                new Goal { th = "เก็บเงินให้ถึง 15,000 ฿", en = "Save up 15,000 ฿", have = s => BranchBought ? 15000 : Money, need = 15000, money = true },   // spent on the branch: still counts
                new Goal { th = "เปิดสาขา 2 ที่คอมพิวเตอร์ (12,000 ฿)", en = "Open branch 2 at the computer (12,000 ฿)", have = s => RivalShop.Instance != null && RivalShop.Instance.BranchOpen ? 1 : 0, need = 1 },
            }
        },
    };

    public bool Active => !ChallengeMode.Active;
    public bool Finished => S.chapter >= Chapters.Length;
    public int ChapterIndex => S.chapter;
    public static int ChapterCount => Chapters.Length;
    public bool CanOpenBranch => S.chapter == 4 && Money >= 15000 && RivalShop.Instance != null && !RivalShop.Instance.BranchOpen;
    Chapter Cur => Finished ? null : Chapters[S.chapter];

    float checkTimer;
    bool pendingOutro;
    int outroChapter = -1;

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
        CustomerController.FrozenByStory = false;                      // statics survive a scene reload
        if (continueButton != null) continueButton.onClick.AddListener(ClosePanel);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnEnable()
    {
        ShopProgress.OnServed += Served;
        ShopProgress.OnDaySummary += DayDone;
        RivalShop.OnOpenChanged += RivalChanged;
        DeliverySystem.OnTicketFinished += Delivered;
    }

    void OnDisable()
    {
        ShopProgress.OnServed -= Served;
        ShopProgress.OnDaySummary -= DayDone;
        RivalShop.OnOpenChanged -= RivalChanged;
        DeliverySystem.OnTicketFinished -= Delivered;
    }

    public static bool PanelOpen => Instance != null && Instance.panel != null && Instance.panel.activeSelf;
    // the intro / chapter popups open by themselves (automated tests switch this off)
    public static bool AutoPopups = true;

    void Served(CustomerController c, ServeResult r)
    {
        if (!Active || Finished) return;
        S.served++;
        if (r.quality == ServeQuality.Perfect) S.perfect++;
    }

    void Delivered(DeliverySystem.Ticket t, bool onTime) { if (Active && onTime) S.deliveries++; }
    void RivalChanged(bool opened) { if (Active && !opened && S.chapter == 2) S.rivalBeaten = true; }

    void DayDone(ShopProgress.DayStats s)
    {
        if (!Active) return;
        if (Finished && RivalShop.Instance != null && RivalShop.Instance.BranchOpen && ShopProgress.Instance != null)
        {
            ShopProgress.Instance.AddBonus(branchIncome, Loc.T("กำไรจากสาขา 2", "Profit from branch 2"));
            return;
        }
        if (Finished) return;
        S.profit += s.Profit;
        S.bestDayPerfect = Mathf.Max(S.bestDayPerfect, s.perfect);
    }

    void Update()
    {
        if (firstCart != null && firstCart.activeSelf != Active) firstCart.SetActive(Active);
        UpdateChip();
        if (!Active || !GameMenus.GameRunning) return;
        bool busy = GameMenus.IsOpen || PanelOpen || (TutorialSystem.Instance != null && TutorialSystem.Instance.Running);
        if (busy) return;
        if (AutoPopups && !Finished && !S.seenIntro) { S.seenIntro = true; ShowPanel(); return; }
        if (AutoPopups && pendingOutro) { pendingOutro = false; ShowPanel(); return; }
        checkTimer -= Time.unscaledDeltaTime;
        if (checkTimer > 0f || Finished) return;
        checkTimer = 1f;
        if (S.chapter == 2 && RivalShop.Instance != null && !RivalShop.Instance.Open && !S.rivalBeaten && !RivalShop.Instance.BranchOpen)
            RivalShop.Instance.OpenUp();                                   // chapter 3 brings the rival
        bool all = true;
        foreach (Goal g in Cur.goals) if (g.have(S) < g.need) { all = false; break; }
        if (all) CompleteChapter();
    }

    void CompleteChapter()
    {
        Chapter done = Cur;
        outroChapter = S.chapter;
        S.chapter++;
        S.served = S.perfect = S.profit = S.deliveries = S.bestDayPerfect = 0;
        S.rivalBeaten = false;
        S.seenIntro = true;                                                // the outro panel also shows the next intro
        GameAudio.Play(Sfx.Chapter);
        if (done.reward > 0 && ShopProgress.Instance != null) ShopProgress.Instance.AddBonus(done.reward, Loc.T("รางวัลจบบท", "Chapter reward"));
        if (Finished) StatsSystem.Add(StatsSystem.Stat.StoryDone);
        if (AutoPopups) ShowPanel();
    }

    // computer button (chapter 5): buy the building across the street
    public bool OpenBranch()
    {
        if (!CanOpenBranch) return false;
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.TrySpend(branchCost)) { GameAudio.Play(Sfx.Error); return false; }
        if (ShopProgress.Instance != null) ShopProgress.Instance.RegisterExpense(branchCost);
        RivalShop.Instance.OpenBranch();
        ShopHUD.ToastNow(Loc.T("เปิด \"บ้านชานมไข่มุก สาขา 2\" แล้ว! ดูฝั่งตรงข้ามสิ", "\"Baan Boba, branch 2\" is open! Look across the street"), 3f);
        checkTimer = 0f;
        return true;
    }

    // ---------------------------------------------------------------- popup
    public void ShowPanel()
    {
        if (panel == null) return;
        CustomerController.FrozenByStory = true;                       // nobody walks out while you read
        panel.SetActive(true);
        GameAudio.Play(Sfx.Open);
        Refresh();
    }

    public void ClosePanel()
    {
        if (panel == null) return;
        CustomerController.FrozenByStory = false;
        panel.SetActive(false);
        GameAudio.Play(Sfx.Close);
        outroChapter = -1;
    }

    void Refresh()
    {
        string story = "";
        if (outroChapter >= 0 && outroChapter < Chapters.Length)
            story = $"<b>{Loc.T("จบ", "End of")} {Loc.T(Chapters[outroChapter].titleTH, Chapters[outroChapter].titleEN)}</b>\n{Loc.T(Chapters[outroChapter].outroTH, Chapters[outroChapter].outroEN)}" +
                    (Chapters[outroChapter].reward > 0 ? Loc.T($"\n<color=#4E8A3C>รางวัล +{Chapters[outroChapter].reward} ฿</color>", $"\n<color=#4E8A3C>Reward +{Chapters[outroChapter].reward} ฿</color>") : "") + "\n\n";
        if (Finished)
        {
            if (chapterText != null) chapterText.text = Loc.T("เนื้อเรื่อง", "Story");
            if (titleText != null) titleText.text = Loc.T("จบเนื้อเรื่อง · สาขาที่สอง", "The end · a second branch");
            if (bodyText != null) bodyText.text = story.Length > 0 ? story.TrimEnd() : Loc.T(Chapters[Chapters.Length - 1].outroTH, Chapters[Chapters.Length - 1].outroEN);
            if (goalsText != null) goalsText.text = Loc.T($"สาขา 2 ส่งกำไรให้ +{branchIncome} ฿ ทุกเย็น", $"Branch 2 sends +{branchIncome} ฿ every evening");
            return;
        }
        Chapter c = Cur;
        if (chapterText != null) chapterText.text = Loc.T($"เนื้อเรื่อง {S.chapter + 1}/{Chapters.Length}", $"Story {S.chapter + 1}/{Chapters.Length}");
        if (titleText != null) titleText.text = Loc.T(c.titleTH, c.titleEN);
        if (bodyText != null) bodyText.text = story + Loc.T(c.introTH, c.introEN);
        if (goalsText != null)
        {
            var sb = new System.Text.StringBuilder(Loc.T("<b>เป้าหมาย</b>\n", "<b>Goals</b>\n"));
            foreach (Goal g in c.goals) sb.AppendLine(g.Text(S));
            goalsText.text = sb.ToString().TrimEnd();
        }
    }

    void UpdateChip()
    {
        if (hudChip == null) return;
        GameObject chip = hudChip.transform.parent != null ? hudChip.transform.parent.gameObject : hudChip.gameObject;
        if (chip.activeSelf != Active) chip.SetActive(Active);
        if (!Active) return;
        string t;
        if (Finished) t = Loc.T("เนื้อเรื่องจบแล้ว · สาขา 2", "Story complete · branch 2");
        else
        {
            int done = 0;
            foreach (Goal g in Cur.goals) if (g.have(S) >= g.need) done++;
            t = Loc.T($"บทที่ {S.chapter + 1} · เป้าหมาย {done}/{Cur.goals.Length}", $"Chapter {S.chapter + 1} · goals {done}/{Cur.goals.Length}");
        }
        if (hudChip.text != t) hudChip.text = t;
        if (PanelOpen) Refresh();
    }

    // ---------------------------------------------------------------- save / load
    public void Load(State s)
    {
        S = s ?? new State();
        outroChapter = -1;
        pendingOutro = false;
        if (RivalShop.Instance != null && S.chapter >= Chapters.Length && !RivalShop.Instance.BranchOpen) RivalShop.Instance.OpenBranch();
    }
}
