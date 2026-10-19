using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public class CustomerController : MonoBehaviour
{
    public Order order;
    [System.NonSerialized] public RegularsSystem.Regular regular;    // set when this is one of the soi's regulars
    CustomerEmote emote;
    bool warnedImpatient;
    NPCAnimDriver anim;
    float gestureTimer = 2f;
    NPCAnimDriver Anim => anim != null ? anim : (anim = GetComponentInChildren<NPCAnimDriver>());
    void Gesture(NPCAnimDriver.Gesture g, float seconds = 0f) { if (Anim != null) Anim.Play(g, seconds); }
    void Emote(CustomerEmote.Kind k, float seconds = 2f)
    {
        if (emote == null) emote = GetComponent<CustomerEmote>();
        if (emote != null) emote.Show(k, seconds);
    }
    public int payment = 10; // fallback flat payment when no ShopProgress exists

    [Header("Thai customer")]
    public CustomerType customerType = CustomerType.Student;
    [Tooltip("Seconds this customer will wait at the counter (scaled by events/upgrades)")]
    public float maxPatience = 60f;
    [Tooltip("Optional world-space TMP text above the head (speech bubble)")]
    public TMP_Text speechText;
    [Tooltip("Optional bar whose local X scale shows remaining patience")]
    public Transform patienceBar;
    [Tooltip("Optional renderer tinted green → red with patience")]
    public Renderer moodRenderer;
    public float queueSpacing = 0.9f;

    public Transform spawnPoint;
    public Transform enterArea;
    public Transform orderArea;
    public Transform exitArea;
    public Transform[] waypoints;

    // Shared counter queue: index 0 is being served
    public static readonly List<CustomerController> Queue = new List<CustomerController>();
    // Where the line stands (slot 0 at the counter, then along the shopfront, never into the road);
    // set by the Spawner from the scene markers. Customers still walking over count toward a full queue.
    public static Vector3[] Slots;
    public static int Incoming;
    public static int Hops;                        // safety-net teleports (should stay rare)
    bool countedIncoming;
    Quaternion faceRotation;
    public static CustomerController Front => Queue.Count > 0 ? Queue[0] : null;
    // The player's customer: the first one in line the barista isn't looking after. The order card, the counter and
    // the patience timer all follow this one, so the player never makes a drink the barista is already making.
    public static CustomerController PlayerFront
    {
        get
        {
            foreach (CustomerController c in Queue)
                if (c != null && c.claimedBy == null) return c;
            return null;
        }
    }
    // Tutorial: nobody gets impatient while the player is learning
    // Patience stops while the tutorial runs or the story popup is open. Each owner keeps its own flag, so one
    // closing can never leave the other's freeze switched on (that left every customer waiting forever).
    public static bool FrozenByTutorial, FrozenByStory;
    public static bool PatienceFrozen => FrozenByTutorial || FrozenByStory;
    [System.NonSerialized] public BaristaStaff claimedBy;          // the barista is making this customer's drink

    public float Patience { get; private set; }
    public float PatienceNormalized => maxPatience > 0 ? Mathf.Clamp01(Patience / maxPatience) : 1f;
    public bool IsWaitingAtCounter => state == CustomerState.GoingToOrder && arrivedAtSlot && PlayerFront == this;
    public CustomerProfiles.Profile Profile { get; private set; }

    private enum CustomerState
    {
        GoingToEnter,
        GoingToOrder,
        GoingToExit,
        GoingToWaypoints,
        GoingToSeat,          // NavMesh walk to the seat's door / approach point
        SeatRouteIn,          // agent off: straight lines inside the shop to the stool
        Seated,
        SeatRouteOut,         // back out to the door, then off down the soi
        Finished
    }

    private NavMeshAgent agent;
    private int waypointIndex;
    private CustomerState state;
    private int queueSlot = -1;
    private bool arrivedAtSlot;
    private bool greeted;
    private float speechTimer;
    private float stuckTimer;

    void Start()
    {
        Profile = CustomerProfiles.Get(customerType);
        ThaiText.Enable(speechText);

        // Unity serializes 'order' on the prefab as an empty Order, so treat a blank flavor as "no order yet"
        if (order == null || string.IsNullOrEmpty(order.flavor))
        {
            int level = ShopProgress.Instance != null ? ShopProgress.Instance.level : 99;
            bool forceIce = DailyEventSystem.Instance != null && DailyEventSystem.Instance.Current.forceIce;
            order = CustomerProfiles.CreateOrder(Profile, level, forceIce);
        }

        float patienceMult = 1f;
        if (ShopProgress.Instance != null)
            patienceMult = ShopProgress.Instance.PatienceMultiplier;
        if (regular != null && RegularsSystem.Instance != null) patienceMult *= RegularsSystem.Instance.PatienceMultiplier(regular);
        maxPatience = Profile.patience * patienceMult;
        Patience = maxPatience;

        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = gameObject.AddComponent<NavMeshAgent>();

        if (spawnPoint != null)
            transform.position = spawnPoint.position;

        agent.avoidancePriority = 50;
        state = CustomerState.GoingToEnter;
        countedIncoming = true;
        Incoming++;
        if (Slots != null && Slots.Length > 0 || enterArea != null)
            GoToEnterArea();
        else if (orderArea != null)
            GoToOrderArea();

        Say("");
        Debug.Log($"{Profile.Label} spawned with {order}");
    }

    void OnDestroy()
    {
        Queue.Remove(this);
        StopIncoming();
        if (seat != null && seat.taken == this) seat.taken = null;
    }

    void StopIncoming()
    {
        if (!countedIncoming) return;
        countedIncoming = false;
        Incoming = Mathf.Max(0, Incoming - 1);
    }

    void Update()
    {
        UpdateSpeech();

        if (agent == null || state == CustomerState.Finished)
            return;

        if (state == CustomerState.GoingToEnter)
        {
            // walk to the end of the line (it moves as people are served) and join it once close
            Vector3 tail = SlotPosition(Queue.Count);
            retarget -= Time.deltaTime;
            if (retarget <= 0f)
            {
                retarget = 0.5f;
                agent.SetDestination(tail);
            }
            Vector3 d = tail - transform.position;
            d.y = 0f;
            if (d.magnitude < 1.4f)
            {
                GoToOrderArea();
                return;
            }
            // Safety net: if something blocks the footpath, hop next to the line instead of freezing
            stuckTimer = agent.velocity.sqrMagnitude < 0.01f && !agent.pathPending ? stuckTimer + Time.deltaTime : 0f;
            if (stuckTimer > 5f)
            {
                stuckTimer = 0f;
                if (NavMesh.SamplePosition(tail, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) agent.Warp(hit.position);
                GoToOrderArea();
            }
            return;
        }

        if (state == CustomerState.GoingToOrder && orderArea != null)
        {
            UpdateQueue();
            return;
        }

        if (state == CustomerState.GoingToExit && exitArea != null)
        {
            if (StuckWalking()) { agent.Warp(exitArea.position); GoToWaypoints(); return; }
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                if (Vector3.Distance(transform.position, exitArea.position) < 0.5f)
                {
                    GoToWaypoints();
                }
            }
            return;
        }

        if (state >= CustomerState.GoingToSeat && state <= CustomerState.SeatRouteOut)
        {
            UpdateSeat();
            return;
        }

        if (state == CustomerState.GoingToWaypoints)
        {
            if (StuckWalking()) { Destroy(gameObject); return; }
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                if (waypointIndex < waypoints.Length)
                {
                    if (waypoints[waypointIndex] != null)
                    {
                        agent.SetDestination(waypoints[waypointIndex].position);
                        waypointIndex++;
                    }
                    else
                    {
                        Destroy(gameObject);
                    }
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    // ---------------------------------------------------------------- queue & patience
    public static Vector3 SlotPosition(int slot, Transform fallback = null)
    {
        if (Slots == null || Slots.Length == 0)
            return fallback != null ? fallback.position - fallback.forward * 0.9f * slot : Vector3.zero;
        if (slot < Slots.Length) return Slots[Mathf.Max(0, slot)];
        // more people than markers: keep going the way the line was heading
        Vector3 last = Slots[Slots.Length - 1];
        Vector3 dir = Slots.Length > 1 ? (last - Slots[Slots.Length - 2]).normalized : Vector3.back;
        return last + dir * 0.75f * (slot - Slots.Length + 1);
    }

    Vector3 SlotPosition(int slot) => SlotPosition(slot, orderArea);

    // face the person ahead (or the counter from the front of the line)
    Quaternion SlotFacing(int slot)
    {
        Vector3 here = SlotPosition(slot);
        Vector3 ahead = slot > 0 ? SlotPosition(slot - 1) : here + (SlotPosition(0) - SlotPosition(1));
        Vector3 f = ahead - here;
        f.y = 0f;
        return f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f.normalized) : transform.rotation;
    }

    void UpdateQueue()
    {
        int slot = Queue.IndexOf(this);
        if (slot != queueSlot)
        {
            queueSlot = slot;
            arrivedAtSlot = false;
            bestDistance = float.MaxValue;
            stuckTimer = 0f;
            agent.updateRotation = true;
            agent.avoidancePriority = 50;
            agent.SetDestination(SlotPosition(slot));
        }
        if (arrivedAtSlot)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, faceRotation, 240f * Time.deltaTime);
            WaitingGestures(slot);
        }

        // pushed around or blocked: if we get no closer to the spot for 4 s, hop onto it
        if (!arrivedAtSlot && slot >= 0)
        {
            float dist = Vector3.Distance(transform.position, SlotPosition(slot));
            if (dist < bestDistance - 0.15f) { bestDistance = dist; stuckTimer = 0f; }
            else stuckTimer += Time.deltaTime;
            if (stuckTimer > 4f)
            {
                stuckTimer = 0f;
                if (dist > 0.4f && NavMesh.SamplePosition(SlotPosition(slot), out NavMeshHit hit, 0.6f, NavMesh.AllAreas)) { agent.Warp(hit.position); Hops++; }
                agent.SetDestination(SlotPosition(slot));
                arrivedAtSlot = true;
                OnArrivedAtSlot(slot);
            }
        }

        if (!arrivedAtSlot && !agent.pathPending && agent.remainingDistance < 0.5f)
        {
            arrivedAtSlot = true;
            OnArrivedAtSlot(slot);
        }

        // Only the player's customer loses patience; people further back, and whoever the barista is serving, just wait
        bool mine = PlayerFront == this;
        if (mine && arrivedAtSlot && !PatienceFrozen)
            Patience -= Time.deltaTime;
        if (!warnedImpatient && mine && arrivedAtSlot && PatienceNormalized < 0.25f)
        {
            warnedImpatient = true;
            Emote(CustomerEmote.Kind.Angry, 2.5f);                       // hurry up!
        }

        if (Patience <= 0f)
            LeaveAngry();
    }

    // people in line look at their phones or their watches; the one at the counter gets fidgety when it drags on
    void WaitingGestures(int slot)
    {
        gestureTimer -= Time.deltaTime;
        if (gestureTimer > 0f || Anim == null) return;
        if (slot == 0)
        {
            if (PatienceNormalized < 0.35f && Random.value < 0.6f) { Gesture(NPCAnimDriver.Gesture.Watch, 2f); gestureTimer = Random.Range(4f, 7f); }
            else gestureTimer = 2f;
            return;
        }
        float r = Random.value;
        if (r < 0.55f) { Gesture(NPCAnimDriver.Gesture.Phone, Random.Range(4f, 9f)); gestureTimer = Random.Range(8f, 14f); }
        else if (r < 0.75f) { Gesture(NPCAnimDriver.Gesture.Watch, 2f); gestureTimer = Random.Range(5f, 9f); }
        else gestureTimer = Random.Range(3f, 6f);
    }

    void OnArrivedAtSlot(int slot)
    {
        if (slot == 0 && Anim != null && Anim.Current == NPCAnimDriver.Gesture.Phone) Gesture(NPCAnimDriver.Gesture.None);   // phone away, ready to order
        // stand still facing forward; walkers go round people in line
        faceRotation = SlotFacing(slot);
        agent.updateRotation = false;
        agent.avoidancePriority = 20;
        if (slot == 0 && !greeted)
        {
            greeted = true;
            string festive = regular == null ? CustomerProfiles.FestivalGreeting(order) : null;
            Say(regular != null && RegularsSystem.Instance != null ? RegularsSystem.Instance.Greeting(regular, order)
                : festive ?? CustomerProfiles.Line(Profile.greet, order), 4f);
            if (regular != null) { Emote(CustomerEmote.Kind.Happy); Gesture(NPCAnimDriver.Gesture.Wave, 1.8f); }   // a familiar face waves hello
        }
    }

    // walking away but not getting anywhere (blocked path): skip to where they were heading
    bool StuckWalking()
    {
        stuckTimer = agent.velocity.sqrMagnitude < 0.01f && !agent.pathPending ? stuckTimer + Time.deltaTime : 0f;
        if (stuckTimer < 4f) return false;
        stuckTimer = 0f;
        return true;
    }

    // read by tests / debug overlays
    public string StateName => state.ToString();
    public bool InLine => state == CustomerState.GoingToOrder;
    public bool ArrivedInLine => state == CustomerState.GoingToOrder && arrivedAtSlot;

    // At the counter, ready to order (used by the tutorial)
    public bool AtCounter => Queue.IndexOf(this) == 0 && arrivedAtSlot;

    // Order card title: a regular shows their name and friendship
    public string DisplayName => regular != null && RegularsSystem.Instance != null
        ? $"{regular.Name} <size=62%>({RegularsSystem.Instance.FriendshipLabel(regular)})</size>"
        : Profile.Label;

    void LeaveAngry()
    {
        if (regular != null && RegularsSystem.Instance != null) RegularsSystem.Instance.OnLeftAngry(regular);
        Emote(CustomerEmote.Kind.Angry, 2.5f);
        Gesture(NPCAnimDriver.Gesture.Angry, 1.6f);
        Say(CustomerProfiles.Line(Profile.angry, order), 3f);
        if (ShopProgress.Instance != null)
            ShopProgress.Instance.RegisterAngry(this);
        Queue.Remove(this);
        GoToExitArea();
    }

    // ---------------------------------------------------------------- speech bubble
    void Say(string text, float seconds = 0f)
    {
        if (speechText == null) return;
        speechText.text = text;
        speechTimer = seconds;
    }

    void UpdateSpeech()
    {
        if (speechTimer > 0f)
        {
            speechTimer -= Time.deltaTime;
            if (speechTimer <= 0f && speechText != null)
                speechText.text = state == CustomerState.GoingToOrder ? order.ToThaiString() : "";
        }

        if (speechText != null && Camera.main != null)
            speechText.transform.rotation = Camera.main.transform.rotation;

        bool waiting = state == CustomerState.GoingToOrder;
        if (patienceBar != null)
        {
            patienceBar.gameObject.SetActive(waiting);
            Vector3 s = patienceBar.localScale;
            s.x = PatienceNormalized;
            patienceBar.localScale = s;
        }
        if (moodRenderer != null && waiting)
            moodRenderer.material.color = Color.Lerp(Color.red, Color.green, PatienceNormalized);
    }

    // ---------------------------------------------------------------- navigation
    public void AssignSpawnPoint(Transform target)
    {
        spawnPoint = target;
        if (spawnPoint != null)
            transform.position = spawnPoint.position;
    }

    public void AssignEnterArea(Transform target)
    {
        enterArea = target;
    }

    public void AssignOrderArea(Transform target)
    {
        orderArea = target;
    }

    public void AssignExitArea(Transform target)
    {
        exitArea = target;
    }

    public void AssignWaypoints(Transform[] targets)
    {
        waypoints = targets;
    }

    float retarget;
    float bestDistance = float.MaxValue;

    public void GoToEnterArea()
    {
        if (agent == null)
            return;

        state = CustomerState.GoingToEnter;
        retarget = 0.5f;
        agent.SetDestination(Slots != null && Slots.Length > 0 ? SlotPosition(Queue.Count) : enterArea.position);
    }

    public void GoToOrderArea()
    {
        if (agent == null || orderArea == null)
            return;

        state = CustomerState.GoingToOrder;
        StopIncoming();
        if (!Queue.Contains(this))
            Queue.Add(this);
        queueSlot = -1;
        arrivedAtSlot = false;
    }

    // Closing time / new day: everyone still waiting leaves without hurting the shop's reputation.
    // instant = true removes them on the spot (a new morning must start with an empty shop).
    public static void SendAllHome(bool instant)
    {
        foreach (CustomerController c in FindObjectsByType<CustomerController>(FindObjectsSortMode.None))
        {
            if (instant) Destroy(c.gameObject);
            else c.LeaveForTheDay();
        }
        Queue.Clear();
        if (instant) Incoming = 0;
    }

    void LeaveForTheDay()
    {
        if (state == CustomerState.GoingToExit || state == CustomerState.Finished || state == CustomerState.SeatRouteOut) return;
        if (state >= CustomerState.GoingToSeat && state <= CustomerState.Seated)
        {
            Say(Loc.T("ร้านจะปิดแล้ว ไปก่อนนะ", "Closing time, see you!"), 3f);
            StandUp();
            return;
        }
        Say(Loc.T("ร้านปิดแล้วเหรอ ไว้มาใหม่นะ", "Closed already? See you tomorrow!"), 3f);
        GoToExitArea();
    }

    public void GoToExitArea()
    {
        Queue.Remove(this);
        StopIncoming();
        if (agent != null)
        {
            agent.updateRotation = true;
            agent.avoidancePriority = 55;
        }
        if (agent == null || exitArea == null)
        {
            state = CustomerState.Finished;
            Destroy(gameObject, 3f);
            return;
        }

        state = CustomerState.GoingToExit;
        agent.SetDestination(exitArea.position);
    }

    public void GoToWaypoints()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            state = CustomerState.Finished;
            Destroy(gameObject);
            return;
        }

        waypointIndex = 0;
        state = CustomerState.GoingToWaypoints;

        if (waypoints[waypointIndex] != null)
            agent.SetDestination(waypoints[waypointIndex].position);
        else
        {
            state = CustomerState.Finished;
            Destroy(gameObject);
        }
    }

    // ---------------------------------------------------------------- serving
    // Called when serving a cup to this customer. Returns true if the customer accepted the drink.
    public bool TryServe(CupController cup)
    {
        if (cup == null) return false;

        ServeResult result = ServeResult.Evaluate(order, cup);

        if (result.quality == ServeQuality.Wrong)
        {
            Patience -= maxPatience * 0.15f;
            Say(CustomerProfiles.Line(Profile.wrong, order) + "\n" + result.Summary, 3f);
            Emote(CustomerEmote.Kind.Meh);
            Gesture(NPCAnimDriver.Gesture.Angry, 1.4f);
            if (ShopProgress.Instance != null)
                ShopProgress.Instance.RegisterRejected(this);
            return false;
        }

        if (claimedBy != null) claimedBy.CustomerServedByPlayer(this);       // the owner got there first
        Accept(result, 1f);
        return true;
    }

    // The barista hands this customer their drink (they were waiting further back in line)
    public void ServedByStaff(ServeQuality quality)
    {
        claimedBy = null;
        ServeResult result = new ServeResult { quality = quality };
        if (quality != ServeQuality.Perfect) result.mistakes.Add(Loc.T("พนักงานทำไม่เป๊ะ", "the barista's drink was a bit off"));
        if (ShopProgress.Instance != null) ShopProgress.Instance.today.staffDrinks++;
        StatsSystem.Add(StatsSystem.Stat.StaffDrink);
        Accept(result, 0.8f);
    }

    // pay + tip, thanks, a sip and off they go
    void Accept(ServeResult result, float tipScale)
    {
        if (ShopProgress.Instance != null)
        {
            int pay = Mathf.RoundToInt(order.Price * Profile.payMultiplier);
            int tip = result.quality == ServeQuality.Perfect
                ? Mathf.RoundToInt(Profile.tipBase * tipScale * (0.5f + PatienceNormalized) * (regular != null && RegularsSystem.Instance != null ? RegularsSystem.Instance.TipMultiplier(regular) : 1f))
                : 0;
            ShopProgress.Instance.RegisterServe(this, result, pay, tip);
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.AddMoney(payment);
        }

        Emote(result.quality == ServeQuality.Perfect ? CustomerEmote.Kind.Love : CustomerEmote.Kind.Meh);
        if (customerType == CustomerType.Influencer && emote != null) emote.TakePhoto();   // the reviewer posts it
        if (Anim != null)
        {
            DrinkMenu.Drink d = DrinkMenu.Get(order.flavor);
            if (d != null) Anim.SetCupColor(d.color);
            Gesture(NPCAnimDriver.Gesture.Sip);                          // walks off sipping the new drink
        }
        if (regular != null && RegularsSystem.Instance != null)
        {
            RegularsSystem.Instance.OnServed(regular, result.quality);
            Say(RegularsSystem.Instance.Thanks(regular, result.quality), 3f);
        }
        else
            Say(CustomerProfiles.Line(result.quality == ServeQuality.Perfect ? Profile.thanks : Profile.okay, order), 3f);
        Debug.Log("Order completed: " + result.quality);
        if (!TrySit()) GoToExitArea();
    }

    // ---------------------------------------------------------------- sitting down with the drink
    // chance a served customer sits down when a seat is free (more when it rains)
    public static float SitChance = 0.6f;
    public static int SatDown;                          // read by tests
    SeatSpot seat;
    List<Vector3> seatPath;
    int routeIndex;
    float sitTimer;
    float standDelay;
    const float RouteSpeed = 1.1f;

    public bool IsSeated => state == CustomerState.Seated;
    public bool InSeatFlow => state >= CustomerState.GoingToSeat && state <= CustomerState.SeatRouteOut;
    public SeatSpot Seat => seat;

    bool TrySit()
    {
        if (agent == null || !agent.isOnNavMesh) return false;
        bool rain = WeatherSystem.Instance != null && WeatherSystem.Instance.IsRaining;
        if (Random.value > SitChance * (rain ? 1.35f : 1f)) return false;
        SeatSpot s = SeatSpot.FindFree(transform.position);
        if (s == null) return false;
        Queue.Remove(this);
        StopIncoming();
        seat = s;
        s.taken = this;
        seatPath = s.Path();
        agent.updateRotation = true;
        agent.avoidancePriority = 55;
        stuckTimer = 0f;
        state = CustomerState.GoingToSeat;
        agent.SetDestination(seatPath[0]);
        return true;
    }

    void UpdateSeat()
    {
        switch (state)
        {
            case CustomerState.GoingToSeat:
                Vector3 d = seatPath[0] - transform.position;
                d.y = 0f;
                bool there = d.magnitude < 0.35f || (!agent.pathPending && agent.remainingDistance < 0.3f && d.magnitude < 0.8f);
                if (!there && StuckWalking()) there = true;              // blocked: step over to the door
                if (!there) return;
                agent.enabled = false;                                   // inside the shop: walk the route by hand
                SetIndoors(seat.covered);
                routeIndex = 1;
                state = CustomerState.SeatRouteIn;
                return;

            case CustomerState.SeatRouteIn:
                if (!WalkTo(seatPath[routeIndex])) return;
                if (++routeIndex < seatPath.Count) return;
                transform.position = seat.SeatPosition;
                state = CustomerState.Seated;
                SatDown++;
                if (Anim != null) { Anim.SpeedOverride = 0f; Anim.Sitting = true; }
                Gesture(NPCAnimDriver.Gesture.None);                     // settle onto the stool first, then sip
                sitTimer = Random.Range(14f, 26f);
                gestureTimer = 1.5f;
                return;

            case CustomerState.Seated:
                transform.rotation = Quaternion.RotateTowards(transform.rotation, seat.Facing, 300f * Time.deltaTime);
                sitTimer -= Time.deltaTime;
                gestureTimer -= Time.deltaTime;
                if (gestureTimer <= 0f && Anim != null)
                {
                    float r = Random.value;                              // sip, scroll the phone, or just sit back
                    if (r < 0.55f) { Gesture(NPCAnimDriver.Gesture.Sip, Random.Range(3f, 5f)); gestureTimer = Random.Range(6f, 9f); }
                    else if (r < 0.8f) { Gesture(NPCAnimDriver.Gesture.Phone, Random.Range(4f, 7f)); gestureTimer = Random.Range(7f, 10f); }
                    else gestureTimer = Random.Range(3f, 5f);
                }
                if (sitTimer <= 0f) StandUp();
                return;

            case CustomerState.SeatRouteOut:
                if (standDelay > 0f) { standDelay -= Time.deltaTime; return; }   // getting up off the stool
                if (!WalkTo(seatPath[routeIndex])) return;
                if (--routeIndex >= 0) return;
                LeaveSeat();
                return;
        }
    }

    void StandUp()
    {
        if (state == CustomerState.GoingToSeat)
        {
            ReleaseSeat();
            GoToWaypoints();
            return;
        }
        if (Anim != null) { Anim.Sitting = false; Gesture(NPCAnimDriver.Gesture.Sip); }
        standDelay = state == CustomerState.Seated ? 0.45f : 0f;
        routeIndex = state == CustomerState.Seated ? seatPath.Count - 2 : Mathf.Max(0, routeIndex - 1);
        state = CustomerState.SeatRouteOut;
    }

    void LeaveSeat()
    {
        if (Anim != null) Anim.SpeedOverride = -1f;
        SetIndoors(false);
        ReleaseSeat();
        agent.enabled = true;
        agent.Warp(seatPath[0]);
        agent.updateRotation = true;
        stuckTimer = 0f;
        GoToWaypoints();
    }

    void ReleaseSeat()
    {
        if (seat != null && seat.taken == this) seat.taken = null;
        seat = null;
    }

    // straight-line walk with the agent off; true once there
    bool WalkTo(Vector3 target)
    {
        Vector3 to = target - transform.position;
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        float step = RouteSpeed * Time.deltaTime;
        if (Anim != null) Anim.SpeedOverride = RouteSpeed;
        if (flat.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat.normalized), 420f * Time.deltaTime);
        if (to.magnitude <= step)
        {
            transform.position = target;
            if (Anim != null) Anim.SpeedOverride = 0f;
            return true;
        }
        transform.position += to.normalized * step;
        return false;
    }

    void SetIndoors(bool indoors)
    {
        NPCWeather w = GetComponentInChildren<NPCWeather>();
        if (w != null) w.Indoors = indoors;
    }
}
