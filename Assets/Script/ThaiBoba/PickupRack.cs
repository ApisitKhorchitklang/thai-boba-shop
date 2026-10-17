using UnityEngine;

// The pick-up rack on the counter: look at it with a sealed cup and press use
public class PickupRack : MonoBehaviour, IInteractable
{
    public string Prompt(RaycastAim player)
    {
        DeliverySystem d = DeliverySystem.Instance;
        if (d == null || !d.Enabled) return Loc.T("ชั้นรับออเดอร์เดลิเวอรี่ (ซื้ออัปเกรดเดลิเวอรี่ก่อน)", "Delivery pick-up rack (buy the delivery upgrade first)");
        if (d.Current == null) return Loc.T("ชั้นรับออเดอร์ · รอออเดอร์จากแอป", "Pick-up rack · waiting for app orders");
        return player.HeldCup != null ? Loc.T($"วางแก้วสำหรับไรเดอร์ ({d.Current.Done}/{d.Current.items.Count})", $"Put the drink out for the rider ({d.Current.Done}/{d.Current.items.Count})")
                                      : Loc.T($"ออเดอร์แอป #{d.Current.number}: วางแล้ว {d.Current.Done}/{d.Current.items.Count}", $"App order #{d.Current.number}: {d.Current.Done}/{d.Current.items.Count} ready");
    }

    public void Interact(RaycastAim player)
    {
        if (DeliverySystem.Instance != null) DeliverySystem.Instance.PlaceCup(player);
    }
}
