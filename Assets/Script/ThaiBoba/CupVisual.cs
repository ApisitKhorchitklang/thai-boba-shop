using System.Collections.Generic;
using UnityEngine;

// Shows the contents of Cup_Modular.fbx based on CupController state:
// children Liquid_<flavor>, Cup_Pearls/2/3 (one per scoop), Cup_Ice, Cup_SealFilm, Cup_Straw.
[RequireComponent(typeof(CupController))]
public class CupVisual : MonoBehaviour
{
    private CupController cup;
    private GameObject ice, seal, straw;
    private readonly GameObject[] pearls = new GameObject[3];     // Cup_Pearls, Cup_Pearls2, Cup_Pearls3: one per scoop
    private readonly Dictionary<string, GameObject> liquids = new Dictionary<string, GameObject>();

    void Awake()
    {
        cup = GetComponent<CupController>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n.StartsWith("Liquid_")) liquids[n.Substring(7)] = t.gameObject;
            else if (n == "Cup_Pearls") pearls[0] = t.gameObject;
            else if (n == "Cup_Pearls2") pearls[1] = t.gameObject;
            else if (n == "Cup_Pearls3") pearls[2] = t.gameObject;
            else if (n == "Cup_Ice") ice = t.gameObject;
            else if (n == "Cup_SealFilm") seal = t.gameObject;
            else if (n == "Cup_Straw") straw = t.gameObject;
        }
        cup.OnChanged += Refresh;
        Refresh(cup);
    }

    void OnDestroy()
    {
        if (cup != null) cup.OnChanged -= Refresh;
    }

    void Refresh(CupController c)
    {
        for (int i = 0; i < pearls.Length; i++)
            if (pearls[i] != null) pearls[i].SetActive(c.bobaCount > i);
        if (ice != null) ice.SetActive(c.hasIce);

        bool filled = c.State == CupController.CupState.Filled || c.State == CupController.CupState.LidClosed;
        foreach (KeyValuePair<string, GameObject> kv in liquids)
            kv.Value.SetActive(filled && kv.Key == c.flavor);

        bool sealedCup = c.State == CupController.CupState.LidClosed;
        if (seal != null) seal.SetActive(sealedCup);
        if (straw != null) straw.SetActive(sealedCup);
    }
}
