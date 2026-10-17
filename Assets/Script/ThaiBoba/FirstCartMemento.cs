using UnityEngine;

// The old cart at the mouth of the soi: a reminder of where it all started
public class FirstCartMemento : MonoBehaviour, IInteractable
{
    public string Prompt(RaycastAim player) => Loc.T("รถเข็นคันแรกของร้าน", "The shop's very first cart");

    public void Interact(RaycastAim player)
    {
        ShopHUD.ToastNow(Loc.T("รถเข็นคันนี้ขายชานมแก้วละ 25 บาทอยู่ปากซอยมาสามปี… ก่อนจะมีร้านวันนี้", "This cart sold 25-baht milk teas at the corner for three years… before the shop existed"), 3f);
    }
}
