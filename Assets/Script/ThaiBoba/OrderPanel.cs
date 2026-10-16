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
    public Button[] orderButtons = new Button[4];     // cups, boba, ice, tea
    public TMP_Text[] orderLabels = new TMP_Text[4];
    public Button closeButton;
    public TMP_Text closeLabel;

    static readonly string[] Resources = { "cups", "boba", "ice", "syrup" };
    static readonly int[] Amounts = { 10, 50, 50, 50 };
    int openedFrame;

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
        InventoryManager inv = InventoryManager.Instance;
        string[] names = { Loc.T("แก้ว", "Cups"), Loc.T("ไข่มุก", "Pearls"), Loc.T("น้ำแข็ง", "Ice"), Loc.T("ชา", "Tea") };
        if (stockText != null && inv != null)
        {
            int money = GameManager.Instance != null ? GameManager.Instance.money : 0;
            stockText.text = Loc.T("เงิน", "Money") + $" <b>{money:N0} ฿</b>     " + Loc.T("คงเหลือ", "In stock") + ":  " +
                             $"{names[0]} <b>{inv.cups}</b> · {names[1]} <b>{inv.boba}</b> · {names[2]} <b>{inv.ice}</b> · {names[3]} <b>{inv.baseSyrup}</b>";
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
