using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int money = 0;

    public event System.Action<int> OnMoneyChanged;

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

    public void AddMoney(int amount)
    {
        money += amount;
        OnMoneyChanged?.Invoke(money);
        Debug.Log($"Added money: {amount}. Total: {money}");
    }

    // Spend money if the player can afford it
    public bool TrySpend(int amount)
    {
        if (amount > money)
            return false;

        money -= amount;
        OnMoneyChanged?.Invoke(money);
        return true;
    }

    // End day workflow called by UI/computer
    public void EndDay()
    {
        if (ShopProgress.Instance != null)
            ShopProgress.Instance.FinalizeDay();

        TimeCyc tc = FindFirstObjectByType<TimeCyc>();
        if (tc != null)
            tc.AdvanceToNextDay();

        // Save after advancing so loading resumes on the new morning
        SaveSystem.SaveGame();

        Debug.Log("Day ended via GameManager.");
    }
}
