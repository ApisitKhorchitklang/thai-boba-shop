using UnityEngine;

public class ComputerTerminal : MonoBehaviour
{
    public float defaultDeliveryDelay = 5f;

    // Order supplies while the day is active. Costs money (InventoryManager prices).
    public void OrderSupply(string resource, int amount)
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("No InventoryManager present to process order.");
            return;
        }

        int cost = InventoryManager.Instance.PricePerUnit(resource) * amount;
        if (GameManager.Instance != null && cost > 0)
        {
            if (!GameManager.Instance.TrySpend(cost))
            {
                ShopHUD.ToastNow(Loc.T($"เงินไม่พอ! ต้องใช้ {cost} ฿", $"Not enough money! Need {cost} ฿"));
                return;
            }
            if (ShopProgress.Instance != null)
                ShopProgress.Instance.RegisterExpense(cost);
        }

        InventoryManager.Instance.OrderResource(resource, amount, defaultDeliveryDelay);
        ShopHUD.ToastNow(Loc.T($"สั่ง {resource} x{amount} แล้ว (-{cost} ฿) รอส่ง {defaultDeliveryDelay:0} วิ", $"Ordered {resource} x{amount} (-{cost} ฿), arriving in {defaultDeliveryDelay:0}s"));
    }

    // UnityEvent-friendly helpers (Button.onClick only supports one argument)
    public void OrderCups10() { OrderSupply("cups", 10); }
    public void OrderBoba50() { OrderSupply("boba", 50); }
    public void OrderIce50() { OrderSupply("ice", 50); }
    public void OrderSyrup50() { OrderSupply("syrup", 50); }

    public void BuyUpgrade(string upgradeId)
    {
        if (UpgradeShop.Instance != null)
            UpgradeShop.Instance.Buy(upgradeId);
    }

    // Called when player ends the day via computer
    public void EndDayAndSave()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndDay();
            return;
        }

        SaveSystem.SaveGame();
        TimeCyc tc = FindFirstObjectByType<TimeCyc>();
        if (tc != null)
            tc.AdvanceToNextDay();

        Debug.Log("Day ended and saved via ComputerTerminal.");
    }
}
