using UnityEngine;

// SM_CupStack: puts a fresh cup in the player's hand.
public class CupDispenser : MonoBehaviour, IInteractable
{
    [Tooltip("Prefab with CupController + CupVisual (Tools > Thai Boba Shop > Create Cup Prefab)")]
    public GameObject cupPrefab;

    public string Prompt(RaycastAim player)
    {
        return player.isEmpty ? Loc.T("หยิบแก้วใหม่", "Take a new cup") : Loc.T("มือไม่ว่าง", "Hands full");
    }

    public void Interact(RaycastAim player)
    {
        if (cupPrefab == null || player.handPos == null) return;
        if (!player.isEmpty) { ShopHUD.ToastNow(Loc.T("วางของในมือก่อน", "Put down what you're holding first")); return; }
        if (InventoryManager.Instance != null && !InventoryManager.Instance.TryUse("cups", 1))
        {
            ShopHUD.ToastNow(Loc.T("แก้วหมด! สั่งเพิ่มที่คอมพิวเตอร์", "Out of cups! Order more at the computer"));
            return;
        }

        GameObject go = Instantiate(cupPrefab, player.handPos.position, player.handPos.rotation);
        CupController cup = go.GetComponent<CupController>();
        if (cup != null) cup.Grab();
        GameAudio.Play(Sfx.Cup);
        ShopHUD.ToastNow(Loc.T("หยิบแก้วใหม่แล้ว", "Took a new cup"));
        player.HoldItem(go.transform);
    }
}
