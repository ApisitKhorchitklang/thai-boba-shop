using System;
using System.Collections.Generic;
using UnityEngine;

// Spend profits on the shop. Each upgrade can switch on scene objects (fan, stools, neon sign...).
public class UpgradeShop : MonoBehaviour
{
    public static UpgradeShop Instance { get; private set; }

    [Serializable]
    public class Upgrade
    {
        public string id;
        public string nameTH;
        public string descriptionTH;
        public string nameEN, descriptionEN;
        public string Name => Loc.T(nameTH, nameEN ?? nameTH);
        public string Description => Loc.T(descriptionTH, descriptionEN ?? descriptionTH);
        public int price;
        public bool owned;
        [Tooltip("Objects activated once owned (e.g. SM_StandingFan, SM_PlasticStool...)")]
        public GameObject[] enableWhenOwned;

        public Upgrade(string id, string nameTH, string descriptionTH, int price)
        {
            this.id = id;
            this.nameTH = nameTH;
            this.descriptionTH = descriptionTH;
            this.price = price;
        }
    }

    public List<Upgrade> upgrades = new List<Upgrade>
    {
        new Upgrade("speaker",     "ลำโพงเปิดเพลงลูกทุ่ง",      "บรรยากาศดี ทิป +10%", 250) { nameEN = "Luk thung speaker", descriptionEN = "Good vibes: tips +10%" },
        new Upgrade("fan",         "พัดลมตั้งพื้น",              "ลูกค้าใจเย็นขึ้น ความอดทน +20%", 300) { nameEN = "Standing fan", descriptionEN = "Cooler customers: patience +20%" },
        new Upgrade("seating",     "โต๊ะสแตนเลส + เก้าอี้แดง",   "นั่งรอสบาย ความอดทน +10% ทิป +10%", 450) { nameEN = "Steel table + red stools", descriptionEN = "Comfy waiting: patience +10%, tips +10%" },
        new Upgrade("bigger_pot",  "หม้อต้มไข่มุกใบใหญ่",         "ต้มได้ครั้งละ 50 ที่ + ไข่มุกดิบฟรี 60 ทุกเช้า", 500) { nameEN = "Big pearl pot", descriptionEN = "Cooks 50 portions per batch + 60 free raw pearls every morning" },
        new Upgrade("fast_sealer", "เครื่องซีลรุ่นเทอร์โบ",        "ซีลแก้วเร็วขึ้น 3 เท่า", 600) { nameEN = "Turbo sealer", descriptionEN = "Seals cups 3x faster" },
        new Upgrade("neon",        "ป้ายไฟนีออน",                "ร้านเด่น ลูกค้าเพิ่ม 25%", 800) { nameEN = "Neon sign", descriptionEN = "Stand out: +25% customers" },
        new Upgrade("delivery",    "เปิดรับเดลิเวอรี่",            "รายได้เพิ่ม 15% ของยอดขายทุกวัน", 1500) { nameEN = "Delivery service", descriptionEN = "Extra income: 15% of daily sales" },
    };

    public static event Action OnChanged;

    private TimeCyc timeCyc;

    public float SealTimeMultiplier => Has("fast_sealer") ? 0.33f : 1f;
    public float PatienceMultiplier => (Has("fan") ? 1.2f : 1f) * (Has("seating") ? 1.1f : 1f);
    public float TipMultiplier => (Has("speaker") ? 1.1f : 1f) * (Has("seating") ? 1.1f : 1f);
    public float SpawnMultiplier => Has("neon") ? 1.25f : 1f;
    public float DeliveryShare => Has("delivery") ? 0.15f : 0f;

    void Awake()
    {
        // Scene-local singleton: the newest scene's instance wins (GameManager is the one that persists)
        Instance = this;
        ApplyVisuals();
    }

    void Start()
    {
        timeCyc = FindFirstObjectByType<TimeCyc>();
        if (timeCyc != null) timeCyc.OnNewDay += MorningPerks;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (timeCyc != null) timeCyc.OnNewDay -= MorningPerks;
    }

    void MorningPerks(int day)
    {
        if (Has("bigger_pot") && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.Add("boba", 60);
            ShopHUD.Toast(Loc.T("ต้มไข่มุกหม้อใหญ่ +60", "Big pot of pearls +60"));
        }
    }

    public Upgrade Find(string id)
    {
        return upgrades.Find(u => u.id == id);
    }

    public bool Has(string id)
    {
        Upgrade u = Find(id);
        return u != null && u.owned;
    }

    // Hook to a UI Button: OnClick → UpgradeShop.BuyUpgrade("fan")
    public void BuyUpgrade(string id)
    {
        Buy(id);
    }

    public bool Buy(string id)
    {
        Upgrade u = Find(id);
        if (u == null || u.owned) return false;

        if (GameManager.Instance == null || !GameManager.Instance.TrySpend(u.price))
        {
            ShopHUD.Toast(Loc.T($"เงินไม่พอซื้อ {u.Name} ({u.price} ฿)", $"Not enough money for {u.Name} ({u.price} ฿)"));
            return false;
        }
        if (ShopProgress.Instance != null)
            ShopProgress.Instance.RegisterExpense(u.price);

        u.owned = true;
        ApplyVisuals();
        string where = Where(u.id);
        ShopHUD.Toast(Loc.T($"ซื้อ {u.Name} แล้ว! ", $"Bought {u.Name}! ") + u.Description + (where.Length > 0 ? "\n<size=80%>" + where + "</size>" : ""));
        OnChanged?.Invoke();
        return true;
    }

    // where to look for it after buying
    static string Where(string id)
    {
        switch (id)
        {
            case "speaker": return Loc.T("ลำโพงแขวนอยู่มุมหน้าร้านทั้งสองข้าง", "Speakers hang in both front corners");
            case "fan": return Loc.T("พัดลมตั้งอยู่ในร้านด้านขวา", "The fan stands inside on the right");
            case "seating": return Loc.T("โต๊ะกับเก้าอี้แดงตั้งอยู่บนทางเท้า ด้านขวาของร้าน", "The table and stools are out on the footpath, right of the shop");
            case "bigger_pot": return Loc.T("หม้อไข่มุกบนเคาน์เตอร์หลังใหญ่ขึ้นแล้ว", "The pearl pot on the back counter got bigger");
            case "fast_sealer": return Loc.T("เครื่องซีลติดชุดแต่ง TURBO แล้ว", "The sealer now wears its TURBO kit");
            case "neon": return Loc.T("ป้ายนีออนอยู่หน้าตึกกับผนังซ้ายในร้าน", "Neon on the shopfront and on the left wall inside");
            case "delivery": return Loc.T("มอเตอร์ไซค์ส่งของจอดอยู่หน้าร้าน", "The delivery scooter is parked out front");
            default: return "";
        }
    }

    public string[] OwnedIds()
    {
        List<string> ids = new List<string>();
        foreach (Upgrade u in upgrades)
            if (u.owned) ids.Add(u.id);
        return ids.ToArray();
    }

    public void LoadOwned(string[] ids)
    {
        foreach (Upgrade u in upgrades)
            u.owned = ids != null && Array.IndexOf(ids, u.id) >= 0;
        ApplyVisuals();
        OnChanged?.Invoke();
    }

    void ApplyVisuals()
    {
        foreach (Upgrade u in upgrades)
        {
            if (u.enableWhenOwned == null) continue;
            foreach (GameObject go in u.enableWhenOwned)
                if (go != null) go.SetActive(u.owned);
        }
    }

    public string Describe()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (Upgrade u in upgrades)
            sb.AppendLine(u.owned ? Loc.T("[มีแล้ว] ", "[Owned] ") + u.Name : $"{u.Name} — {u.price} ฿ : {u.Description}");
        return sb.ToString();
    }
}
