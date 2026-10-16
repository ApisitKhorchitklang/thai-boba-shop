using System.Collections.Generic;
using UnityEngine;

// Thai bubble-tea menu. Drink ids match the child objects of the Blender models:
// Cup_Modular/Liquid_<id>, SM_TeaUrnSet/Urn_<id>, SM_BackCounter/Tin_<id>.
public static class DrinkMenu
{
    public class Drink
    {
        public readonly string id;
        public readonly string nameTH;
        public readonly string nameEN;
        public readonly int price;
        public readonly int unlockLevel;
        public readonly Color color;
        public string Name => Loc.T(nameTH, nameEN);

        public Drink(string id, string nameTH, string nameEN, int price, int unlockLevel, Color color)
        {
            this.id = id;
            this.nameTH = nameTH;
            this.nameEN = nameEN;
            this.price = price;
            this.unlockLevel = unlockLevel;
            this.color = color;
        }
    }

    public static readonly Drink[] All =
    {
        new Drink("ThaiTea",  "ชาไทย",        "Thai Tea",      45, 1, new Color(0.93f, 0.45f, 0.10f)),
        new Drink("MilkTea",  "ชานม",         "Milk Tea",      40, 1, new Color(0.80f, 0.62f, 0.45f)),
        new Drink("Matcha",   "ชาเขียว",       "Green Tea",     50, 2, new Color(0.42f, 0.62f, 0.22f)),
        new Drink("PinkMilk", "นมชมพู",        "Pink Milk",     35, 3, new Color(0.98f, 0.55f, 0.70f)),
        new Drink("Taro",     "เผือก",         "Taro",          50, 4, new Color(0.62f, 0.48f, 0.80f)),
        new Drink("Cocoa",    "โกโก้",         "Cocoa",         45, 5, new Color(0.33f, 0.18f, 0.10f)),
        new Drink("RedLime",  "แดงมะนาวโซดา",  "Red Lime Soda", 40, 6, new Color(0.85f, 0.08f, 0.15f)),
        // secret recipe: shared by a regular at friendship 5/5 (RegularsSystem), never unlocked by level
        new Drink("ButterflyPea", "อัญชันมะนาว", "Butterfly Pea Lime", 55, 99, new Color(0.33f, 0.36f, 0.92f)),
    };

    // ระดับความหวาน (sweetness %) — Thai customers are picky about this!
    public static readonly int[] SweetLevels = { 0, 25, 50, 100, 120 };

    public static Drink Get(string id)
    {
        foreach (Drink d in All)
            if (d.id == id) return d;
        return null;
    }

    public static bool IsSecret(Drink d) => d != null && d.id == RegularsSystem.SecretDrink;

    public static bool IsUnlocked(Drink d, int shopLevel)
    {
        if (d == null) return false;
        return IsSecret(d) ? RegularsSystem.SecretUnlocked : d.unlockLevel <= shopLevel;
    }

    public static List<Drink> Unlocked(int shopLevel)
    {
        List<Drink> list = new List<Drink>();
        foreach (Drink d in All)
            if (IsUnlocked(d, shopLevel)) list.Add(d);
        return list;
    }

    public static int NextSweetness(int current)
    {
        for (int i = 0; i < SweetLevels.Length; i++)
            if (SweetLevels[i] == current)
                return SweetLevels[(i + 1) % SweetLevels.Length];
        return SweetLevels[0];
    }

    public static string SweetTH(int sweetness)
    {
        switch (sweetness)
        {
            case 0: return Loc.T("ไม่หวาน", "No sugar");
            case 25: return Loc.T("หวานน้อย", "Less sweet");
            case 50: return Loc.T("หวานกลาง", "Half sweet");
            case 100: return Loc.T("หวานปกติ", "Normal sweet");
            default: return Loc.T("หวานเจี๊ยบ", "Extra sweet");
        }
    }
}
