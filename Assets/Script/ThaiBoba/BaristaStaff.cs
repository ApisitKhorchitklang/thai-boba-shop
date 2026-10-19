using TMPro;
using UnityEngine;

// The second staff member (พนักงานชงชา, hired at the shop computer): stands at the left end of the counter and
// takes the order of the next customer in line while you serve yours (never yours: the HUD and the counter follow
// the first customer the barista hasn't taken), then shakes it up and hands it over.
// Skill level 1-3 (training at the computer): faster and more often perfect. Paid a daily wage.
public class BaristaStaff : MonoBehaviour, IInteractable
{
    public static BaristaStaff Instance { get; private set; }

    public GameObject body;                    // the NPC model, shown once hired
    public TMP_Text speech;                    // world-space label above the head
    public float[] secondsPerLevel = { 24f, 17f, 12f };
    public float[] perfectPerLevel = { 0.75f, 0.88f, 0.96f };

    CustomerController working;
    bool hasWork;
    float progress, needed, shakeTimer, chatTimer;
    NPCAnimDriver anim;
    RaycastAim player;
    TimeCyc clock;

    public bool Hired => DecorShop.Instance != null && DecorShop.Instance.Has("barista");
    public int Level => DecorShop.Instance != null ? DecorShop.Instance.baristaLevel : 1;
    public CustomerController Working => working;
    public float Progress01 => needed > 0f ? Mathf.Clamp01(progress / needed) : 0f;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        anim = GetComponentInChildren<NPCAnimDriver>(true);
        player = FindFirstObjectByType<RaycastAim>();
        clock = FindFirstObjectByType<TimeCyc>();
    }

    void Update()
    {
        bool hired = Hired;
        if (body != null && body.activeSelf != hired) body.SetActive(hired);
        if (speech != null && Camera.main != null) speech.transform.rotation = Camera.main.transform.rotation;
        if (!hired || !GameMenus.GameRunning || ShopHUD.SummaryOpen || Spawner.TutorialActive) { Drop(); Say(""); return; }

        if (hasWork && (working == null || !working.InLine)) Drop();             // left the line / was served / gone
        if (working == null) { PickCustomer(); if (working == null) { Idle(); return; } }

        progress += Time.deltaTime;
        shakeTimer -= Time.deltaTime;
        if (shakeTimer <= 0f)
        {
            shakeTimer = 2f;
            GameAudio.PlayAt(Sfx.Shake, transform.position + Vector3.up * 1.2f, 0.6f);
        }
        Say(Loc.T($"กำลังชง {working.order.DrinkNameTH} {Progress01 * 100f:0}%", $"Making {working.order.DrinkNameTH} {Progress01 * 100f:0}%"));
        if (progress >= needed) Finish();
    }

    void PickCustomer()
    {
        // Never the player's customer (the first one not taken): only the next one in line. One clear rule beats
        // "sometimes the barista grabs the front one too", which had players making the same drink twice.
        CustomerController mine = CustomerController.PlayerFront, pick = null;
        foreach (CustomerController c in CustomerController.Queue)
            if (c != null && c != mine && c.claimedBy == null && c.ArrivedInLine) { pick = c; break; }
        if (pick == null) return;
        working = pick;
        hasWork = true;
        working.claimedBy = this;
        progress = 0f;
        Order o = working.order;
        int extras = (o.wantsFoam ? 1 : 0) + (o.blended ? 1 : 0) + (o.marshmallow ? 1 : 0) + (string.IsNullOrEmpty(o.topping) ? 0 : 1);
        needed = secondsPerLevel[Mathf.Clamp(Level - 1, 0, secondsPerLevel.Length - 1)] + 3f * extras;
        shakeTimer = 0.5f;
        if (anim != null) anim.Play(NPCAnimDriver.Gesture.Shake);
    }

    void Finish()
    {
        CustomerController c = working;
        working = null;
        hasWork = false;
        if (anim != null) anim.Play(NPCAnimDriver.Gesture.None);
        if (c == null || !c.InLine) return;
        bool perfect = Random.value < perfectPerLevel[Mathf.Clamp(Level - 1, 0, perfectPerLevel.Length - 1)];
        c.ServedByStaff(perfect ? ServeQuality.Perfect : ServeQuality.Okay);
        Say(Loc.T("ได้แล้วค่า!", "Here you go!"));
        chatTimer = 1.5f;
    }

    // the player served "our" customer first: start over with someone else
    public void CustomerServedByPlayer(CustomerController c)
    {
        if (c != working) return;
        working = null;
        hasWork = false;
        if (anim != null) anim.Play(NPCAnimDriver.Gesture.None);
        Say(Loc.T("อ้าว เจ้าของร้านทำให้แล้ว", "Oh, the boss got that one"));
        chatTimer = 1.5f;
    }

    void Drop()
    {
        if (!hasWork) return;
        if (working != null && working.claimedBy == this) working.claimedBy = null;
        if (anim != null) anim.Play(NPCAnimDriver.Gesture.None);
        working = null;
        hasWork = false;
    }

    void Idle()
    {
        if (chatTimer > 0f) { chatTimer -= Time.deltaTime; return; }
        int h = clock != null ? clock.Hours : 12;
        Say(h >= 21 ? Loc.T("เก็บร้านแล้วจ้า", "Cleaning up") : "");
    }

    void Say(string text)
    {
        if (speech != null && speech.text != text) speech.text = text;
    }

    // ---------------------------------------------------------------- IInteractable
    public string Prompt(RaycastAim p)
    {
        if (!Hired) return "";
        string lv = Loc.T($"พนักงานชงชา (ฝีมือเลเวล {Level})", $"Barista (skill level {Level})");
        return working != null ? lv + Loc.T($" · ชงให้ลูกค้าคนถัดไป {Progress01 * 100f:0}%", $" · making the next customer's drink {Progress01 * 100f:0}%")
                               : lv + Loc.T(" · ว่าง รอลูกค้าคนถัดไป", " · free, waiting for the next customer");
    }

    public void Interact(RaycastAim p)
    {
        if (!Hired) return;
        ShopHUD.ToastNow(Loc.T("พนักงานรับลูกค้าคนที่ 2 ในคิวให้ระหว่างที่คุณทำแก้วหน้าเคาน์เตอร์ · ฝึกฝีมือได้ที่คอมพิวเตอร์",
                               "Your barista takes the 2nd customer in line while you serve the front · train them at the computer"), 2.6f);
    }
}
