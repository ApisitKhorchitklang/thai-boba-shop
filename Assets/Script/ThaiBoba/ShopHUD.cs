using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Binds the Thai boba systems to the HUD (Illustrator-made sprites in Assets/UI/Sprites). Every field is optional.
public class ShopHUD : MonoBehaviour
{
    [Header("Stats (top left)")]
    public TMP_Text moneyText;
    public TMP_Text reputationText;
    public Image[] reputationStars;          // 5 star icons, dimmed when not earned
    public TMP_Text levelText;
    public Image xpFill;                     // filled image 0..1
    public TMP_Text comboText;
    public GameObject comboRoot;

    [Header("Inventory chips")]
    public TMP_Text inventoryText;           // legacy single line
    public TMP_Text cupsText, bobaText, iceText, teaText, foamText;

    [Header("Event / goals / order")]
    public TMP_Text eventText;
    public Image eventIcon;
    public Sprite sunIcon, rainIcon, festivalIcon;
    public TMP_Text goalsText;
    public TMP_Text orderText;
    public TMP_Text orderTitle;
    public Image patienceFill;

    [Header("Weather")]
    public TMP_Text weatherText;
    public Image weatherIcon;
    public Sprite clearIcon, cloudIcon, rainSprite, stormIcon, coolIcon;

    [Header("Context")]
    public TMP_Text promptText;
    public GameObject promptRoot;
    public TMP_Text heldCupText;
    public GameObject heldCupRoot;
    public TMP_Text toastText;
    public RectTransform toastRoot;
    public float toastSeconds = 2.5f;

    [Header("End of day")]
    public GameObject summaryPanel;
    public TMP_Text summaryText;
    public TMP_Text upgradesText;
    public TMP_Text summaryTitle;

    public RaycastAim player;

    public static bool SummaryOpen { get; private set; }

    static event Action<string> ToastRequested;
    static event Action<string, float> ToastNowRequested;
    readonly Queue<string> toasts = new Queue<string>();
    float toastTimer, toastAge;

    // Callable from anywhere; logs even when no HUD exists
    public static void Toast(string message)
    {
        Debug.Log("[Shop] " + message);
        ToastRequested?.Invoke(message);
    }

    // Shows right away, replacing whatever toast is up (for rapid feedback like scooping pearls)
    public static void ToastNow(string message, float seconds = 1.6f)
    {
        ToastNowRequested?.Invoke(message, seconds);
    }

    void ShowNow(string msg, float seconds)
    {
        if (toastText == null) return;
        toastText.text = msg;
        toastTimer = seconds;
        toastAge = 0f;
    }

    void OnEnable()
    {
        ToastRequested += Enqueue;
        ToastNowRequested += ShowNow;
        Loc.Changed += LanguageChanged;
        ShopProgress.OnDaySummary += ShowSummary;
        DailyEventSystem.OnEventStarted += ShowEvent;
        UpgradeShop.OnChanged += RefreshUpgradeButtons;
        if (summaryPanel != null) summaryPanel.SetActive(false);
        SummaryOpen = false;
    }

    void OnDisable()
    {
        ToastRequested -= Enqueue;
        ToastNowRequested -= ShowNow;
        Loc.Changed -= LanguageChanged;
        ShopProgress.OnDaySummary -= ShowSummary;
        DailyEventSystem.OnEventStarted -= ShowEvent;
        UpgradeShop.OnChanged -= RefreshUpgradeButtons;
    }

    // bought upgrades say so (and can't be bought twice) instead of still showing a price
    void RefreshUpgradeButtons()
    {
        UpgradeShop shop = UpgradeShop.Instance;
        if (summaryPanel == null || shop == null) return;
        foreach (UpgradeShop.Upgrade u in shop.upgrades)
        {
            Transform t = summaryPanel.transform.Find("Card/Upgrade_" + u.id);
            if (t == null) continue;
            Button b = t.GetComponent<Button>();
            if (b != null) b.interactable = !u.owned;
            TMP_Text label = t.GetComponentInChildren<TMP_Text>(true);
            LocalizedText lt = label != null ? label.GetComponent<LocalizedText>() : null;
            if (lt == null) continue;
            lt.th = u.owned ? $"{u.nameTH}  · มีแล้ว" : $"{u.nameTH}  {u.price}฿";
            lt.en = u.owned ? $"{u.nameEN ?? u.nameTH}  · owned" : $"{u.nameEN ?? u.nameTH}  {u.price}฿";
            lt.Apply();
        }
    }

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<RaycastAim>();
        foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(true))
            ThaiText.Enable(t);
        if (DailyEventSystem.Instance != null) ShowEvent(DailyEventSystem.Instance.Current);
    }

    // Texts that are only set on events: redraw them in the new language
    void LanguageChanged()
    {
        if (DailyEventSystem.Instance != null) ShowEvent(DailyEventSystem.Instance.Current);
        if (SummaryOpen && lastStats != null) ShowSummary(lastStats);
    }

    void Enqueue(string msg)
    {
        toasts.Enqueue(msg);
    }

    void Update()
    {
        ShopProgress sp = ShopProgress.Instance;
        if (moneyText != null && GameManager.Instance != null)
            moneyText.text = $"{GameManager.Instance.money:N0}";
        if (sp != null)
        {
            if (reputationText != null) reputationText.text = $"{sp.reputation:0.0}";
            if (reputationStars != null)
                for (int i = 0; i < reputationStars.Length; i++)
                    if (reputationStars[i] != null)
                        reputationStars[i].color = sp.reputation >= i + 0.5f ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            bool max = sp.level >= sp.maxLevel;
            if (levelText != null) levelText.text = max ? Loc.T($"เลเวล {sp.level}  สูงสุด", $"Level {sp.level}  MAX") : Loc.T($"เลเวล {sp.level}", $"Level {sp.level}");
            if (xpFill != null) xpFill.fillAmount = max ? 1f : Mathf.Clamp01((float)sp.xp / sp.XpForNextLevel);
            if (comboRoot != null) comboRoot.SetActive(sp.combo >= 2);
            if (comboText != null) comboText.text = sp.combo >= 2 ? Loc.T($"คอมโบ x{sp.combo}", $"Combo x{sp.combo}") : "";
        }

        // the order buttons on the summary change the stock: keep the "remaining" block current
        if (SummaryOpen && summaryText != null && StockLine() != lastStock) ShowSummary(lastStats);

        WeatherSystem w = WeatherSystem.Instance;
        if (w != null)
        {
            if (weatherText != null) weatherText.text = $"{WeatherSystem.SeasonTH(w.CurrentSeason)} · {w.SkyTH}";
            if (weatherIcon != null)
            {
                Sprite s = w.Now == WeatherSystem.Sky.Storm ? stormIcon : w.Now == WeatherSystem.Sky.Rain ? rainSprite
                         : w.Now == WeatherSystem.Sky.Cloudy ? cloudIcon : w.CurrentSeason == WeatherSystem.Season.Cool ? coolIcon : clearIcon;
                if (s != null && weatherIcon.sprite != s) weatherIcon.sprite = s;
            }
        }

        InventoryManager inv = InventoryManager.Instance;
        if (inv != null)
        {
            if (inventoryText != null) inventoryText.text = Loc.T($"แก้ว {inv.cups} · ไข่มุก {inv.boba} · น้ำแข็ง {inv.ice} · ชา {inv.baseSyrup}", $"Cups {inv.cups} · Pearls {inv.boba} · Ice {inv.ice} · Tea {inv.baseSyrup}");
            if (cupsText != null) cupsText.text = inv.cups.ToString();
            if (bobaText != null) bobaText.text = inv.boba.ToString();
            if (iceText != null) iceText.text = inv.ice.ToString();
            if (teaText != null) teaText.text = inv.baseSyrup.ToString();
            if (foamText != null) foamText.text = inv.foam.ToString();
        }
        if (goalsText != null && DailyGoals.Instance != null)
            goalsText.text = DailyGoals.Instance.Describe().Replace(Loc.T("ภารกิจวันนี้\n", "Today's goals\n"), "");

        CustomerController c = CustomerController.PlayerFront;
        bool hasCustomer = c != null && c.Profile != null;
        if (orderTitle != null) orderTitle.text = hasCustomer ? c.DisplayName : Loc.T("ออเดอร์", "Order");
        if (orderText != null)
        {
            string order = hasCustomer ? c.order.ToThaiString() : Loc.T("รอลูกค้า...", "Waiting for customers...");
            BaristaStaff b = BaristaStaff.Instance;
            if (b != null && b.Working != null)        // say plainly who the barista has, so nobody makes that drink twice
                order += Loc.T($"\n<size=78%><color=#2E8B57>พนักงานดูแล: {b.Working.order.DrinkNameTH} ({b.Progress01 * 100f:0}%) ไม่ต้องทำแก้วนี้</color></size>",
                               $"\n<size=78%><color=#2E8B57>Barista has: {b.Working.order.DrinkNameTH} ({b.Progress01 * 100f:0}%), skip it</color></size>");
            orderText.text = order;
        }
        if (patienceFill != null)
        {
            float p = hasCustomer ? c.PatienceNormalized : 0f;
            patienceFill.fillAmount = p;
            patienceFill.color = Color.Lerp(new Color(0.95f, 0.3f, 0.3f), new Color(0.45f, 0.8f, 0.35f), p);
        }

        if (player != null)
        {
            string prompt = player.CurrentTarget != null ? player.CurrentTarget.Prompt(player) : "";
            if (GameSettings.UsePCControls && prompt.Length > 0) prompt = "[E]  " + prompt;
            if (promptText != null) promptText.text = prompt;
            if (promptRoot != null) promptRoot.SetActive(prompt.Length > 0);
            CupController cup = player.HeldCup;
            if (heldCupText != null) heldCupText.text = cup != null ? CupSummary(cup) : "";
            if (heldCupRoot != null) heldCupRoot.SetActive(cup != null);
        }

        // toast: pops in with a little bounce, fades out
        if (toastText != null)
        {
            toastTimer -= Time.unscaledDeltaTime;
            toastAge += Time.unscaledDeltaTime;
            if (toastTimer <= 0f)
            {
                if (toasts.Count > 0)
                {
                    toastText.text = toasts.Dequeue();
                    toastTimer = toastSeconds;
                    toastAge = 0f;
                }
                else toastText.text = "";
            }
            if (toastRoot != null)
            {
                toastRoot.gameObject.SetActive(toastText.text.Length > 0);
                float s = toastAge < 0.25f ? Mathf.Lerp(0.6f, 1.08f, toastAge / 0.25f) : Mathf.Lerp(1.08f, 1f, Mathf.Clamp01((toastAge - 0.25f) / 0.15f));
                toastRoot.localScale = Vector3.one * s;
            }
        }
    }

    static string CupSummary(CupController cup)
    {
        DrinkMenu.Drink d = DrinkMenu.Get(cup.flavor);
        string tea = d != null ? d.Name : Loc.T("ยังไม่เทชา", "no tea yet");
        string seal = cup.State == CupController.CupState.LidClosed ? Loc.T("ซีลแล้ว", "sealed") : Loc.T("ยังไม่ซีล", "not sealed");
        string ice = cup.hasIce ? Loc.T("มีน้ำแข็ง", "ice") : Loc.T("ไม่มีน้ำแข็ง", "no ice");
        if (cup.hasFoam) ice += Loc.T(" · ชีสโฟม", " · cheese foam");
        if (cup.blended) ice += Loc.T(" · ปั่น", " · blended");
        if (cup.marshmallow) ice += Loc.T(" · มาร์ชเมลโลว์", " · marshmallows");
        DrinkMenu.Topping tp = DrinkMenu.GetTopping(cup.topping);
        if (tp != null) ice += " · " + tp.Name;
        if (cup.watered) ice += Loc.T(" · <color=#4FA3E0>เปียกน้ำ!</color>", " · <color=#4FA3E0>soaked!</color>");
        return Loc.T($"ในมือ: {tea} · ไข่มุก {cup.bobaCount} · {ice} · {DrinkMenu.SweetTH(cup.sweetness)} · {seal}",
                     $"Holding: {tea} · pearls {cup.bobaCount} · {ice} · {DrinkMenu.SweetTH(cup.sweetness)} · {seal}");
    }

    void ShowEvent(DailyEventSystem.DayEvent ev)
    {
        if (eventText != null)
            eventText.text = $"{ev.Title}\n<size=72%>{ev.Description}</size>";
        if (eventIcon != null)
        {
            Sprite s = ev.type == DailyEventSystem.EventType.HeavyRain ? rainIcon
                     : DailyEventSystem.IsFestival(ev.type) || ev.type == DailyEventSystem.EventType.Payday ? festivalIcon : sunIcon;
            if (s != null) eventIcon.sprite = s;
        }
    }

    static string Regulars() => RegularsSystem.Instance != null ? RegularsSystem.Instance.TodaySummary() : "";

    // deliveries, toppings, the barista, ang pao + the rival across the street
    static string Extras(ShopProgress.DayStats s)
    {
        var parts = new List<string>();
        if (s.deliveries + s.deliveriesLate > 0) parts.Add(Loc.T($"เดลิเวอรี่ทัน {s.deliveries} · ไม่ทัน {s.deliveriesLate}", $"Deliveries {s.deliveries} on time · {s.deliveriesLate} late"));
        if (s.toppings > 0) parts.Add(Loc.T($"ท็อปปิ้ง {s.toppings}", $"Toppings {s.toppings}"));
        if (s.staffDrinks > 0) parts.Add(Loc.T($"พนักงานชงให้ {s.staffDrinks} แก้ว", $"Barista served {s.staffDrinks}"));
        if (s.angPao > 0) parts.Add(Loc.T($"อั่งเปา {s.angPao} ซอง", $"Ang pao {s.angPao}"));
        string line = parts.Count > 0 ? "\n<size=90%>" + string.Join("   ·   ", parts) + "</size>" : "";
        return line + (RivalShop.Instance != null ? RivalShop.Instance.SummaryLine() : "");
    }

    // best seller + busiest hour, to plan tomorrow (cook pearls before the rush, stock up the favourite)
    static string Insights(ShopProgress.DayStats s)
    {
        string best = s.BestSeller(out int n);
        int hour = s.BusiestHour(out int hc);
        if (best == null && hour < 0) return "";
        string line = "\n<size=90%>";
        if (best != null) line += Loc.T($"ขายดีสุด: <b>{best}</b> ({n} แก้ว)", $"Best seller: <b>{best}</b> ({n})");
        if (hour >= 0) line += (best != null ? "   ·   " : "") + Loc.T($"คนเยอะสุด: <b>{hour:00}:00–{hour + 1:00}:00</b> ({hc} คน)", $"Busiest: <b>{hour:00}:00–{hour + 1:00}:00</b> ({hc})");
        return line + "</size>";
    }

    ShopProgress.DayStats lastStats;
    string lastStock;

    // Remaining stock under the day's numbers; low items in red so you know what to order for tomorrow
    static string StockLine()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return "";
        string Item(string label, int n, int low) => n < low ? $"{label} <color=#C23A66><b>{n}</b></color>" : $"{label} <b>{n}</b>";
        return $"\n\n<b>{Loc.T("ของคงเหลือ", "Stock left")}</b>\n{Item(Loc.T("แก้ว", "Cups"), inv.cups, 15)}   {Item(Loc.T("ไข่มุก", "Pearls"), inv.boba, 60)}   {Item(Loc.T("น้ำแข็ง", "Ice"), inv.ice, 120)}   {Item(Loc.T("ชา", "Tea"), inv.baseSyrup, 80)}   {Item(Loc.T("ชีสโฟม", "Foam"), inv.foam, 10)}" +
               (inv.cups < 15 || inv.boba < 60 || inv.ice < 120 || inv.baseSyrup < 80 || inv.foam < 10 ? Loc.T("\n<size=80%><color=#C23A66>ตัวสีแดงใกล้หมด สั่งเพิ่มก่อนเปิดร้านพรุ่งนี้</color></size>", "\n<size=80%><color=#C23A66>Red items are running low, restock before opening tomorrow</color></size>") : "");
    }

    // the challenge result replaces the restock / upgrade side of the summary
    void SetChallengeLayout(bool challenge)
    {
        if (summaryPanel == null) return;
        foreach (Transform t in summaryPanel.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Supply") || t.name.StartsWith("Upgrade")) t.gameObject.SetActive(!challenge);
        Transform next = summaryPanel.transform.Find("Card/NextDay");
        TMP_Text label = next != null ? next.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null)
        {
            LocalizedText lt = label.GetComponent<LocalizedText>();
            if (lt != null) lt.enabled = !challenge;
            if (challenge) label.text = Loc.T("กลับเมนูหลัก", "Main menu");
            else if (lt != null) lt.Apply();
        }
    }

    void ShowSummary(ShopProgress.DayStats s)
    {
        lastStats = s;
        SetChallengeLayout(ChallengeMode.Active);
        if (ChallengeMode.Active)
        {
            int score = ChallengeMode.Score(s);
            bool best = ChallengeMode.Record(score);
            string share = ChallengeMode.ShareText(s, score);
            GUIUtility.systemCopyBuffer = share;
            if (summaryTitle != null) summaryTitle.text = Loc.T($"ท้าทายรายวัน {ChallengeMode.DateLabel}", $"Daily challenge {ChallengeMode.DateLabel}");
            if (summaryText != null)
                summaryText.text = Loc.T(
                    $"<size=150%><b>{score} คะแนน</b></size>" + (best ? "  <color=#4E8A3C>สถิติใหม่!</color>" : $"  <size=80%>(สถิติวันนี้ {ChallengeMode.Best(ChallengeMode.Seed)})</size>") + "\n\n" +
                    $"กำไร {s.Profit} ฿  +  เป๊ะ {s.perfect} × 20  +  คอมโบ x{s.bestCombo} × 5\n" +
                    $"เสิร์ฟ {s.served} แก้ว · ลูกค้าเดินหนี {s.angry}\n\n" +
                    "<size=80%>คัดลอกผลไว้แล้ว วางแชร์ให้เพื่อนได้เลย:\n<i>" + share + "</i></size>",
                    $"<size=150%><b>{score} points</b></size>" + (best ? "  <color=#4E8A3C>New best!</color>" : $"  <size=80%>(today's best {ChallengeMode.Best(ChallengeMode.Seed)})</size>") + "\n\n" +
                    $"Profit {s.Profit} ฿  +  perfect {s.perfect} × 20  +  combo x{s.bestCombo} × 5\n" +
                    $"Served {s.served} · walked out {s.angry}\n\n" +
                    "<size=80%>Result copied, paste it to share:\n<i>" + share + "</i></size>");
            if (summaryPanel != null) summaryPanel.SetActive(true);
            SummaryOpen = true;
            return;
        }
        lastStock = StockLine();
        RefreshUpgradeButtons();
        if (summaryTitle != null) summaryTitle.text = Loc.T($"ปิดร้าน · สรุปยอดวันที่ {s.day}", $"Closing time · Day {s.day} summary");
        if (summaryText != null)
        {
            summaryText.text = Loc.EN
                ? $"Served  <b>{s.served}</b> drinks   (perfect {s.perfect} · okay {s.okay})\n" +
                  $"Customers who left  {s.angry}     Drinks refused  {s.rejected}\n" +
                  $"Best combo  x{s.bestCombo}\n\n" +
                  $"Sales  <color=#4E8A3C>+{s.revenue} ฿</color>     Tips  <color=#4E8A3C>+{s.tips} ฿</color>\n" +
                  $"Bonus  <color=#4E8A3C>+{s.bonus} ฿</color>     Expenses  <color=#C23A66>-{s.expenses} ฿</color>\n" +
                  $"<size=125%><b>Net profit  {s.Profit} ฿</b></size>" + Insights(s) + Extras(s) + Regulars() + lastStock
                : $"เสิร์ฟ  <b>{s.served}</b> แก้ว   (เป๊ะ {s.perfect} · พอใช้ {s.okay})\n" +
                  $"ลูกค้าเดินหนี  {s.angry}     แก้วโดนปฏิเสธ  {s.rejected}\n" +
                  $"คอมโบสูงสุด  x{s.bestCombo}\n\n" +
                  $"ยอดขาย  <color=#4E8A3C>+{s.revenue} ฿</color>     ทิป  <color=#4E8A3C>+{s.tips} ฿</color>\n" +
                  $"โบนัส  <color=#4E8A3C>+{s.bonus} ฿</color>     ค่าใช้จ่าย  <color=#C23A66>-{s.expenses} ฿</color>\n" +
                  $"<size=125%><b>กำไรสุทธิ  {s.Profit} ฿</b></size>" + Insights(s) + Extras(s) + Regulars() + lastStock;
        }
        if (upgradesText != null && UpgradeShop.Instance != null)
            upgradesText.text = UpgradeShop.Instance.Describe();
        if (summaryPanel != null) summaryPanel.SetActive(true);
        SummaryOpen = true;
    }

    // Hook to the summary panel's "next day" button; the day is auto-saved
    public void NextDayButton()
    {
        GameAudio.Play(Sfx.Click);
        if (ChallengeMode.Active)
        {
            SummaryOpen = false;
            if (GameMenus.Instance != null) GameMenus.Instance.MainMenuButton();   // ends the challenge, back to the title
            return;
        }
        if (summaryPanel != null) summaryPanel.SetActive(false);
        SummaryOpen = false;
        if (GameManager.Instance != null) GameManager.Instance.EndDay();
        Toast(Loc.T("บันทึกเกมอัตโนมัติแล้ว · เปิดร้านวันใหม่!", "Game auto-saved · a new day begins!"));
    }
}
