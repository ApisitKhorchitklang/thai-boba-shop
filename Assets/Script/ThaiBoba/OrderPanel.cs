using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The shop computer's "order supplies" screen. Opened from SupplyComputer; the game keeps running while it is up
// (deliveries take a few seconds). PC: the cursor is freed while it is open; E / Esc close it.
public class OrderPanel : MonoBehaviour
{
    public static OrderPanel Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.panel != null && Instance.panel.activeSelf;

    public GameObject panel;
    public TMP_Text stockText;
    public Button[] orderButtons = new Button[5];     // cups, boba, ice, tea, cheese foam
    public TMP_Text[] orderLabels = new TMP_Text[5];
    public Button closeButton;
    public TMP_Text closeLabel;

    [Header("Tabs: supplies / decorate & expand")]
    public GameObject supplyGroup, decorGroup;
    public Button supplyTab, decorTab;
    public Button[] decorButtons = new Button[8];
    public TMP_Text[] decorLabels = new TMP_Text[8];
    public TMP_Text beautyText;
    bool decorMode;

    static readonly string[] Resources = { "cups", "boba", "ice", "syrup", "foam" };
    static readonly int[] Amounts = { 10, 50, 50, 50, 10 };
    int openedFrame;

    static void SetTabLook(Button b, bool active, string label)
    {
        if (b == null) return;
        b.image.color = active ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t != null && t.text != label) t.text = label;
    }

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
        for (int i = 0; i < orderButtons.Length; i++)
        {
            int k = i;
            if (orderButtons[i] != null) orderButtons[i].onClick.AddListener(() => Order(k));
        }
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (supplyTab != null) supplyTab.onClick.AddListener(() => { decorMode = false; GameAudio.Play(Sfx.Click); Refresh(); });
        if (decorTab != null) decorTab.onClick.AddListener(() => { decorMode = true; GameAudio.Play(Sfx.Click); Refresh(); });
        for (int i = 0; i < decorButtons.Length; i++)
        {
            int k = i;
            if (decorButtons[i] != null) decorButtons[i].onClick.AddListener(() => BuyDecor(k));
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Open()
    {
        if (panel == null || IsOpen) return;
        panel.SetActive(true);
        openedFrame = Time.frameCount;
        GameAudio.Play(Sfx.Open);
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;
        panel.SetActive(false);
        GameAudio.Play(Sfx.Close);
    }

    void BuyDecor(int i)
    {
        DecorShop shop = DecorShop.Instance;
        if (shop == null || i >= shop.items.Count) return;
        shop.Buy(shop.items[i].id);
        Refresh();
    }

    public void ShowDecorTab() { decorMode = true; Refresh(); }      // tests / tutorial

    void Order(int i)
    {
        ComputerTerminal terminal = FindFirstObjectByType<ComputerTerminal>();
        if (terminal == null) return;
        GameAudio.Play(Sfx.Click);
        terminal.OrderSupply(Resources[i], Amounts[i]);
        Refresh();
    }

    void Update()
    {
        if (!IsOpen) return;
        if (Time.frameCount != openedFrame && Input.GetKeyDown(KeyCode.E)) { Close(); return; }
        if (!GameMenus.GameRunning || ShopHUD.SummaryOpen) { panel.SetActive(false); return; }
        Refresh();
    }

    void Refresh()
    {
        if (supplyGroup != null) supplyGroup.SetActive(!decorMode);
        if (decorGroup != null) decorGroup.SetActive(decorMode);
        SetTabLook(supplyTab, !decorMode, Loc.T("สั่งของ", "Supplies"));
        SetTabLook(decorTab, decorMode, Loc.T("ตกแต่ง & ขยายร้าน", "Decorate & expand"));
        DecorShop shop = DecorShop.Instance;
        if (decorMode && shop != null)
        {
            int money = GameManager.Instance != null ? GameManager.Instance.money : 0;
            if (beautyText != null)
                beautyText.text = Loc.T("เงิน", "Money") + $" <b>{money:N0} ฿</b>     " + Loc.T("ความสวยร้าน", "Shop beauty") + $" <b>{shop.Beauty}</b>   " +
                                  Loc.T($"(ลูกค้า +{(shop.SpawnMultiplier - 1f) * 100f:0}%)", $"(customers +{(shop.SpawnMultiplier - 1f) * 100f:0}%)");
            for (int i = 0; i < decorLabels.Length && i < shop.items.Count; i++)
            {
                DecorShop.Item it = shop.items[i];
                if (decorLabels[i] != null)
                {
                    string t = it.owned ? $"<b>{it.Name}</b>  <size=80%>{Loc.T("มีแล้ว", "owned")}</size>" : $"<b>{it.Name}</b>  <size=80%>{it.price:N0} ฿</size>";
                    t += $"\n<size=62%>{it.Desc}</size>";
                    if (decorLabels[i].text != t) decorLabels[i].text = t;
                }
                if (decorButtons[i] != null) decorButtons[i].interactable = !it.owned;
            }
        }
        InventoryManager inv = InventoryManager.Instance;
        string[] names = { Loc.T("แก้ว", "Cups"), Loc.T("ไข่มุก", "Pearls"), Loc.T("น้ำแข็ง", "Ice"), Loc.T("ชา", "Tea"), Loc.T("ชีสโฟม", "Cheese foam") };
        if (stockText != null && inv != null)
        {
            int money = GameManager.Instance != null ? GameManager.Instance.money : 0;
            stockText.text = Loc.T("เงิน", "Money") + $" <b>{money:N0} ฿</b>     " + Loc.T("คงเหลือ", "In stock") + ":  " +
                             $"{names[0]} <b>{inv.cups}</b> · {names[1]} <b>{inv.boba}</b> · {names[2]} <b>{inv.ice}</b> · {names[3]} <b>{inv.baseSyrup}</b> · {names[4]} <b>{inv.foam}</b>";
        }
        for (int i = 0; i < orderLabels.Length; i++)
        {
            if (orderLabels[i] == null) continue;
            int cost = inv != null ? inv.PricePerUnit(Resources[i]) * Amounts[i] : 0;
            string label = $"{names[i]} +{Amounts[i]}   <size=80%>{cost} ฿</size>";
            if (orderLabels[i].text != label) orderLabels[i].text = label;
        }
        if (closeLabel != null)
        {
            string c = Loc.T("ปิด", "Close") + (GameSettings.UsePCControls ? " <size=80%>[E]</size>" : "");
            if (closeLabel.text != c) closeLabel.text = c;
        }
    }
}
