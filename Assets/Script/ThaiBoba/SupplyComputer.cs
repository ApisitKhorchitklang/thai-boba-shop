using UnityEngine;

// SM_OrderComputer: the shop's PC. Use it to order cups, pearls, ice and tea during the day.
public class SupplyComputer : MonoBehaviour, IInteractable
{
    public string Prompt(RaycastAim player)
    {
        return Loc.T("คอมพิวเตอร์: สั่งของ", "Computer: order supplies");
    }

    public void Interact(RaycastAim player)
    {
        if (OrderPanel.Instance != null) OrderPanel.Instance.Open();
    }
}
