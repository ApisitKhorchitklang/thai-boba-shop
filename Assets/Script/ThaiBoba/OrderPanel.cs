using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The shop computer: order supplies / decorate & expand (+ arrange the decorations) / staff & promotions
// (hire the helper or a barista, train the barista, a promo against the rival, open branch 2 in the story).
// Opened from SupplyComputer; the game keeps running while it is up. PC: the cursor is freed; E / Esc close it.
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

    [Header("Tabs: supplies / decorate & expand / staff & promo")]
    public GameObject supplyGroup, decorGroup, staffGroup;
    public Button supplyTab, decorTab, staffTab;
    public Button[] decorButtons = new Button[8];          // decorations + expansion; the last one is "arrange"
    public TMP_Text[] decorLabels = new TMP_Text[8];
    public TMP_Text beautyText;
    public Button[] staffButtons = new Button[5];          // helper, barista, training, promo, branch 2
    public TMP_Text[] staffLabels = new TMP_Text[5];
    public TMP_Text staffInfo;
    enum Tab { Supply, Decor, Staff }
    Tab tab;

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
        if (supplyTab != null) supplyTab.onClick.AddListener(() => { tab = Tab.Supply; GameAudio.Play(Sfx.Click); Refresh(); });
        if (decorTab != null) decorTab.onClick.AddListener(() => { tab = Tab.Decor; GameAudio.Play(Sfx.Click); Refresh(); });
        if (staffTab != null) staffTab.onClick.AddListener(() => { tab = Tab.Staff; GameAudio.Play(Sfx.Click); Refresh(); });
        for (int i = 0; i < decorButtons.Length; i++)
        {
            int k = i;
            if (decorButtons[i] != null) decorButtons[i].onClick.AddListener(() => BuyDecor(k));
        }
        for (int i = 0; i < staffButtons.Length; i++)
        {
            int k = i;
            if (staffButtons[i] != null) staffButtons[i].onClick.AddListener(() => Staff(k));
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

    // decorations + expansion (the staff are on their own tab)
    static System.Collections.Generic.List<DecorShop.Item> DecorItems() =>
        DecorShop.Instance != null ? DecorShop.Instance.items.FindAll(i => i.kind != DecorShop.Kind.Helper) : new System.Collections.Generic.List<DecorShop.Item>();

    void BuyDecor(int i)
    {
        DecorShop shop = DecorShop.Instance;
        var list = DecorItems();
        if (i == decorButtons.Length - 1)                                    // last button: arrange the shop
        {
            if (DecorPlacement.Instance != null) DecorPlacement.Instance.Begin();
            return;
        }
        if (shop == null || i >= list.Count) return;
        shop.Buy(list[i].id);
        Refresh();
    }

    void Staff(int i)
    {
        DecorShop shop = DecorShop.Instance;
        switch (i)
        {
            case 0: if (shop != null) shop.Buy("helper"); break;
            case 1: if (shop != null) shop.Buy("barista"); break;
            case 2: if (shop != null) shop.TrainBarista(); break;
            case 3: if (RivalShop.Instance != null) RivalShop.Instance.BuyPromo(); break;
            case 4: if (StoryMode.Instance != null && StoryMode.Instance.OpenBranch()) Close(); break;
        }
        Refresh();
    }

    public void ShowDecorTab() { tab = Tab.Decor; Refresh(); }      // tests / tutorial
    public void ShowStaffTab() { tab = Tab.Staff; Refresh(); }

    void Order(int i)
    {
        ComputerTerminal terminal = FindFirstObjectByType<ComputerTerminal>();
        if (terminal == null) return;
        GameAudio.Play(Sfx.Click);
        terminal.OrderSupply(Resources[i], Amounts[i]);
        Refresh();
    }

    void RefreshStaff(DecorShop shop)
    {
        RivalShop rival = RivalShop.Instance;
        StoryMode story = StoryMode.Instance;
        int money = GameManager.Instance != null ? GameManager.Instance.money : 0;
        if (staffInfo != null)
        {
            string t = Loc.T("เงิน", "Money") + $" <b>{money:N0} ฿</b>";
            if (rival != null && rival.Open) t += Loc.T($"     ร้านคู่แข่ง: แรงดึงลูกค้า <b>{rival.pull * 100f:0}%</b>", $"     Rival: pulling <b>{rival.pull * 100f:0}%</b> of customers");
            if (staffInfo.text != t) staffInfo.text = t;
        }
        DecorShop.Item helper = shop != null ? shop.Get("helper") : null, barista = shop != null ? shop.Get("barista") : null;
        void Row(int i, string label, bool interactable, bool show = true)
        {
            if (i >= staffButtons.Length || staffButtons[i] == null) return;
            staffButtons[i].gameObject.SetActive(show);
            staffButtons[i].interactable = interactable;
            if (staffLabels[i] != null && staffLabels[i].text != label) staffLabels[i].text = label;
        }
        string Item(DecorShop.Item it) => it == null ? "" : (it.owned ? $"<b>{it.Name}</b>  <size=80%>{Loc.T("จ้างแล้ว", "hired")}</size>" : $"<b>{it.Name}</b>  <size=80%>{it.price:N0} ฿</size>") + $"\n<size=62%>{it.Desc}</size>";
        Row(0, Item(helper), helper != null && !helper.owned);
        Row(1, Item(barista), barista != null && !barista.owned);
        int lv = shop != null ? shop.baristaLevel : 1;
        bool canTrain = barista != null && barista.owned && lv < 3;
        Row(2, lv >= 3 ? Loc.T("<b>ฝึกพนักงาน</b>  <size=80%>ฝีมือสูงสุดแล้ว</size>\n<size=62%>เลเวล 3: ชงไวและเป๊ะเกือบทุกแก้ว</size>", "<b>Train the barista</b>  <size=80%>max skill</size>\n<size=62%>Level 3: fast and nearly always perfect</size>")
                     : Loc.T($"<b>ฝึกพนักงาน → เลเวล {lv + 1}</b>  <size=80%>{DecorShop.TrainCost[lv]:N0} ฿</size>\n<size=62%>ชงไวขึ้น เป๊ะขึ้น (ค่าแรง +30 ฿/วัน)</size>",
                             $"<b>Train barista → level {lv + 1}</b>  <size=80%>{DecorShop.TrainCost[lv]:N0} ฿</size>\n<size=62%>Faster, more precise (wage +30 ฿/day)</size>"), canTrain);
        bool rivalOpen = rival != null && rival.Open;
        Row(3, rivalOpen ? (rival.PromoToday ? Loc.T("<b>โปรโมชั่นวันนี้</b>  <size=80%>ทำแล้ว</size>\n<size=62%>ลูกค้าเพิ่ม + ดึงลูกค้ากลับ 2 เท่า</size>", "<b>Today's promo</b>  <size=80%>running</size>\n<size=62%>More customers + win them back twice as fast</size>")
                                             : Loc.T($"<b>แจกใบปลิว + ป้ายโปร</b>  <size=80%>{rival.promoCost} ฿</size>\n<size=62%>วันนี้: ลูกค้าเพิ่ม + ดึงลูกค้าจากคู่แข่ง 2 เท่า</size>", $"<b>Flyers + promo sign</b>  <size=80%>{rival.promoCost} ฿</size>\n<size=62%>Today: more customers, win back twice as fast</size>"))
                         : Loc.T("<b>โปรโมชั่นสู้คู่แข่ง</b>\n<size=62%>ใช้ได้เมื่อมีร้านคู่แข่งเปิดฝั่งตรงข้าม</size>", "<b>Promo against a rival</b>\n<size=62%>Available while a rival is open across the street</size>"),
            rivalOpen && !rival.PromoToday);
        bool storyBranch = story != null && story.Active && story.ChapterIndex == 4;
        Row(4, Loc.T($"<b>เปิดสาขา 2</b>  <size=80%>{(story != null ? story.branchCost : 12000):N0} ฿</size>\n<size=62%>ต้องมีเงินเก็บ 15,000 ฿ (เนื้อเรื่องบทที่ 5)</size>",
                     $"<b>Open branch 2</b>  <size=80%>{(story != null ? story.branchCost : 12000):N0} ฿</size>\n<size=62%>Needs 15,000 ฿ saved (story chapter 5)</size>"),
            story != null && story.CanOpenBranch, storyBranch);
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
        bool decorMode = tab == Tab.Decor;
        if (supplyGroup != null) supplyGroup.SetActive(tab == Tab.Supply);
        if (decorGroup != null) decorGroup.SetActive(decorMode);
        if (staffGroup != null) staffGroup.SetActive(tab == Tab.Staff);
        SetTabLook(supplyTab, tab == Tab.Supply, Loc.T("สั่งของ", "Supplies"));
        SetTabLook(decorTab, decorMode, Loc.T("ตกแต่ง & ขยายร้าน", "Decorate & expand"));
        SetTabLook(staffTab, tab == Tab.Staff, Loc.T("พนักงาน & โปรโมชั่น", "Staff & promo"));
        DecorShop shop = DecorShop.Instance;
        if (tab == Tab.Staff) RefreshStaff(shop);
        if (decorMode && shop != null)
        {
            int money = GameManager.Instance != null ? GameManager.Instance.money : 0;
            if (beautyText != null)
                beautyText.text = Loc.T("เงิน", "Money") + $" <b>{money:N0} ฿</b>     " + Loc.T("ความสวยร้าน", "Shop beauty") + $" <b>{shop.Beauty}</b>   " +
                                  Loc.T($"(ลูกค้า +{(shop.SpawnMultiplier - 1f) * 100f:0}%)", $"(customers +{(shop.SpawnMultiplier - 1f) * 100f:0}%)");
            var list = DecorItems();
            for (int i = 0; i < decorLabels.Length - 1; i++)
            {
                bool has = i < list.Count;
                if (decorButtons[i] != null) decorButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                DecorShop.Item it = list[i];
                if (decorLabels[i] != null)
                {
                    string t = it.owned ? $"<b>{it.Name}</b>  <size=80%>{Loc.T("มีแล้ว", "owned")}</size>" : $"<b>{it.Name}</b>  <size=80%>{it.price:N0} ฿</size>";
                    t += $"\n<size=62%>{it.Desc}</size>";
                    if (decorLabels[i].text != t) decorLabels[i].text = t;
                }
                if (decorButtons[i] != null) decorButtons[i].interactable = !it.owned;
            }
            int last = decorLabels.Length - 1;
            if (last >= 0 && decorLabels[last] != null)
            {
                string t = $"<b>{Loc.T("จัดร้านเอง", "Arrange the shop")}</b>\n<size=62%>{Loc.T("ย้าย/หมุน ต้นไม้ ม้านั่ง โต๊ะ ป้าย ได้ตามใจ", "Move / turn plants, bench, tables, signs")}</size>";
                if (decorLabels[last].text != t) decorLabels[last].text = t;
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
