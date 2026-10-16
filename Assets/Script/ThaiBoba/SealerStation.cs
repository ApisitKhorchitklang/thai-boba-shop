using System.Collections;
using UnityEngine;

// SM_CupSealer: seals a filled cup after a short delay (faster with the turbo upgrade).
public class SealerStation : MonoBehaviour, IInteractable
{
    public float sealSeconds = 1.5f;
    [Tooltip("Optional lever that dips while sealing")]
    public Transform lever;
    public AudioClip sealSfx;

    private bool busy;

    public string Prompt(RaycastAim player)
    {
        if (busy) return Loc.T("กำลังซีล...", "Sealing...");
        CupController c = player.HeldCup;
        return c != null && c.CanSeal ? Loc.T("ซีลฝาแก้ว", "Seal the cup") : Loc.T("เครื่องซีล (เทชาก่อน)", "Sealer (pour the tea first)");
    }

    public void Interact(RaycastAim player)
    {
        if (busy) return;
        CupController cup = player.HeldCup;
        if (cup == null) { ShopHUD.ToastNow(Loc.T("ถือแก้วก่อนนะ", "Hold a cup first")); return; }
        if (!cup.CanSeal) { ShopHUD.ToastNow(Loc.T("ต้องเทชาก่อนซีล", "Pour the tea before sealing")); return; }
        StartCoroutine(Seal(cup));
    }

    IEnumerator Seal(CupController cup)
    {
        busy = true;
        GameAudio.Play(Sfx.SealStart);
        if (sealSfx != null)
            AudioSource.PlayClipAtPoint(sealSfx, transform.position);

        float t = sealSeconds * (UpgradeShop.Instance != null ? UpgradeShop.Instance.SealTimeMultiplier : 1f);
        Quaternion start = lever != null ? lever.localRotation : Quaternion.identity;
        for (float e = 0; e < t; e += Time.deltaTime)
        {
            if (lever != null)
                lever.localRotation = start * Quaternion.Euler(Mathf.Sin(e / t * Mathf.PI) * 40f, 0, 0);
            yield return null;
        }
        if (lever != null) lever.localRotation = start;

        if (cup != null && cup.CloseLid())
        {
            ShopHUD.ToastNow(Loc.T("ซีลเรียบร้อย พร้อมเสิร์ฟ!", "Sealed, ready to serve!"));
            GameAudio.Play(Sfx.SealDone);
        }
        busy = false;
    }
}
