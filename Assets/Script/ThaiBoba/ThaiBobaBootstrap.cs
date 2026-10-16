using System.Collections;
using UnityEngine;

// Loads the save one frame after start, once every system has subscribed to TimeCyc events.
public class ThaiBobaBootstrap : MonoBehaviour
{
    public bool loadSaveOnStart = true;
    [Tooltip("Starting cash for a brand-new game")]
    public int startingMoney = 100;

    IEnumerator Start()
    {
        yield return null;
        if (GameMenus.Instance != null) yield break;   // the main menu decides (Continue / New / Load)
        if (loadSaveOnStart && SaveSystem.HasSave)
            SaveSystem.LoadGame();
        else if (GameManager.Instance != null && GameManager.Instance.money == 0)
            GameManager.Instance.AddMoney(startingMoney);
    }
}
