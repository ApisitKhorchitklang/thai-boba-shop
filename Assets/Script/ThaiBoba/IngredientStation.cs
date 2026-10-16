using UnityEngine;

// Pearls / ice / tea / sweetness. Put on SM_BobaPot, SM_IceCooler, each Urn_<id> / Tin_<id>, SM_SyrupBottles.
public class IngredientStation : MonoBehaviour, IInteractable
{
    public enum Kind { Boba, Ice, Flavor, Sweetness }

    public Kind kind = Kind.Flavor;
    [Tooltip("Drink id for Flavor stations (ThaiTea, MilkTea, Matcha, PinkMilk, Taro, Cocoa, RedLime)")]
    public string flavorId = "ThaiTea";
    public AudioClip useSfx;

    public string Prompt(RaycastAim player)
    {
        switch (kind)
        {
            case Kind.Boba: return Loc.T("ตักไข่มุก (+1 ช้อน)", "Scoop pearls (+1 scoop)");
            case Kind.Ice: return Loc.T("ใส่น้ำแข็ง", "Add ice");
            case Kind.Sweetness:
                CupController c = player.HeldCup;
                return c != null ? Loc.T($"ปรับความหวาน (ตอนนี้ {c.sweetness}%)", $"Change sweetness (now {c.sweetness}%)") : Loc.T("ปรับความหวาน", "Change sweetness");
            default:
                DrinkMenu.Drink d = DrinkMenu.Get(flavorId);
                bool locked = d != null && ShopProgress.Instance != null && d.unlockLevel > ShopProgress.Instance.level;
                return locked ? Loc.T($"{d.Name} (ปลดล็อกที่เลเวล {d.unlockLevel})", $"{d.Name} (unlocks at level {d.unlockLevel})") : Loc.T("เทชา: ", "Pour: ") + (d != null ? d.Name : flavorId);
        }
    }

    public void Interact(RaycastAim player)
    {
        CupController cup = player.HeldCup;
        if (cup == null)
        {
            ShopHUD.Toast(Loc.T("หยิบแก้วจากที่วางแก้วก่อนนะ", "Take a cup from the cup stack first"));
            return;
        }

        InventoryManager inv = InventoryManager.Instance;
        switch (kind)
        {
            case Kind.Boba:
                if (!cup.CanAddBoba) { ShopHUD.ToastNow(cup.State == CupController.CupState.LidClosed ? Loc.T("ซีลแล้ว ใส่เพิ่มไม่ได้", "Already sealed, can't add more") : Loc.T("ไข่มุกเต็มแล้ว (สูงสุด 3 ช้อน)", "Pearls are full (max 3 scoops)")); return; }
                if (inv != null && !inv.TryUse("boba", 1)) { ShopHUD.Toast(Loc.T("ไข่มุกหมด! สั่งเพิ่มที่คอมพิวเตอร์", "Out of pearls! Order more at the computer")); return; }
                cup.AddBoba(1);
                GameAudio.Play(Sfx.Boba);
                ShopHUD.ToastNow(Loc.T($"ตักไข่มุก <size=130%><b>{cup.bobaCount}</b></size> ช้อน  <size=80%>(สูงสุด {cup.maxBoba})</size>", $"Pearls: <size=130%><b>{cup.bobaCount}</b></size> scoop{(cup.bobaCount > 1 ? "s" : "")}  <size=80%>(max {cup.maxBoba})</size>"));
                break;

            case Kind.Ice:
                if (!cup.CanAddIce) { ShopHUD.Toast(cup.hasIce ? Loc.T("ใส่น้ำแข็งไปแล้ว", "Already has ice") : Loc.T("ซีลแล้ว ใส่เพิ่มไม่ได้", "Already sealed, can't add more")); return; }
                if (inv != null && !inv.TryUse("ice", 1)) { ShopHUD.Toast(Loc.T("น้ำแข็งหมด!", "Out of ice!")); return; }
                cup.AddIce();
                GameAudio.Play(Sfx.Ice);
                break;

            case Kind.Flavor:
                DrinkMenu.Drink d = DrinkMenu.Get(flavorId);
                if (d != null && ShopProgress.Instance != null && d.unlockLevel > ShopProgress.Instance.level)
                {
                    ShopHUD.Toast(Loc.T($"{d.nameTH} ยังไม่ปลดล็อก (เลเวล {d.unlockLevel})", $"{d.Name} is still locked (level {d.unlockLevel})"));
                    return;
                }
                if (!cup.CanFill) { ShopHUD.Toast(Loc.T("แก้วนี้เทชาไปแล้ว", "This cup already has tea")); return; }
                if (inv != null && !inv.TryUse("syrup", 1)) { ShopHUD.Toast(Loc.T("วัตถุดิบหมด!", "Out of ingredients!")); return; }
                cup.Fill(flavorId);
                GameAudio.Play(Sfx.Pour);
                break;

            case Kind.Sweetness:
                if (!cup.SetSweetness(DrinkMenu.NextSweetness(cup.sweetness))) { ShopHUD.Toast(Loc.T("ซีลแล้ว ปรับความหวานไม่ได้", "Already sealed, can't change the sweetness")); return; }
                ShopHUD.Toast(Loc.T($"ความหวาน: {DrinkMenu.SweetTH(cup.sweetness)} ({cup.sweetness}%)", $"Sweetness: {DrinkMenu.SweetTH(cup.sweetness)} ({cup.sweetness}%)"));
                GameAudio.Play(Sfx.Sweet);
                break;
        }

        if (useSfx != null)
            AudioSource.PlayClipAtPoint(useSfx, transform.position);
    }
}
