using System;
using UnityEngine;

// Reputation (0-5 stars), combo, XP/shop level (unlocks menu), and the day's stats.
public class ShopProgress : MonoBehaviour
{
    public static ShopProgress Instance { get; private set; }

    [Range(0f, 5f)] public float reputation = 3f;
    public int xp;
    public int level = 1;
    public int combo;
    public int bestCombo;
    public int maxLevel = 6;

    [Serializable]
    public class DayStats
    {
        public int day = 1;
        public int served, perfect, okay, rejected, angry;
        public int revenue, tips, expenses, bonus;
        public int bestCombo;
        public int Profit => revenue + tips + bonus - expenses;
    }

    public DayStats today = new DayStats();

    public static event Action OnChanged;
    public static event Action<CustomerController, ServeResult> OnServed;
    public static event Action<CustomerController> OnCustomerAngry;
    public static event Action<DayStats> OnDaySummary;
    public static event Action<int> OnLevelUp;

    private TimeCyc timeCyc;
    private bool dayFinalized;

    public int XpForNextLevel => 60 + (level - 1) * 50;
    public float ComboMultiplier => 1f + Mathf.Min(combo, 10) * 0.05f;

    // More stars = more people stop by
    public float SpawnRateMultiplier
    {
        get
        {
            float m = 0.5f + reputation * 0.2f;
            if (DailyEventSystem.Instance != null) m *= DailyEventSystem.Instance.Current.spawnMultiplier;
            if (UpgradeShop.Instance != null) m *= UpgradeShop.Instance.SpawnMultiplier;
            if (DecorShop.Instance != null) m *= DecorShop.Instance.SpawnMultiplier;
            return m;
        }
    }

    public float PatienceMultiplier
    {
        get
        {
            float m = 1f;
            if (DailyEventSystem.Instance != null) m *= DailyEventSystem.Instance.Current.patienceMultiplier;
            if (UpgradeShop.Instance != null) m *= UpgradeShop.Instance.PatienceMultiplier;
            if (DecorShop.Instance != null) m *= DecorShop.Instance.PatienceMultiplier;
            return m;
        }
    }

    public float TipMultiplier
    {
        get
        {
            float m = ComboMultiplier;
            if (DailyEventSystem.Instance != null) m *= DailyEventSystem.Instance.Current.tipMultiplier;
            if (UpgradeShop.Instance != null) m *= UpgradeShop.Instance.TipMultiplier;
            if (SpiritHouseOffering.Instance != null) m *= SpiritHouseOffering.Instance.TipMultiplier;
            if (DecorShop.Instance != null) m *= DecorShop.Instance.TipMultiplier;
            return m;
        }
    }

    void Awake()
    {
        // Scene-local singleton: the newest scene's instance wins (GameManager is the one that persists)
        Instance = this;
    }

    void Start()
    {
        timeCyc = FindFirstObjectByType<TimeCyc>();
        if (timeCyc != null)
        {
            timeCyc.OnDayEnd += FinalizeDay;
            timeCyc.OnNewDay += StartNewDay;
            today.day = timeCyc.Days;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (timeCyc != null)
        {
            timeCyc.OnDayEnd -= FinalizeDay;
            timeCyc.OnNewDay -= StartNewDay;
        }
    }

    public void LoadFrom(float rep, int savedXp, int savedLevel, int savedBestCombo)
    {
        reputation = Mathf.Clamp(rep, 0f, 5f);
        xp = savedXp;
        level = Mathf.Max(1, savedLevel);
        bestCombo = savedBestCombo;
        OnChanged?.Invoke();
    }

    // ---------------------------------------------------------------- events from gameplay
    public void RegisterServe(CustomerController customer, ServeResult result, int pay, int tip)
    {
        bool perfect = result.quality == ServeQuality.Perfect;
        bool vip = customer != null && customer.customerType == CustomerType.Influencer;

        combo = perfect ? combo + 1 : 0;
        bestCombo = Mathf.Max(bestCombo, combo);
        today.bestCombo = Mathf.Max(today.bestCombo, combo);

        tip = Mathf.RoundToInt(tip * TipMultiplier);

        int lucky = 0;
        if (SpiritHouseOffering.Instance != null && SpiritHouseOffering.Instance.RollLucky())
            lucky = SpiritHouseOffering.Instance.luckyBonus;

        if (GameManager.Instance != null)
            GameManager.Instance.AddMoney(pay + tip + lucky);

        today.served++;
        if (perfect) today.perfect++; else today.okay++;
        today.revenue += pay;
        today.tips += tip;
        today.bonus += lucky;

        float repDelta = perfect ? 0.04f : -0.02f;
        if (vip) repDelta = perfect ? 0.6f : -0.3f;
        reputation = Mathf.Clamp(reputation + repDelta, 0f, 5f);

        AddXp(perfect ? 12 : 5);

        string msg = $"+{pay} ฿";
        if (tip > 0) msg += Loc.T($"  ทิป +{tip} ฿", $"  tip +{tip} ฿");
        if (combo >= 2) msg += Loc.T($"  คอมโบ x{combo}!", $"  combo x{combo}!");
        if (!perfect) msg += "  (" + result.Summary + ")";
        ShopHUD.ToastNow(msg, 2f);                          // pay + tip shows right away; extras queue behind it
        if (lucky > 0) ShopHUD.Toast(Loc.T($"เจ้าที่ช่วย! ลูกค้าใจดีให้พิเศษ +{lucky} ฿", $"Spirit house luck! A generous customer gave +{lucky} ฿"));
        if (vip) ShopHUD.Toast(perfect ? Loc.T("รีวิว 5 ดาว! ร้านดังขึ้นทันที", "5-star review! The shop just got famous") : Loc.T("รีวิวกลางๆ ...", "A so-so review..."));

        OnServed?.Invoke(customer, result);
        OnChanged?.Invoke();
    }

    public void RegisterRejected(CustomerController customer)
    {
        combo = 0;
        today.rejected++;
        ShopHUD.ToastNow(Loc.T("ลูกค้าไม่รับแก้วนี้ ทิ้งแล้วทำใหม่ได้ที่ถังขยะ", "The customer refused this drink. Throw it in the bin and make a new one"));
        OnChanged?.Invoke();
    }

    public void RegisterAngry(CustomerController customer)
    {
        combo = 0;
        today.angry++;
        bool vip = customer != null && customer.customerType == CustomerType.Influencer;
        reputation = Mathf.Clamp(reputation - (vip ? 0.5f : 0.12f), 0f, 5f);
        ShopHUD.Toast(vip ? Loc.T("รีวิวเวอร์เดินออกไป... ชื่อเสียงลดลงเยอะ", "The reviewer walked out... big reputation hit") : Loc.T("ลูกค้ารอนานจนเดินออกไป", "A customer got tired of waiting and left"));
        OnCustomerAngry?.Invoke(customer);
        OnChanged?.Invoke();
    }

    public void RegisterExpense(int amount)
    {
        today.expenses += amount;
        OnChanged?.Invoke();
    }

    public void AddBonus(int amount, string reason)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.AddMoney(amount);
        today.bonus += amount;
        ShopHUD.Toast($"{reason} +{amount} ฿");
        OnChanged?.Invoke();
    }

    void AddXp(int amount)
    {
        if (level >= maxLevel) return;
        xp += amount;
        while (level < maxLevel && xp >= XpForNextLevel)
        {
            xp -= XpForNextLevel;
            level++;
            string unlocked = "";
            foreach (DrinkMenu.Drink d in DrinkMenu.All)
                if (d.unlockLevel == level) unlocked += " " + d.Name;
            ShopHUD.Toast(Loc.T($"ร้านเลเวล {level}!", $"Shop level {level}!") + (unlocked.Length > 0 ? Loc.T(" เมนูใหม่:", " New drink:") + unlocked : ""));
            OnLevelUp?.Invoke(level);
        }
    }

    // ---------------------------------------------------------------- day cycle
    // Called at 22:00 (TimeCyc.OnDayEnd) or when the player ends the day early. Safe to call twice.
    public void FinalizeDay()
    {
        if (dayFinalized) return;
        dayFinalized = true;

        // Delivery upgrade: extra income from app orders
        if (UpgradeShop.Instance != null && UpgradeShop.Instance.DeliveryShare > 0f)
        {
            int delivery = Mathf.RoundToInt(today.revenue * UpgradeShop.Instance.DeliveryShare);
            if (delivery > 0) AddBonus(delivery, Loc.T("ยอดขายเดลิเวอรี่", "Delivery sales"));
        }

        if (DailyGoals.Instance != null)
            DailyGoals.Instance.EvaluateEndOfDay(today);
        if (DecorShop.Instance != null)
            DecorShop.Instance.PayWages();                      // the helper's daily wage

        OnDaySummary?.Invoke(today);
    }

    void StartNewDay(int day)
    {
        dayFinalized = false;
        combo = 0;
        today = new DayStats { day = day };
        OnChanged?.Invoke();
    }
}
