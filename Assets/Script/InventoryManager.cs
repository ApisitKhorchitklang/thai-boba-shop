using System.Collections;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Resources")]
    public int cups = 50;
    public int boba = 200;
    public int ice = 500;
    public int baseSyrup = 300;
    public int foam = 40;           // cheese foam portions (kept in the fridge)

    [Header("Supply prices (baht per unit)")]
    public int cupPrice = 2;
    public int bobaPrice = 1;
    public int icePrice = 1;
    public int syrupPrice = 1;
    public int foamPrice = 3;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Try to consume resources for a made drink. Returns true if enough resources.
    public bool ConsumeForOrder(int neededCups, int neededBoba, int neededIce, int neededSyrup)
    {
        if (cups < neededCups || boba < neededBoba || ice < neededIce || baseSyrup < neededSyrup)
            return false;

        cups -= neededCups;
        boba -= neededBoba;
        ice -= neededIce;
        baseSyrup -= neededSyrup;
        return true;
    }

    public int Get(string resource)
    {
        switch (resource.ToLower())
        {
            case "cups": return cups;
            case "boba": return boba;
            case "ice": return ice;
            case "syrup": return baseSyrup;
            case "foam": return foam;
            default: return 0;
        }
    }

    // Consume a single resource type (used by the drink stations)
    public bool TryUse(string resource, int amount)
    {
        if (Get(resource) < amount)
            return false;

        Add(resource, -amount);
        return true;
    }

    public int PricePerUnit(string resource)
    {
        switch (resource.ToLower())
        {
            case "cups": return cupPrice;
            case "boba": return bobaPrice;
            case "ice": return icePrice;
            case "syrup": return syrupPrice;
            case "foam": return foamPrice;
            default: return 0;
        }
    }

    public void Add(string resource, int amount)
    {
        switch (resource.ToLower())
        {
            case "cups": cups += amount; break;
            case "boba": boba += amount; break;
            case "ice": ice += amount; break;
            case "syrup": baseSyrup += amount; break;
            case "foam": foam += amount; break;
            default: break;
        }
    }

    // Place an order to restock resources; delivery simulated after delaySeconds
    public void OrderResource(string resource, int amount, float delaySeconds = 5f)
    {
        StartCoroutine(DelayedDelivery(resource, amount, delaySeconds));
    }

    IEnumerator DelayedDelivery(string resource, int amount, float delay)
    {
        yield return new WaitForSeconds(delay);
        Add(resource, amount);
        ShopHUD.Toast(Loc.T($"ของมาส่งแล้ว: {resource} +{amount}", $"Delivered: {resource} +{amount}"));
    }
}
