using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Saves the shop: one "latest" file (Continue) plus one file per day (saves/day_###.json) so any earlier
// day can be loaded from the menu. GameManager.EndDay() auto-saves every evening.
public static class SaveSystem
{
    private static string saveFile => Path.Combine(Application.persistentDataPath, "bubble_tea_save.json");
    public static string SlotFolder => Path.Combine(Application.persistentDataPath, "saves");
    public const int MaxDaySlots = 30;

    [System.Serializable]
    public class SaveData
    {
        public int cups;
        public int boba;
        public int ice;
        public int baseSyrup;
        public int money;
        public int day;
        // Thai boba shop progression
        public float reputation = 3f;
        public int xp;
        public int level = 1;
        public int bestCombo;
        public string[] upgrades = new string[0];
        public string savedAt = "";
    }

    public class SaveInfo
    {
        public string path;
        public SaveData data;
    }

    public static bool HasSave => File.Exists(saveFile);

    static SaveData Capture()
    {
        SaveData data = new SaveData();
        if (InventoryManager.Instance != null)
        {
            data.cups = InventoryManager.Instance.cups;
            data.boba = InventoryManager.Instance.boba;
            data.ice = InventoryManager.Instance.ice;
            data.baseSyrup = InventoryManager.Instance.baseSyrup;
        }
        if (GameManager.Instance != null)
            data.money = GameManager.Instance.money;

        TimeCyc tc = Object.FindFirstObjectByType<TimeCyc>();
        data.day = tc != null ? tc.Days : 1;

        ShopProgress sp = ShopProgress.Instance;
        if (sp != null)
        {
            data.reputation = sp.reputation;
            data.xp = sp.xp;
            data.level = sp.level;
            data.bestCombo = sp.bestCombo;
        }
        if (UpgradeShop.Instance != null)
            data.upgrades = UpgradeShop.Instance.OwnedIds();
        data.savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        return data;
    }

    public static void SaveGame()
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("No InventoryManager to save.");
            return;
        }

        SaveData data = Capture();
        string json = JsonUtility.ToJson(data, true);
        try
        {
            File.WriteAllText(saveFile, json);
            Directory.CreateDirectory(SlotFolder);
            File.WriteAllText(Path.Combine(SlotFolder, $"day_{data.day:000}.json"), json);
            PruneSlots();
            Debug.Log($"Game saved (day {data.day}) to {saveFile}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to save game: " + ex.Message);
        }
    }

    static void PruneSlots()
    {
        List<SaveInfo> all = ListDaySaves();
        for (int i = MaxDaySlots; i < all.Count; i++)
            File.Delete(all[i].path);
    }

    // Newest day first
    public static List<SaveInfo> ListDaySaves()
    {
        List<SaveInfo> list = new List<SaveInfo>();
        if (!Directory.Exists(SlotFolder)) return list;
        foreach (string f in Directory.GetFiles(SlotFolder, "day_*.json"))
        {
            try
            {
                SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(f));
                if (d != null) list.Add(new SaveInfo { path = f, data = d });
            }
            catch (System.Exception) { }
        }
        list.Sort((a, b) => b.data.day.CompareTo(a.data.day));
        return list;
    }

    public static SaveData PeekLatest()
    {
        if (!HasSave) return null;
        try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(saveFile)); }
        catch (System.Exception) { return null; }
    }

    public static void LoadGame()
    {
        LoadGame(saveFile);
    }

    public static void LoadGame(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data != null) Apply(data);
            Debug.Log("Game loaded from " + path);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to load save: " + ex.Message);
        }
    }

    static void Apply(SaveData data)
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.cups = data.cups;
            InventoryManager.Instance.boba = data.boba;
            InventoryManager.Instance.ice = data.ice;
            InventoryManager.Instance.baseSyrup = data.baseSyrup;
        }

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.money = 0;
            gm.AddMoney(data.money);
        }

        ShopProgress sp = ShopProgress.Instance;
        if (sp != null)
            sp.LoadFrom(data.reputation, data.xp, data.level, data.bestCombo);

        if (UpgradeShop.Instance != null)
            UpgradeShop.Instance.LoadOwned(data.upgrades);

        TimeCyc tc = Object.FindFirstObjectByType<TimeCyc>();
        if (tc != null && data.day > 0)
            tc.SetDay(data.day);
    }

    // Fresh shop: default stock, starting cash, 3 stars, day 1. Old saves are removed.
    public static void StartNewGame(int startingMoney)
    {
        DeleteAllSaves();
        SaveData fresh = new SaveData
        {
            cups = 50, boba = 200, ice = 500, baseSyrup = 300, money = startingMoney, day = 1,
            reputation = 3f, xp = 0, level = 1, bestCombo = 0, upgrades = new string[0]
        };
        Apply(fresh);
    }

    public static void DeleteAllSaves()
    {
        if (File.Exists(saveFile)) File.Delete(saveFile);
        if (Directory.Exists(SlotFolder))
            foreach (string f in Directory.GetFiles(SlotFolder, "day_*.json")) File.Delete(f);
    }
}
