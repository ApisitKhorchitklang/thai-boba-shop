using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Lifetime stats and medals (เหรียญรางวัล). They live in their own profile file, so they survive a new game and
// loading an older day; the daily challenge doesn't count. Shown on the "Stats & medals" page (pause / main menu).
public class StatsSystem : MonoBehaviour
{
    public static StatsSystem Instance { get; private set; }

    [Serializable]
    public class Lifetime
    {
        public int served, perfect, angry, rejected, days, revenue, tips, bestDayProfit, bestCombo, maxMoney, bestRep10;
        public int toppings, deliveries, deliveriesLate, catsPetted, rivalWins, staffDrinks, decorMoves, angPao, cleanDays;
        public int bestFriend, storyDone;
        public int[] byDrink = new int[16];
        public string[] festivals = new string[0];
        public string[] medals = new string[0];
    }

    public enum Stat { Delivery, DeliveryLate, CatPet, RivalWin, StaffDrink, DecorMove, AngPao, StoryDone }

    public class Medal
    {
        public string id, nameTH, nameEN, descTH, descEN, icon;
        public Func<Lifetime, float> progress;                  // 0..1, 1 = earned
        public string Name => Loc.T(nameTH, nameEN);
        public string Desc => Loc.T(descTH, descEN);
    }

    static float P(float have, float need) => Mathf.Clamp01(have / need);

    public static readonly Medal[] Medals =
    {
        new Medal { id = "first_cup", icon = "icon_cup", nameTH = "แก้วแรกของร้าน", nameEN = "First cup", descTH = "เสิร์ฟแก้วแรก", descEN = "Serve your first drink", progress = l => P(l.served, 1) },
        new Medal { id = "cups_100", icon = "icon_cup", nameTH = "ร้อยแก้ว", nameEN = "A hundred cups", descTH = "เสิร์ฟครบ 100 แก้ว", descEN = "Serve 100 drinks", progress = l => P(l.served, 100) },
        new Medal { id = "cups_500", icon = "icon_shop", nameTH = "ขายดีทั้งซอย", nameEN = "Talk of the soi", descTH = "เสิร์ฟครบ 500 แก้ว", descEN = "Serve 500 drinks", progress = l => P(l.served, 500) },
        new Medal { id = "perfect_50", icon = "icon_star", nameTH = "มือโปร", nameEN = "Pro hands", descTH = "ทำแก้วเป๊ะ 50 แก้ว", descEN = "50 perfect drinks", progress = l => P(l.perfect, 50) },
        new Medal { id = "combo_10", icon = "icon_fire", nameTH = "คอมโบไฟลุก", nameEN = "On fire", descTH = "คอมโบ x10", descEN = "Reach a x10 combo", progress = l => P(l.bestCombo, 10) },
        new Medal { id = "profit_1500", icon = "icon_coin", nameTH = "วันทองคำ", nameEN = "Golden day", descTH = "กำไร 1,500 ฿ ในวันเดียว", descEN = "1,500 ฿ profit in one day", progress = l => P(l.bestDayProfit, 1500) },
        new Medal { id = "rich_20000", icon = "icon_coin", nameTH = "เศรษฐีชานม", nameEN = "Boba tycoon", descTH = "มีเงินเก็บ 20,000 ฿", descEN = "Save up 20,000 ฿", progress = l => P(l.maxMoney, 20000) },
        new Medal { id = "stars_5", icon = "icon_star", nameTH = "ร้านห้าดาว", nameEN = "Five-star shop", descTH = "ชื่อเสียงถึง 4.8 ดาว", descEN = "Reach a 4.8 star rating", progress = l => P(l.bestRep10, 48) },
        new Medal { id = "best_friend", icon = "emote_love", nameTH = "เพื่อนซี้", nameEN = "Best friends", descTH = "ลูกค้าประจำสนิท 5/5", descEN = "A regular at friendship 5/5", progress = l => P(l.bestFriend, 5) },
        new Medal { id = "full_menu", icon = "icon_tea", nameTH = "ครบทุกเมนู", nameEN = "Full menu", descTH = "ขายครบทั้ง 7 เมนูหลัก", descEN = "Sell each of the 7 main drinks", progress = l => FullMenu(l) },
        new Medal { id = "toppings_50", icon = "icon_boba", nameTH = "สายท็อปปิ้ง", nameEN = "Topping lover", descTH = "ขายท็อปปิ้ง 50 ถ้วย", descEN = "Sell 50 toppings", progress = l => P(l.toppings, 50) },
        new Medal { id = "delivery_20", icon = "icon_clock", nameTH = "ไรเดอร์รักร้านนี้", nameEN = "Riders' favourite", descTH = "ส่งเดลิเวอรี่ทันเวลา 20 ออเดอร์", descEN = "20 delivery orders on time", progress = l => P(l.deliveries, 20) },
        new Medal { id = "rival_win", icon = "icon_trophy", nameTH = "เจ้าถิ่นตัวจริง", nameEN = "King of the soi", descTH = "เอาชนะร้านคู่แข่งฝั่งตรงข้าม", descEN = "Beat the rival shop across the street", progress = l => P(l.rivalWins, 1) },
        new Medal { id = "festivals_3", icon = "icon_calendar", nameTH = "สายเทศกาล", nameEN = "Festival goer", descTH = "เปิดร้านช่วงเทศกาล 3 แบบ", descEN = "Open during 3 different festivals", progress = l => P(l.festivals.Length, 3) },
        new Medal { id = "cat_friend", icon = "emote_happy", nameTH = "ทาสแมว", nameEN = "Cat servant", descTH = "ลูบแมวจร 10 ครั้ง", descEN = "Pet the stray cats 10 times", progress = l => P(l.catsPetted, 10) },
        new Medal { id = "clean_day", icon = "icon_sun", nameTH = "ไม่มีใครเดินหนี", nameEN = "Nobody walked out", descTH = "เสิร์ฟ 15 แก้วขึ้นไปโดยไม่มีใครเดินหนี", descEN = "Serve 15+ in a day with nobody walking out", progress = l => P(l.cleanDays, 1) },
        new Medal { id = "teamwork", icon = "icon_levelup", nameTH = "ทีมเวิร์ก", nameEN = "Teamwork", descTH = "พนักงานชงให้ลูกค้า 30 แก้ว", descEN = "Your barista serves 30 drinks", progress = l => P(l.staffDrinks, 30) },
        new Medal { id = "decorator", icon = "icon_home", nameTH = "นักจัดร้าน", nameEN = "Decorator", descTH = "ย้ายของตกแต่ง 5 ครั้ง", descEN = "Move decorations 5 times", progress = l => P(l.decorMoves, 5) },
        new Medal { id = "week_1", icon = "icon_calendar", nameTH = "ครบสัปดาห์", nameEN = "One week in", descTH = "เปิดร้านครบ 7 วัน", descEN = "Run the shop for 7 days", progress = l => P(l.days, 7) },
        new Medal { id = "branch_2", icon = "logo_cup", nameTH = "สาขาที่สอง", nameEN = "Second branch", descTH = "เปิดสาขา 2 (จบเนื้อเรื่อง)", descEN = "Open branch 2 (finish the story)", progress = l => P(l.storyDone, 1) },
    };

    static float FullMenu(Lifetime l)
    {
        int n = 0;
        for (int i = 0; i < 7 && i < l.byDrink.Length; i++) if (l.byDrink[i] > 0) n++;
        return n / 7f;
    }

    public static Lifetime Life { get; private set; } = new Lifetime();
    public static event Action<Medal> OnMedal;
    static bool loaded;
    static string FilePath => Path.Combine(Application.persistentDataPath, "profile_stats.json");
    float pollTimer;

    public static bool Counting => !ChallengeMode.Active;

    void Awake()
    {
        Instance = this;
        EnsureLoaded();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnApplicationQuit() { Save(); }

    void OnEnable()
    {
        ShopProgress.OnServed += Served;
        ShopProgress.OnCustomerAngry += Angry;
        ShopProgress.OnDaySummary += DayDone;
        RegularsSystem.OnFriendshipChanged += Friendship;
    }

    void OnDisable()
    {
        ShopProgress.OnServed -= Served;
        ShopProgress.OnCustomerAngry -= Angry;
        ShopProgress.OnDaySummary -= DayDone;
        RegularsSystem.OnFriendshipChanged -= Friendship;
    }

    public static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            if (File.Exists(FilePath)) Life = JsonUtility.FromJson<Lifetime>(File.ReadAllText(FilePath)) ?? new Lifetime();
        }
        catch (Exception e) { Debug.LogWarning("Stats profile unreadable, starting fresh: " + e.Message); Life = new Lifetime(); }
        if (Life.byDrink == null || Life.byDrink.Length < 16) Array.Resize(ref Life.byDrink, 16);
        if (Life.festivals == null) Life.festivals = new string[0];
        if (Life.medals == null) Life.medals = new string[0];
    }

    public static void Save()
    {
        if (testing) return;                                   // automated tests never touch the player's profile
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(Life, true)); }
        catch (Exception e) { Debug.LogWarning("Could not save stats: " + e.Message); }
    }

    // tests: start from a blank profile without touching the file; EndTests puts the real profile back in memory
    static bool testing;
    public static void ResetForTests() { Life = new Lifetime(); loaded = true; testing = true; }
    public static void EndTests() { testing = false; loaded = false; EnsureLoaded(); }

    // ---------------------------------------------------------------- counting
    void Served(CustomerController c, ServeResult r)
    {
        if (!Counting) return;
        Life.served++;
        if (r.quality == ServeQuality.Perfect) Life.perfect++;
        if (c != null && c.order != null)
        {
            int di = Array.FindIndex(DrinkMenu.All, d => d.id == c.order.flavor);
            if (di >= 0 && di < Life.byDrink.Length) Life.byDrink[di]++;
            if (!string.IsNullOrEmpty(c.order.topping)) Life.toppings++;
        }
        if (ShopProgress.Instance != null) Life.bestCombo = Mathf.Max(Life.bestCombo, ShopProgress.Instance.combo);
        Check();
    }

    void Angry(CustomerController c) { if (Counting) Life.angry++; }

    void Friendship(RegularsSystem.Regular r)
    {
        if (!Counting || r == null) return;
        Life.bestFriend = Mathf.Max(Life.bestFriend, r.hearts);
        Check();
    }

    void DayDone(ShopProgress.DayStats s)
    {
        if (!Counting) return;
        Life.days++;
        Life.revenue += s.revenue;
        Life.tips += s.tips;
        Life.rejected += s.rejected;
        Life.bestDayProfit = Mathf.Max(Life.bestDayProfit, s.Profit);
        if (s.angry == 0 && s.served >= 15) Life.cleanDays++;
        DailyEventSystem ev = DailyEventSystem.Instance;
        if (ev != null && DailyEventSystem.IsFestival(ev.Current.type))
        {
            string id = ev.Current.type.ToString();
            if (Array.IndexOf(Life.festivals, id) < 0)
            {
                var l = new List<string>(Life.festivals) { id };
                Life.festivals = l.ToArray();
            }
        }
        Check();
        Save();
    }

    public static void Add(Stat s, int n = 1)
    {
        if (!Counting) return;
        switch (s)
        {
            case Stat.Delivery: Life.deliveries += n; break;
            case Stat.DeliveryLate: Life.deliveriesLate += n; break;
            case Stat.CatPet: Life.catsPetted += n; break;
            case Stat.RivalWin: Life.rivalWins += n; break;
            case Stat.StaffDrink: Life.staffDrinks += n; break;
            case Stat.DecorMove: Life.decorMoves += n; break;
            case Stat.AngPao: Life.angPao += n; break;
            case Stat.StoryDone: Life.storyDone += n; break;
        }
        Check();
    }

    void Update()
    {
        if (!Counting || !GameMenus.GameRunning) return;
        pollTimer -= Time.unscaledDeltaTime;
        if (pollTimer > 0f) return;
        pollTimer = 2f;
        bool changed = false;
        if (GameManager.Instance != null && GameManager.Instance.money > Life.maxMoney) { Life.maxMoney = GameManager.Instance.money; changed = true; }
        if (ShopProgress.Instance != null)
        {
            int rep = Mathf.FloorToInt(ShopProgress.Instance.reputation * 10f + 0.001f);
            if (rep > Life.bestRep10) { Life.bestRep10 = rep; changed = true; }
        }
        if (changed) Check();
    }

    public static bool Has(string id) => Array.IndexOf(Life.medals, id) >= 0;
    public static int EarnedCount => Life.medals.Length;

    // award every medal whose goal is met (toast + jingle), once
    public static void Check()
    {
        foreach (Medal m in Medals)
        {
            if (Has(m.id) || m.progress(Life) < 1f) continue;
            var l = new List<string>(Life.medals) { m.id };
            Life.medals = l.ToArray();
            ShopHUD.Toast(Loc.T($"ได้เหรียญรางวัล: <b>{m.nameTH}</b> · {m.descTH}", $"Medal earned: <b>{m.nameEN}</b> · {m.descEN}"));
            GameAudio.Play(Sfx.Medal);
            OnMedal?.Invoke(m);
            Save();
        }
    }

    // ---------------------------------------------------------------- the stats page
    public static string Describe()
    {
        Lifetime l = Life;
        string best = null;
        int n = 0;
        for (int i = 0; i < DrinkMenu.All.Length && i < l.byDrink.Length; i++)
            if (l.byDrink[i] > n) { n = l.byDrink[i]; best = DrinkMenu.All[i].Name; }
        float perfectPct = l.served > 0 ? 100f * l.perfect / l.served : 0f;
        return Loc.EN
            ? $"<b>Days open</b>  {l.days}\n<b>Drinks served</b>  {l.served:N0}   <size=80%>(perfect {perfectPct:0}%)</size>\n" +
              $"<b>Best seller</b>  {(best ?? "-")}{(best != null ? $"  ({n:N0})" : "")}\n<b>Toppings sold</b>  {l.toppings:N0}\n" +
              $"<b>Sales</b>  {l.revenue:N0} ฿     <b>Tips</b>  {l.tips:N0} ฿\n<b>Best day</b>  {l.bestDayProfit:N0} ฿ profit\n" +
              $"<b>Best combo</b>  x{l.bestCombo}     <b>Top rating</b>  {l.bestRep10 / 10f:0.0} stars\n" +
              $"<b>Deliveries</b>  {l.deliveries} on time · {l.deliveriesLate} late\n<b>Walked out</b>  {l.angry}     <b>Refused</b>  {l.rejected}\n" +
              $"<b>Barista drinks</b>  {l.staffDrinks}     <b>Cats petted</b>  {l.catsPetted}\n<b>Festivals</b>  {l.festivals.Length}     <b>Rival beaten</b>  {l.rivalWins}"
            : $"<b>เปิดร้านมาแล้ว</b>  {l.days} วัน\n<b>เสิร์ฟไปแล้ว</b>  {l.served:N0} แก้ว   <size=80%>(เป๊ะ {perfectPct:0}%)</size>\n" +
              $"<b>เมนูขายดีตลอดกาล</b>  {(best ?? "-")}{(best != null ? $"  ({n:N0} แก้ว)" : "")}\n<b>ท็อปปิ้งที่ขาย</b>  {l.toppings:N0}\n" +
              $"<b>ยอดขาย</b>  {l.revenue:N0} ฿     <b>ทิป</b>  {l.tips:N0} ฿\n<b>วันที่ดีที่สุด</b>  กำไร {l.bestDayProfit:N0} ฿\n" +
              $"<b>คอมโบสูงสุด</b>  x{l.bestCombo}     <b>ดาวสูงสุด</b>  {l.bestRep10 / 10f:0.0} ดาว\n" +
              $"<b>เดลิเวอรี่</b>  ทัน {l.deliveries} · ไม่ทัน {l.deliveriesLate}\n<b>ลูกค้าเดินหนี</b>  {l.angry}     <b>แก้วโดนปฏิเสธ</b>  {l.rejected}\n" +
              $"<b>พนักงานชงให้</b>  {l.staffDrinks} แก้ว     <b>ลูบแมว</b>  {l.catsPetted} ครั้ง\n<b>เทศกาลที่ผ่านมา</b>  {l.festivals.Length}     <b>ชนะร้านคู่แข่ง</b>  {l.rivalWins}";
    }
}
