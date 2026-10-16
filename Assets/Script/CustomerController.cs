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
    public static CustomerController Front => Queue.Count > 0 ? Queue[0] : null;
    // Tutorial: nobody gets impatient while the player is learning
    public static bool PatienceFrozen;

    public float Patience { get; private set; }
    public float PatienceNormalized => maxPatience > 0 ? Mathf.Clamp01(Patience / maxPatience) : 1f;
    public bool IsWaitingAtCounter => state == CustomerState.GoingToOrder && arrivedAtSlot && Front == this;
    public CustomerProfiles.Profile Profile { get; private set; }

    private enum CustomerState
    {
        GoingToEnter,
        GoingToOrder,
        GoingToExit,
        GoingToWaypoints,
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

        state = CustomerState.GoingToEnter;
        if (enterArea != null)
            GoToEnterArea();
        else if (orderArea != null)
            GoToOrderArea();

        Say("");
        Debug.Log($"{Profile.Label} spawned with {order}");
    }

    void OnDestroy()
    {
        Queue.Remove(this);
    }

    void Update()
    {
        UpdateSpeech();

        if (agent == null || state == CustomerState.Finished)
            return;

        if (state == CustomerState.GoingToEnter && enterArea != null)
        {
            // Safety net: if something blocks the footpath, hop to the shop entrance instead of freezing
            stuckTimer = agent.velocity.sqrMagnitude < 0.01f && !agent.pathPending ? stuckTimer + Time.deltaTime : 0f;
            if (stuckTimer > 5f && Vector3.Distance(transform.position, enterArea.position) > 1f)
            {
                stuckTimer = 0f;
                agent.Warp(enterArea.position);
                GoToEnterArea();
            }
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                if (Vector3.Distance(transform.position, enterArea.position) < 0.5f)
                {
                    if (exitArea != null)
                        transform.position = exitArea.position;

                    GoToOrderArea();
                }
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
    Vector3 SlotPosition(int slot)
    {
        return orderArea.position - orderArea.forward * queueSpacing * slot;
    }

    void UpdateQueue()
    {
        int slot = Queue.IndexOf(this);
        if (slot != queueSlot)
        {
            queueSlot = slot;
            arrivedAtSlot = false;
            agent.SetDestination(SlotPosition(slot));
        }

        // pushed around by the people behind or blocked by a prop: close enough counts, otherwise hop to the spot
        if (!arrivedAtSlot)
        {
            stuckTimer = agent.velocity.sqrMagnitude < 0.01f && !agent.pathPending ? stuckTimer + Time.deltaTime : 0f;
            if (stuckTimer > 3f)
            {
                stuckTimer = 0f;
                if (Vector3.Distance(transform.position, SlotPosition(slot)) > 1.2f) agent.Warp(SlotPosition(slot));
                arrivedAtSlot = slot >= 0;
                if (arrivedAtSlot) OnArrivedAtSlot(slot);
            }
        }

        if (!arrivedAtSlot && !agent.pathPending && agent.remainingDistance < 0.5f)
        {
            arrivedAtSlot = true;
            OnArrivedAtSlot(slot);
        }

        // Only the customer being served (at the counter) loses patience; people further back in line just wait
        if (slot == 0 && arrivedAtSlot && !PatienceFrozen)
            Patience -= Time.deltaTime;
        if (!warnedImpatient && slot == 0 && arrivedAtSlot && PatienceNormalized < 0.25f)
        {
            warnedImpatient = true;
            Emote(CustomerEmote.Kind.Angry, 2.5f);                       // hurry up!
        }

        if (Patience <= 0f)
            LeaveAngry();
    }

    void OnArrivedAtSlot(int slot)
    {
        if (slot == 0 && !greeted)
        {
            greeted = true;
            Say(regular != null && RegularsSystem.Instance != null ? RegularsSystem.Instance.Greeting(regular, order) : CustomerProfiles.Line(Profile.greet, order), 4f);
            if (regular != null) Emote(CustomerEmote.Kind.Happy);       // a familiar face is happy to be back
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

    public void GoToEnterArea()
    {
        if (agent == null || enterArea == null)
            return;

        state = CustomerState.GoingToEnter;
        agent.SetDestination(enterArea.position);
    }

    public void GoToOrderArea()
    {
        if (agent == null || orderArea == null)
            return;

        state = CustomerState.GoingToOrder;
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
    }

    void LeaveForTheDay()
    {
        if (state == CustomerState.GoingToExit || state == CustomerState.Finished) return;
        Say(Loc.T("ร้านปิดแล้วเหรอ ไว้มาใหม่นะ", "Closed already? See you tomorrow!"), 3f);
        GoToExitArea();
    }

    public void GoToExitArea()
    {
        Queue.Remove(this);
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
            if (ShopProgress.Instance != null)
                ShopProgress.Instance.RegisterRejected(this);
            return false;
        }

        if (ShopProgress.Instance != null)
        {
            int pay = Mathf.RoundToInt(order.Price * Profile.payMultiplier);
            int tip = result.quality == ServeQuality.Perfect
                ? Mathf.RoundToInt(Profile.tipBase * (0.5f + PatienceNormalized) * (regular != null && RegularsSystem.Instance != null ? RegularsSystem.Instance.TipMultiplier(regular) : 1f))
                : 0;
            ShopProgress.Instance.RegisterServe(this, result, pay, tip);
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.AddMoney(payment);
        }

        Emote(result.quality == ServeQuality.Perfect ? CustomerEmote.Kind.Love : CustomerEmote.Kind.Meh);
        if (customerType == CustomerType.Influencer && emote != null) emote.TakePhoto();   // the reviewer posts it
        if (regular != null && RegularsSystem.Instance != null)
        {
            RegularsSystem.Instance.OnServed(regular, result.quality);
            Say(RegularsSystem.Instance.Thanks(regular, result.quality), 3f);
        }
        else
            Say(CustomerProfiles.Line(result.quality == ServeQuality.Perfect ? Profile.thanks : Profile.okay, order), 3f);
        Debug.Log("Order completed: " + result.quality);
        GoToExitArea();
        return true;
    }
}
