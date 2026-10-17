using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// App delivery orders (needs the "delivery" upgrade). A ticket of 2-3 drinks pops up with a timer; make each one and
// put the sealed cup on the pick-up rack at the end of the counter (in any order). A rider rides up, parks at the
// curb and waits by the rack; when the last cup is on it they bag the order and ride off: pay + a tip for the time
// left. Run out of time and the app cancels it (a small reputation dip) and the rider leaves empty-handed.
public class DeliverySystem : MonoBehaviour
{
    public static DeliverySystem Instance { get; private set; }

    public class Item
    {
        public Order order;
        public bool done;
        public ServeQuality quality;
        public CupController cup;
    }

    public class Ticket
    {
        public int number;
        public readonly List<Item> items = new List<Item>();
        public float timeLeft, total;
        public int Done { get { int n = 0; foreach (Item i in items) if (i.done) n++; return n; } }
        public bool Complete => Done == items.Count;
    }

    [Header("Timing (game-running seconds)")]
    public float firstOrderDelay = 35f;
    public float minGap = 70f, maxGap = 140f;
    public float baseTime = 60f, perDrinkTime = 40f;
    public int openHour = 9, closeHour = 20;

    [Header("World")]
    public Transform[] rackSlots;              // where the finished cups stand on the rack
    public Transform scooter;                  // the rider's scooter (moves along the curb)
    public GameObject scooterRider;            // seated rider shown while riding
    public NavMeshAgent rider;                 // the rider on foot (NPC_Rider)
    public Transform parkPoint, riderWait, arriveFrom, leaveTo;

    [Header("HUD")]
    public GameObject card;
    public TMP_Text cardTitle, cardItems;
    public Image timerFill;

    public Ticket Current { get; private set; }
    public bool RiderWaiting { get; private set; }
    public static event System.Action<Ticket, bool> OnTicketFinished;       // (ticket, delivered on time)

    float nextOrderIn;
    int counter;
    TimeCyc clock;
    Coroutine riderRun;
    NPCAnimDriver riderAnim;
    AudioSource engine;

    public bool Enabled => UpgradeShop.Instance != null && UpgradeShop.Instance.Has("delivery");

    void Awake()
    {
        Instance = this;
        nextOrderIn = firstOrderDelay;
        if (card != null) card.SetActive(false);
        if (rider != null) { riderAnim = rider.GetComponentInChildren<NPCAnimDriver>(true); rider.gameObject.SetActive(false); }
        if (scooter != null)
        {
            scooter.gameObject.SetActive(false);
            engine = scooter.GetComponent<AudioSource>();
        }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        clock = FindFirstObjectByType<TimeCyc>();
        if (clock != null) clock.OnNewDay += NewDay;
        ShopProgress.OnDaySummary += ClosingTime;
    }

    void OnDisable()
    {
        if (clock != null) clock.OnNewDay -= NewDay;
        ShopProgress.OnDaySummary -= ClosingTime;
    }

    void NewDay(int day) { Cancel(false); nextOrderIn = firstOrderDelay; }
    void ClosingTime(ShopProgress.DayStats s) { Cancel(false); }

    // ---------------------------------------------------------------- orders
    void Update()
    {
        UpdateCard();
        if (!GameMenus.GameRunning || ShopHUD.SummaryOpen || Spawner.TutorialActive || ChallengeMode.Active) return;
        if (Current == null)
        {
            int h = clock != null ? clock.Hours : 12;
            if (!Enabled || h < openHour || h >= closeHour) return;
            float mult = ShopProgress.Instance != null ? Mathf.Clamp(ShopProgress.Instance.SpawnRateMultiplier, 0.6f, 2f) : 1f;
            nextOrderIn -= Time.deltaTime * mult;
            if (nextOrderIn <= 0f) NewTicket();
            return;
        }
        Current.timeLeft -= Time.deltaTime;
        if (Current.timeLeft <= 0f) Expire();
    }

    public Ticket NewTicket(int drinks = 0)
    {
        if (Current != null) return Current;
        int level = ShopProgress.Instance != null ? ShopProgress.Instance.level : 1;
        if (drinks <= 0) drinks = level >= 3 && Random.value < 0.45f ? 3 : 2;
        var t = new Ticket { number = ++counter };
        CustomerType[] who = { CustomerType.Student, CustomerType.OfficeWorker, CustomerType.Tourist, CustomerType.Auntie };
        bool forceIce = DailyEventSystem.Instance != null && DailyEventSystem.Instance.Current.forceIce;
        for (int i = 0; i < drinks; i++)
        {
            Order o = CustomerProfiles.CreateOrder(CustomerProfiles.Get(who[Random.Range(0, who.Length)]), level, forceIce);
            o.blended = o.marshmallow = false;                           // the seasonal extras don't travel well
            t.items.Add(new Item { order = o });
        }
        t.total = t.timeLeft = baseTime + perDrinkTime * drinks;
        Current = t;
        GameAudio.Play(Sfx.AppOrder);
        ShopHUD.Toast(Loc.T($"ออเดอร์แอปเดลิเวอรี่ #{t.number}: {drinks} แก้ว · วางแก้วที่ซีลแล้วบนชั้นรับออเดอร์ข้างเครื่องซีล",
                            $"Delivery app order #{t.number}: {drinks} drinks · put the sealed cups on the pick-up rack next to the sealer"));
        if (riderRun != null) StopCoroutine(riderRun);
        riderRun = StartCoroutine(RiderComesIn(Random.Range(6f, 12f)));
        return t;
    }

    // Player puts the cup in hand on the rack. Returns true if it went on.
    public bool PlaceCup(RaycastAim player)
    {
        CupController cup = player != null ? player.HeldCup : null;
        if (Current == null) { ShopHUD.ToastNow(Loc.T("ยังไม่มีออเดอร์เดลิเวอรี่", "No delivery order right now")); return false; }
        if (cup == null) { ShopHUD.ToastNow(Loc.T("ถือแก้วที่ทำเสร็จแล้วก่อนนะ", "Hold a finished drink first")); return false; }
        if (cup.State != CupController.CupState.LidClosed) { ShopHUD.ToastNow(Loc.T("ซีลฝาก่อนส่งให้ไรเดอร์นะ", "Seal it before it goes to the rider")); return false; }
        Item best = null;
        ServeResult bestResult = null;
        foreach (Item it in Current.items)
        {
            if (it.done) continue;
            ServeResult r = ServeResult.Evaluate(it.order, cup);
            if (r.quality == ServeQuality.Wrong) continue;
            if (best == null || r.quality > bestResult.quality) { best = it; bestResult = r; }
        }
        if (best == null)
        {
            GameAudio.Play(Sfx.Reject);
            ShopHUD.ToastNow(Loc.T("แก้วนี้ไม่ตรงกับออเดอร์ในแอป", "This drink isn't on the app order"));
            return false;
        }
        best.done = true;
        best.quality = bestResult.quality;
        best.cup = cup;
        int slot = Current.Done - 1;
        Transform at = rackSlots != null && rackSlots.Length > 0 ? rackSlots[Mathf.Clamp(slot, 0, rackSlots.Length - 1)] : transform;
        cup.transform.SetParent(at, false);                         // out of the hand, onto the rack
        cup.transform.localPosition = Vector3.zero;
        cup.transform.localRotation = Quaternion.identity;
        Rigidbody rb = cup.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        GameAudio.Play(Sfx.Cup);
        ShopHUD.ToastNow(bestResult.quality == ServeQuality.Perfect
            ? Loc.T($"วางแก้ว {Current.Done}/{Current.items.Count} แล้ว", $"Cup {Current.Done}/{Current.items.Count} on the rack")
            : Loc.T($"วางแก้ว {Current.Done}/{Current.items.Count} ({bestResult.Summary})", $"Cup {Current.Done}/{Current.items.Count} on the rack ({bestResult.Summary})"));
        if (Current.Complete && RiderWaiting) Handover();
        return true;
    }

    // the rider bags the order: money for every cup + a tip for the time left
    void Handover()
    {
        Ticket t = Current;
        if (t == null) return;
        int pay = 0, perfect = 0;
        foreach (Item it in t.items)
        {
            pay += it.order.Price;
            if (it.quality == ServeQuality.Perfect) perfect++;
            if (it.cup != null) Destroy(it.cup.gameObject);
        }
        float left = Mathf.Clamp01(t.timeLeft / t.total);
        int tip = Mathf.RoundToInt((5 + 10 * left) * perfect * (ShopProgress.Instance != null ? ShopProgress.Instance.TipMultiplier : 1f));
        if (ShopProgress.Instance != null)
        {
            ShopProgress sp = ShopProgress.Instance;
            sp.today.deliveries++;
            sp.today.revenue += pay;
            sp.today.tips += tip;
            sp.reputation = Mathf.Clamp(sp.reputation + (perfect == t.items.Count ? 0.05f : 0.01f), 0f, 5f);
        }
        if (GameManager.Instance != null) GameManager.Instance.AddMoney(pay + tip);
        StatsSystem.Add(StatsSystem.Stat.Delivery);
        GameAudio.Play(Sfx.Coins);
        ShopHUD.ToastNow(Loc.T($"ไรเดอร์รับออเดอร์ #{t.number} แล้ว  +{pay} ฿  ทิป +{tip} ฿", $"Rider picked up order #{t.number}  +{pay} ฿  tip +{tip} ฿"), 2.5f);
        Current = null;
        nextOrderIn = Random.Range(minGap, maxGap);
        OnTicketFinished?.Invoke(t, true);
        if (riderRun != null) StopCoroutine(riderRun);
        riderRun = StartCoroutine(RiderLeaves(true));
    }

    void Expire()
    {
        Ticket t = Current;
        foreach (Item it in t.items) if (it.cup != null) Destroy(it.cup.gameObject);
        Current = null;
        nextOrderIn = Random.Range(minGap, maxGap);
        if (ShopProgress.Instance != null)
        {
            ShopProgress.Instance.today.deliveriesLate++;
            ShopProgress.Instance.reputation = Mathf.Clamp(ShopProgress.Instance.reputation - 0.1f, 0f, 5f);
        }
        StatsSystem.Add(StatsSystem.Stat.DeliveryLate);
        GameAudio.Play(Sfx.Angry);
        ShopHUD.Toast(Loc.T($"ออเดอร์แอป #{t.number} ถูกยกเลิก ทำไม่ทันเวลา (ชื่อเสียงลดนิดหน่อย)", $"App order #{t.number} was cancelled: too slow (small reputation hit)"));
        OnTicketFinished?.Invoke(t, false);
        if (riderRun != null) StopCoroutine(riderRun);
        riderRun = StartCoroutine(RiderLeaves(false));
    }

    // closing time / new day: quietly drop whatever is open
    public void Cancel(bool announce)
    {
        if (Current != null) foreach (Item it in Current.items) if (it.cup != null) Destroy(it.cup.gameObject);
        Current = null;
        if (riderRun != null) StopCoroutine(riderRun);
        riderRun = null;
        RiderWaiting = false;
        if (rider != null) rider.gameObject.SetActive(false);
        if (scooter != null) scooter.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- the rider
    IEnumerator Ride(Vector3 from, Vector3 to, float seconds, bool arriving)
    {
        if (scooter == null) yield break;
        scooter.gameObject.SetActive(true);
        if (scooterRider != null) scooterRider.SetActive(true);
        Vector3 dir = (to - from).normalized;
        scooter.rotation = Quaternion.LookRotation(dir);
        if (engine != null) { engine.volume = 0.7f * GameSettings.Sfx; engine.pitch = Random.Range(0.95f, 1.05f); engine.Play(); }
        for (float e = 0f; e < seconds; e += Time.deltaTime)
        {
            float k = e / seconds;
            float s = arriving ? 1f - (1f - k) * (1f - k) : k * k;           // brake into the stop / pull away
            scooter.position = Vector3.Lerp(from, to, s);
            yield return null;
        }
        scooter.position = to;
        if (engine != null && !arriving) engine.Stop();
    }

    IEnumerator WalkTo(Vector3 target, float timeout)
    {
        if (rider == null || !rider.isOnNavMesh) yield break;
        rider.SetDestination(target);
        for (float e = 0f; e < timeout; e += Time.deltaTime)
        {
            if (!rider.pathPending && rider.remainingDistance < 0.35f) break;
            yield return null;
        }
        if (rider.isOnNavMesh && rider.remainingDistance > 0.6f) rider.Warp(target);  // never stuck in a crowd
    }

    IEnumerator RiderComesIn(float delay)
    {
        RiderWaiting = false;
        yield return new WaitForSeconds(delay);
        if (scooter != null && arriveFrom != null && parkPoint != null)
        {
            yield return Ride(arriveFrom.position, parkPoint.position, 4.5f, true);
            GameAudio.PlayAt(Sfx.HornMoto, parkPoint.position, 0.8f);               // beep beep, I'm here
            if (engine != null) engine.Stop();
        }
        if (scooterRider != null) scooterRider.SetActive(false);
        if (rider != null && parkPoint != null && riderWait != null)
        {
            rider.gameObject.SetActive(true);
            if (NavMesh.SamplePosition(parkPoint.position + (riderWait.position - parkPoint.position).normalized * 0.9f, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                rider.Warp(hit.position);
            yield return WalkTo(riderWait.position, 20f);
            rider.transform.rotation = Quaternion.LookRotation(Flat(rackSlots != null && rackSlots.Length > 0 ? rackSlots[0].position - rider.transform.position : -riderWait.forward));
            if (riderAnim != null) riderAnim.Play(NPCAnimDriver.Gesture.Phone);
        }
        RiderWaiting = true;
        if (Current != null && Current.Complete) Handover();
    }

    IEnumerator RiderLeaves(bool withOrder)
    {
        RiderWaiting = false;
        if (riderAnim != null) riderAnim.Play(withOrder ? NPCAnimDriver.Gesture.Wave : NPCAnimDriver.Gesture.Angry, 1.5f);
        yield return new WaitForSeconds(1.2f);
        if (rider != null && rider.gameObject.activeSelf && parkPoint != null)
        {
            Vector3 back = parkPoint.position + (riderWait != null ? (riderWait.position - parkPoint.position).normalized * 0.9f : Vector3.zero);
            yield return WalkTo(back, 15f);
            rider.gameObject.SetActive(false);
        }
        if (scooter != null && scooter.gameObject.activeSelf && leaveTo != null)
        {
            if (scooterRider != null) scooterRider.SetActive(true);
            yield return Ride(scooter.position, leaveTo.position, 6f, false);
            scooter.gameObject.SetActive(false);
        }
        riderRun = null;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward; }

    // ---------------------------------------------------------------- HUD card
    void UpdateCard()
    {
        if (card == null) return;
        bool show = Current != null && GameMenus.GameRunning;
        if (card.activeSelf != show) card.SetActive(show);
        if (!show) return;
        int sec = Mathf.CeilToInt(Mathf.Max(0f, Current.timeLeft));
        string time = $"{sec / 60}:{sec % 60:00}";
        if (cardTitle != null)
            cardTitle.text = Loc.T($"เดลิเวอรี่ #{Current.number}  ·  {time}", $"Delivery #{Current.number}  ·  {time}") +
                             (RiderWaiting ? Loc.T("  <size=75%>ไรเดอร์รออยู่</size>", "  <size=75%>rider waiting</size>") : "");
        if (cardItems != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Item it in Current.items)
            {
                string line = it.order.ToThaiString().Replace("\n", " · ");
                sb.AppendLine(it.done ? $"<color=#4E8A3C>• <s>{line}</s>  <b>{Loc.T("วางแล้ว", "ready")}</b></color>" : $"• {line}");
            }
            cardItems.text = sb.ToString().TrimEnd();
        }
        if (timerFill != null)
        {
            float k = Mathf.Clamp01(Current.timeLeft / Current.total);
            timerFill.fillAmount = k;
            timerFill.color = Color.Lerp(new Color(0.95f, 0.3f, 0.3f), new Color(0.45f, 0.8f, 0.35f), k);
        }
    }
}
