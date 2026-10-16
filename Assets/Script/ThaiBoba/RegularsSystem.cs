using System;
using System.Collections.Generic;
using UnityEngine;

// Regular customers of the soi: named people with a usual order who come back on their own days.
// Friendship 0..5: a perfect drink +1, walking out angry -1. Friends tip more and wait longer; from 3 they bring
// friends (more customers); reaching 5 gives a 200 baht gift, and the first one to get there shares the secret recipe
// (Butterfly Pea Lime, อัญชันมะนาว). Friendship and the recipe are saved.
public class RegularsSystem : MonoBehaviour
{
    public static RegularsSystem Instance { get; private set; }
    public static bool SecretUnlocked => Instance != null && Instance.secretUnlocked;
    public const string SecretDrink = "ButterflyPea";

    [Serializable]
    public class Regular
    {
        public string id, nameTH, nameEN, model;
        public CustomerType type;
        public string flavor;
        public int boba;
        public bool ice;
        public int sweet;
        public bool foam;
        [Range(0f, 1f)] public float visitChance = 0.45f;
        public string usualTH, usualEN;            // how they ask for "the usual"

        [NonSerialized] public int hearts;
        [NonSerialized] public int planHour = -1;
        [NonSerialized] public bool visitedToday;

        public string Name => Loc.T(nameTH, nameEN);
    }

    public List<Regular> regulars = Defaults();
    bool secretUnlocked;
    TimeCyc clock;
    readonly List<string> todayLog = new List<string>();

    public static event Action<Regular> OnFriendshipChanged;

    static List<Regular> Defaults() => new List<Regular>
    {
        new Regular { id = "mind", nameTH = "น้องมายด์", nameEN = "Mind", model = "NPC_Student", type = CustomerType.Student,
                      flavor = "ThaiTea", boba = 2, ice = true, sweet = 50, visitChance = 0.5f,
                      usualTH = "พี่ขา เหมือนเดิมนะคะ ชาไทยไข่มุกเยอะๆ!", usualEN = "The usual please! Thai tea, lots of pearls!" },
        new Regular { id = "sri", nameTH = "ป้าศรี", nameEN = "Auntie Sri", model = "NPC_Auntie", type = CustomerType.Auntie,
                      flavor = "MilkTea", boba = 1, ice = true, sweet = 25, visitChance = 0.45f,
                      usualTH = "ลูก ของป้าเหมือนเดิมจ้ะ หวานน้อยนะ", usualEN = "Dear, Auntie's usual, not too sweet" },
        new Regular { id = "win", nameTH = "พี่วิน", nameEN = "Win", model = "NPC_Rider", type = CustomerType.Rider,
                      flavor = "RedLime", boba = 0, ice = true, sweet = 100, visitChance = 0.6f,
                      usualTH = "แดงโซดาเหมือนเดิมครับ รีบนิดนึง!", usualEN = "Red soda as usual, quick!" },
        new Regular { id = "jay", nameTH = "คุณเจ", nameEN = "Jay", model = "NPC_Office", type = CustomerType.OfficeWorker,
                      flavor = "Matcha", boba = 0, ice = true, sweet = 0, foam = true, visitChance = 0.45f,
                      usualTH = "เหมือนเดิมครับ ชาเขียวไม่หวาน ท็อปโฟม", usualEN = "The usual: green tea, no sugar, cheese foam" },
        new Regular { id = "chai", nameTH = "ลุงชัย", nameEN = "Uncle Chai", model = "NPC_Grandpa", type = CustomerType.Auntie,
                      flavor = "Cocoa", boba = 1, ice = false, sweet = 100, visitChance = 0.4f,
                      usualTH = "โกโก้ร้อนแก้วเดิมนะหลาน", usualEN = "My hot cocoa, as always" },
        new Regular { id = "praewa", nameTH = "แพรวา", nameEN = "Praewa", model = "NPC_Influencer", type = CustomerType.Influencer,
                      flavor = "Taro", boba = 2, ice = true, sweet = 50, foam = true, visitChance = 0.35f,
                      usualTH = "เผือกท็อปโฟมแก้วเดิมค่ะ วันนี้ไลฟ์ด้วยนะ!", usualEN = "My taro with cheese foam, I'm live today!" },
    };

    void Awake() { Instance = this; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (clock != null) clock.OnNewDay -= PlanDay;
    }

    void Start()
    {
        clock = FindFirstObjectByType<TimeCyc>();
        if (clock != null) clock.OnNewDay += PlanDay;
        PlanDay(clock != null ? clock.Days : 1);
    }

    // Who drops by today, and at what hour (seeded by the day, so a reloaded day keeps its visitors)
    public void PlanDay(int day)
    {
        System.Random rng = new System.Random(day * 7919 + 3);
        foreach (Regular r in regulars)
        {
            r.visitedToday = false;
            r.planHour = rng.NextDouble() < r.visitChance ? rng.Next(8, 20) : -1;
        }
        todayLog.Clear();
    }

    // Spawner asks this when a customer is about to come: a regular whose hour has come takes the slot
    public Regular TakeDue(int hour)
    {
        foreach (Regular r in regulars)
            if (!r.visitedToday && r.planHour >= 0 && hour >= r.planHour)
            {
                r.visitedToday = true;
                return r;
            }
        return null;
    }

    public void ForceDue(int index) { regulars[index].visitedToday = false; regulars[index].planHour = 0; }   // tests / debug

    public Order FavoriteOrder(Regular r)
    {
        string flavor = r.flavor;
        DrinkMenu.Drink d = DrinkMenu.Get(flavor);
        int level = ShopProgress.Instance != null ? ShopProgress.Instance.level : 99;
        if (d == null || !DrinkMenu.IsUnlocked(d, level)) flavor = "ThaiTea";
        return new Order(r.boba, r.ice, flavor, r.sweet) { wantsFoam = r.foam && flavor != "RedLime" };
    }

    public string Greeting(Regular r, Order o)
    {
        if (r.hearts >= 2) return $"{r.Name}: " + Loc.T(r.usualTH, r.usualEN);
        return $"{r.Name}: " + Loc.T($"ขอ{o.DrinkNameTH}แก้วนึง", $"One {o.DrinkNameTH}, please!");
    }

    public string Thanks(Regular r, ServeQuality q)
    {
        return $"{r.Name}: " + (q == ServeQuality.Perfect
            ? (r.hearts >= 3 ? Loc.T("อร่อยเหมือนเดิมเลย! พรุ่งนี้มาอีกนะ", "Perfect as always! See you tomorrow") : Loc.T("อร่อยมาก! เดี๋ยวมาอีกนะ", "So good! I'll be back"))
            : Loc.T("อืม...วันนี้ไม่เหมือนทุกที", "Hmm... not like usual today"));
    }

    public float TipMultiplier(Regular r) => 1f + 0.15f * r.hearts;
    public float PatienceMultiplier(Regular r) => 1f + 0.1f * r.hearts;

    // regulars at 3+ hearts tell their friends about the shop
    public float SpawnBonus
    {
        get
        {
            int n = 0;
            foreach (Regular r in regulars) if (r.hearts >= 3) n++;
            return 1f + 0.04f * n;
        }
    }

    public string FriendshipLabel(Regular r) => Loc.T($"ลูกค้าประจำ · สนิท {r.hearts}/5", $"regular · friendship {r.hearts}/5");

    public void OnServed(Regular r, ServeQuality q)
    {
        if (r == null) return;
        if (q == ServeQuality.Perfect && r.hearts < 5)
        {
            r.hearts++;
            ShopHUD.Toast(Loc.T($"{r.nameTH} ประทับใจ! สนิท {r.hearts}/5", $"{r.nameEN} loved it! Friendship {r.hearts}/5"));
            if (r.hearts == 3) ShopHUD.Toast(Loc.T($"{r.nameTH} เริ่มชวนเพื่อนมาร้าน (ลูกค้าเพิ่มขึ้น)", $"{r.nameEN} is bringing friends (more customers)"));
            if (r.hearts == 5)
            {
                if (ShopProgress.Instance != null) ShopProgress.Instance.AddBonus(200, Loc.T($"ของขวัญจาก{r.nameTH}", $"Gift from {r.nameEN}"));
                if (!secretUnlocked)
                {
                    secretUnlocked = true;
                    GameAudio.Play(Sfx.LevelUp);
                    ShopHUD.Toast(Loc.T($"{r.nameTH} บอกสูตรลับ: อัญชันมะนาว! เมนูใหม่ปลดล็อก", $"{r.nameEN} shared a secret recipe: Butterfly Pea Lime! New drink unlocked"));
                }
            }
            OnFriendshipChanged?.Invoke(r);
        }
        todayLog.Add($"{r.Name} {(q == ServeQuality.Perfect ? "+1" : "·")} ({r.hearts}/5)");
    }

    public void OnLeftAngry(Regular r)
    {
        if (r == null) return;
        if (r.hearts > 0) r.hearts--;
        todayLog.Add($"{r.Name} -1 ({r.hearts}/5)");
        OnFriendshipChanged?.Invoke(r);
    }

    public string TodaySummary()
    {
        if (todayLog.Count == 0) return "";
        return $"\n<size=90%>{Loc.T("ลูกค้าประจำวันนี้", "Regulars today")}: {string.Join(" · ", todayLog)}</size>";
    }

    // ---------------------------------------------------------------- save / load
    public int[] HeartsArray()
    {
        int[] h = new int[regulars.Count];
        for (int i = 0; i < h.Length; i++) h[i] = regulars[i].hearts;
        return h;
    }

    public void Load(int[] hearts, bool secret)
    {
        for (int i = 0; i < regulars.Count; i++) regulars[i].hearts = hearts != null && i < hearts.Length ? Mathf.Clamp(hearts[i], 0, 5) : 0;
        secretUnlocked = secret;
    }
}
