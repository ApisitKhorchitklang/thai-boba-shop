using UnityEngine;

[System.Serializable]
public class Order
{
    public int bobaCount;
    public bool wantsIce;
    public string flavor;
    public int sweetness = 100; // percent: 0 / 25 / 50 / 100 / 120
    public bool wantsFoam;      // cheese foam topping (+10 baht), from the fridge
    public bool blended;        // hot season: frappe from the blender (+10 baht, needs ice)
    public bool marshmallow;    // cool season: marshmallows on a hot drink (+10 baht)
    public string topping = ""; // topping bar: Konjac / Jelly / Pudding ("" = none)
    public bool vegetarian;     // vegetarian festival (เทศกาลกินเจ): no cheese foam, no pudding

    public Order() { }

    public Order(int boba, bool ice, string flavor)
    {
        this.bobaCount = boba;
        this.wantsIce = ice;
        this.flavor = flavor;
    }

    public Order(int boba, bool ice, string flavor, int sweetness) : this(boba, ice, flavor)
    {
        this.sweetness = sweetness;
    }

    // Menu price + 5 baht for pearls (ไข่มุก +5)
    public int Price
    {
        get
        {
            DrinkMenu.Drink d = DrinkMenu.Get(flavor);
            int basePrice = d != null ? d.price : 40;
            DrinkMenu.Topping t = DrinkMenu.GetTopping(topping);
            return basePrice + (bobaCount > 0 ? 5 : 0) + (wantsFoam ? 10 : 0) + (blended ? 10 : 0) + (marshmallow ? 10 : 0) + (t != null ? t.price : 0);
        }
    }

    public string DrinkNameTH
    {
        get
        {
            DrinkMenu.Drink d = DrinkMenu.Get(flavor);
            return d != null ? d.Name : flavor;
        }
    }

    public string ToThaiString()
    {
        string boba = bobaCount == 0 ? Loc.T("ไม่ใส่ไข่มุก", "no pearls") : Loc.T($"ไข่มุก {bobaCount} ช้อน", $"{bobaCount} scoop{(bobaCount > 1 ? "s" : "")} of pearls");
        string ice = wantsIce ? Loc.T("ใส่น้ำแข็ง", "iced") : Loc.T("ร้อน (ไม่ใส่น้ำแข็ง)", "hot (no ice)");
        string foam = (wantsFoam ? Loc.T(" · ท็อปชีสโฟม", " · cheese foam") : "") + (blended ? Loc.T(" · ปั่น", " · blended") : "") +
                      (marshmallow ? Loc.T(" · มาร์ชเมลโลว์", " · marshmallows") : "");
        DrinkMenu.Topping t = DrinkMenu.GetTopping(topping);
        if (t != null) foam += " · +" + t.Name;
        string jay = vegetarian ? Loc.T(" <color=#D4A017>(เจ)</color>", " <color=#D4A017>(vegetarian)</color>") : "";
        return $"{DrinkNameTH} {DrinkMenu.SweetTH(sweetness)}{jay}\n{boba} · {ice}{foam}";
    }

    public override string ToString()
    {
        return $"Order: {bobaCount} boba, ice:{wantsIce}, flavor:{flavor}, sweet:{sweetness}%, foam:{wantsFoam}, topping:{topping}";
    }
}
