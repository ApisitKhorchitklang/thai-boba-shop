using UnityEngine;

// The hired helper (DecorShop "helper"): stands at the back counter and keeps the shop running —
// cooks a new pot of pearls when it is empty or has gone hard, and reorders supplies that run low.
public class HelperStaff : MonoBehaviour
{
    public float checkEvery = 3f;
    float timer;
    TimeCyc clock;

    void Start() { clock = FindFirstObjectByType<TimeCyc>(); }

    void Update()
    {
        if (!GameMenus.GameRunning || DecorShop.Instance == null || !DecorShop.Instance.Has("helper")) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = checkEvery;
        int h = clock != null ? clock.Hours : 12;
        if (h < 7 || h >= 21) return;                                  // works shop hours only

        BobaPot pot = BobaPot.Instance;
        if (pot != null)
        {
            if (pot.Current == BobaPot.State.Stale) pot.Discard();
            if (pot.Current == BobaPot.State.Empty && InventoryManager.Instance != null && InventoryManager.Instance.boba > 0)
            {
                pot.StartCooking();
                ShopHUD.Toast(Loc.T("พนักงาน: ต้มไข่มุกให้แล้วนะ", "Helper: I put a pot of pearls on"));
            }
        }

        InventoryManager inv = InventoryManager.Instance;
        ComputerTerminal terminal = FindFirstObjectByType<ComputerTerminal>();
        if (inv == null || terminal == null) return;
        string res = inv.cups < 10 ? "cups" : inv.boba < 40 ? "boba" : inv.ice < 100 ? "ice" : inv.baseSyrup < 60 ? "syrup" : inv.foam < 8 ? "foam" : null;
        if (res == null) return;
        terminal.OrderSupply(res, res == "cups" || res == "foam" ? 10 : 50);
        timer = 8f;                                                    // wait for the delivery before checking again
    }
}
