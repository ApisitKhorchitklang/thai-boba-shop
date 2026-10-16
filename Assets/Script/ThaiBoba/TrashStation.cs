using UnityEngine;

// SM_TrashBin: throw away a wrong cup and start over.
public class TrashStation : MonoBehaviour, IInteractable
{
    public string Prompt(RaycastAim player)
    {
        return player.HeldCup != null ? Loc.T("ทิ้งแก้วนี้", "Throw this cup away") : Loc.T("ถังขยะ", "Trash bin");
    }

    public void Interact(RaycastAim player)
    {
        CupController cup = player.HeldCup;
        if (cup == null) return;
        Destroy(cup.gameObject);
        GameAudio.Play(Sfx.Trash);
        ShopHUD.ToastNow(Loc.T("ทิ้งแก้วแล้ว เริ่มใหม่ได้เลย", "Cup thrown away, start again"));
    }
}
