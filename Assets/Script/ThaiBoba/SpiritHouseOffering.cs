using UnityEngine;

// ศาลพระภูมิ: offer red drink + garland once a day for a tip blessing and lucky customers.
public class SpiritHouseOffering : MonoBehaviour, IInteractable
{
    public static SpiritHouseOffering Instance { get; private set; }

    public int offeringCost = 20;
    [Tooltip("Tip multiplier bonus while blessed (0.2 = +20%)")]
    public float tipBonus = 0.2f;
    [Tooltip("Chance per served customer of a lucky extra payment")]
    [Range(0f, 1f)] public float luckyChance = 0.1f;
    public int luckyBonus = 20;
    [Tooltip("Shown only after making the offering. Auto-finds the SpiritHouse_Offerings child if empty.")]
    public GameObject offeringsVisual;
    public AudioClip blessSfx;

    public bool BlessedToday { get; private set; }
    public float TipMultiplier => BlessedToday ? 1f + tipBonus : 1f;

    private TimeCyc timeCyc;

    void Awake()
    {
        Instance = this;
        if (offeringsVisual == null)
        {
            Transform t = FindChild(transform, "SpiritHouse_Offerings");
            if (t != null) offeringsVisual = t.gameObject;
        }
        SetVisual(false);
    }

    void Start()
    {
        timeCyc = FindFirstObjectByType<TimeCyc>();
        if (timeCyc != null) timeCyc.OnNewDay += ResetDay;
    }

    void OnDestroy()
    {
        if (timeCyc != null) timeCyc.OnNewDay -= ResetDay;
        if (Instance == this) Instance = null;
    }

    void ResetDay(int day)
    {
        BlessedToday = false;
        SetVisual(false);
    }

    public bool RollLucky()
    {
        return BlessedToday && Random.value < luckyChance;
    }

    public string Prompt(RaycastAim player)
    {
        return BlessedToday ? Loc.T("ไหว้เจ้าที่แล้ววันนี้", "Already paid respects today") : Loc.T($"ไหว้เจ้าที่ (น้ำแดง+พวงมาลัย {offeringCost} ฿)", $"Make an offering at the spirit house (red soda + garland {offeringCost} ฿)");
    }

    public void Interact(RaycastAim player)
    {
        if (BlessedToday)
        {
            ShopHUD.ToastNow(Loc.T("วันนี้ไหว้แล้ว พรุ่งนี้มาใหม่นะ", "You already made an offering today, come back tomorrow"));
            return;
        }
        if (GameManager.Instance != null && !GameManager.Instance.TrySpend(offeringCost))
        {
            ShopHUD.ToastNow(Loc.T("เงินไม่พอซื้อน้ำแดงกับพวงมาลัย", "Not enough money for the red soda and garland"));
            return;
        }
        if (ShopProgress.Instance != null)
            ShopProgress.Instance.RegisterExpense(offeringCost);

        BlessedToday = true;
        GameAudio.Play(Sfx.Bless);
        SetVisual(true);
        if (blessSfx != null) AudioSource.PlayClipAtPoint(blessSfx, transform.position);
        ShopHUD.ToastNow(Loc.T($"สาธุ! เจ้าที่อวยพร ทิป +{Mathf.RoundToInt(tipBonus * 100)}% และมีลุ้นลูกค้าใจดีทั้งวัน", $"Sathu! The spirits bless you: tips +{Mathf.RoundToInt(tipBonus * 100)}% and generous customers all day"));
    }

    void SetVisual(bool on)
    {
        if (offeringsVisual != null) offeringsVisual.SetActive(on);
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
