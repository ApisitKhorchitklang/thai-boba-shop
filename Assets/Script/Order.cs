using UnityEngine;

[System.Serializable]
public class Order
{
    public int bobaCount;
    public bool wantsIce;
    public string flavor;
    public int sweetness = 100; // percent: 0 / 25 / 50 / 100 / 120
    public bool wantsFoam;      // cheese foam topping (+10 baht), from the fridge

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
            return basePrice + (bobaCount > 0 ? 5 : 0) + (wantsFoam ? 10 : 0);
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
        string foam = wantsFoam ? Loc.T(" · ท็อปชีสโฟม", " · cheese foam") : "";
        return $"{DrinkNameTH} {DrinkMenu.SweetTH(sweetness)}\n{boba} · {ice}{foam}";
    }

    public override string ToString()
    {
        return $"Order: {bobaCount} boba, ice:{wantsIce}, flavor:{flavor}, sweet:{sweetness}%, foam:{wantsFoam}";
    }
}
