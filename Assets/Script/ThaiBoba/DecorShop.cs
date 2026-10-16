using System;
using System.Collections.Generic;
using UnityEngine;

// Decorate and grow the shop (bought at the shop computer):
//   decorations add "beauty" (more customers, a bit more patience);
//   hiring a helper (daily wage) gets pearls cooked and supplies reordered automatically;
//   expanding into the shophouse next door adds seating: many more customers, a longer queue, calmer customers.
public class DecorShop : MonoBehaviour
{
    public static DecorShop Instance { get; private set; }

    public enum Kind { Decor, Helper, Expansion }

    [Serializable]
    public class Item
    {
        public string id, nameTH, nameEN, descTH, descEN;
        public int price;
        public int beauty;
        public Kind kind;
        public GameObject show;          // switched on when owned
        public GameObject hide;          // switched off when owned (e.g. the pharmacy next door)
        [NonSerialized] public bool owned;
        public string Name => Loc.T(nameTH, nameEN);
        public string Desc => Loc.T(descTH, descEN);
    }

    public int helperWage = 120;
    public List<Item> items = Defaults();

    public static event Action OnChanged;

    public static List<Item> Defaults() => new List<Item>
    {
        new Item { id = "plants", nameTH = "ต้นไม้หน้าร้าน", nameEN = "Potted plants", descTH = "ร้านดูร่มรื่น", descEN = "A greener shopfront", price = 300, beauty = 1 },
        new Item { id = "bunting", nameTH = "ธงราวสีสด", nameEN = "Colourful bunting", descTH = "หน้าร้านดูคึกคัก", descEN = "A festive shopfront", price = 250, beauty = 1 },
        new Item { id = "lights", nameTH = "ไฟปิงปองหน้าร้าน", nameEN = "Fairy lights", descTH = "สวยตอนกลางคืน", descEN = "Pretty at night", price = 400, beauty = 1 },
        new Item { id = "cat", nameTH = "แมวกวักนำโชค", nameEN = "Lucky cat", descTH = "ทิปเพิ่มนิดหน่อย", descEN = "Slightly better tips", price = 350, beauty = 1 },
        new Item { id = "mural", nameTH = "ภาพวาดผนังร้าน", nameEN = "Wall mural", descTH = "ร้านมีสไตล์", descEN = "A shop with style", price = 600, beauty = 2 },
        new Item { id = "bench", nameTH = "ม้านั่งรอหน้าร้าน", nameEN = "Waiting bench", descTH = "ลูกค้าใจเย็นขึ้น", descEN = "Customers wait longer", price = 450, beauty = 1 },
        new Item { id = "helper", kind = Kind.Helper, nameTH = "จ้างพนักงาน", nameEN = "Hire a helper",
                   descTH = "ต้มไข่มุก + สั่งของให้อัตโนมัติ · ค่าแรง 120 ฿/วัน", descEN = "Cooks pearls + reorders stock · wage 120 ฿/day", price = 1500 },
        new Item { id = "expand", kind = Kind.Expansion, nameTH = "ขยายร้านไปตึกข้างๆ", nameEN = "Expand next door",
                   descTH = "มีที่นั่งเพิ่ม · ลูกค้า +30% · ต่อคิวได้ยาวขึ้น", descEN = "More seats · +30% customers · longer queue", price = 5000, beauty = 2 },
    };

    void Awake()
    {
        Instance = this;
        ApplyAll();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public Item Get(string id) => items.Find(i => i.id == id);
    public bool Has(string id) { Item i = Get(id); return i != null && i.owned; }

    public int Beauty
    {
        get { int b = 0; foreach (Item i in items) if (i.owned) b += i.beauty; return b; }
    }

    // effects other systems read
    public float SpawnMultiplier => (1f + 0.03f * Beauty) * (Has("expand") ? 1.3f : 1f);
    public float PatienceMultiplier => (1f + 0.02f * Beauty) * (Has("bench") ? 1.08f : 1f) * (Has("expand") ? 1.15f : 1f);
    public float TipMultiplier => Has("cat") ? 1.05f : 1f;
    public int ExtraQueue => Has("expand") ? 2 : 0;

    public bool Buy(string id)
    {
        Item it = Get(id);
        if (it == null || it.owned) return false;
        GameManager gm = GameManager.Instance;
        if (gm != null && !gm.TrySpend(it.price))
        {
            GameAudio.Play(Sfx.Error);
            ShopHUD.ToastNow(Loc.T($"เงินไม่พอซื้อ{it.nameTH} ({it.price} ฿)", $"Not enough money for {it.nameEN} ({it.price} ฿)"));
            return false;
        }
        if (ShopProgress.Instance != null) ShopProgress.Instance.RegisterExpense(it.price);
        it.owned = true;
        Apply(it);
        GameAudio.Play(it.kind == Kind.Decor ? Sfx.Confirm : Sfx.LevelUp);
        ShopHUD.ToastNow(Loc.T($"{it.nameTH} เรียบร้อย! {it.descTH}", $"{it.nameEN} done! {it.descEN}"), 2.2f);
        OnChanged?.Invoke();
        return true;
    }

    void Apply(Item it)
    {
        if (it.show != null) it.show.SetActive(it.owned);
        if (it.hide != null) it.hide.SetActive(!it.owned);
    }

    void ApplyAll() { foreach (Item it in items) Apply(it); }

    // end of day: the helper gets paid (called by ShopProgress before the summary)
    public void PayWages()
    {
        if (!Has("helper")) return;
        GameManager gm = GameManager.Instance;
        if (gm != null) gm.money -= helperWage;
        if (ShopProgress.Instance != null) ShopProgress.Instance.RegisterExpense(helperWage);
    }

    // ---------------------------------------------------------------- save / load
    public string[] OwnedIds()
    {
        var l = new List<string>();
        foreach (Item it in items) if (it.owned) l.Add(it.id);
        return l.ToArray();
    }

    public void LoadOwned(string[] ids)
    {
        foreach (Item it in items) it.owned = ids != null && Array.IndexOf(ids, it.id) >= 0;
        ApplyAll();
        OnChanged?.Invoke();
    }
}
