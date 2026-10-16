using UnityEngine;

// Bot that plays the counter: makes each waiting customer's drink through the real stations and serves it.
// Used by the PlayMode test; also handy to watch the full loop (add it to any object in ThaiBobaStreet).
public class ThaiBobaAutoPlayer : MonoBehaviour
{
    public int customersToServe = 3;
    public float stepSeconds = 0.3f;
    public float speedUp = 4f;
    public bool endDayWhenDone = true;

    // Read by the test: null while running
    public static bool? Passed;
    public static string Report = "";

    int served;
    int moneyAtStart = -1;
    CupController cup;
    float nextAction;
    float nextDiag;

    void Start()
    {
        Passed = null;
        Report = "";
        Time.timeScale = speedUp;
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (Passed.HasValue || Time.time < nextAction) return;
        nextAction = Time.time + stepSeconds;

        RaycastAim aim = FindFirstObjectByType<RaycastAim>();
        GameManager gm = GameManager.Instance;
        if (aim == null || gm == null) return;
        if (moneyAtStart < 0 && Time.time > 1f) moneyAtStart = gm.money;

        CustomerController c = CustomerController.Front;
        if (Time.time > nextDiag)
        {
            nextDiag = Time.time + 4f;
            if (c != null)
            {
                var ag = c.GetComponent<UnityEngine.AI.NavMeshAgent>();
                Debug.Log($"[BOT] diag: queue {CustomerController.Queue.Count}, front at {c.transform.position}, waiting {c.IsWaitingAtCounter}, " +
                          $"path {ag.pathStatus} hasPath {ag.hasPath} remaining {ag.remainingDistance:0.0} dest {ag.destination} onMesh {ag.isOnNavMesh}");
            }
            else
            {
                CustomerController[] all = FindObjectsByType<CustomerController>(FindObjectsSortMode.None);
                string extra = "";
                if (all.Length > 0)
                {
                    var ag = all[0].GetComponent<UnityEngine.AI.NavMeshAgent>();
                    extra = $" first at {all[0].transform.position} path {ag.pathStatus} remaining {ag.remainingDistance:0.0} dest {ag.destination} onMesh {ag.isOnNavMesh} vel {ag.velocity.magnitude:0.0}";
                }
                Debug.Log($"[BOT] diag: no customer in queue, customers alive {all.Length}{extra}");
            }
        }
        if (c == null || !c.IsWaitingAtCounter) return;
        Order o = c.order;

        // an empty / hard pot gets cooked before anything else (what the tutorial teaches)
        BobaPot firstPot = FindFirstObjectByType<BobaPot>();
        if (o.bobaCount > 0 && (cup == null || cup.bobaCount < o.bobaCount) && firstPot != null &&
            (firstPot.Current == BobaPot.State.Empty || firstPot.Current == BobaPot.State.Stale) && (cup == null || !cup.CanAddBoba || firstPot.Current == BobaPot.State.Empty))
        {
            Use(aim, Station(IngredientStation.Kind.Boba)); return;
        }
        if (cup == null)
        {
            Use(aim, FindFirstObjectByType<CupDispenser>());
            cup = aim.HeldCup;
            if (cup == null) { Done(false, "dispenser did not put a cup in hand"); return; }
            Debug.Log($"[BOT] {c.customerType} wants {o}");
            return;
        }

        // pearls have to be cooked first (the pot starts every day empty)
        BobaPot pot = FindFirstObjectByType<BobaPot>();
        if (o.bobaCount > cup.bobaCount && pot != null && !pot.Ready)
        {
            if (!pot.IsCooking) { Use(aim, Station(IngredientStation.Kind.Boba)); return; }      // one action per step, like a player
            if (cup.CanFill) Use(aim, Station(IngredientStation.Kind.Flavor, o.flavor));          // prep while it boils
            return;
        }
        // Tea first, then pearls/ice/sweetness: checks that ingredients can be added in any order before sealing
        if (cup.CanFill) { Use(aim, Station(IngredientStation.Kind.Flavor, o.flavor)); return; }
        if (cup.bobaCount < o.bobaCount)
        {
            if (!cup.CanAddBoba) { Done(false, "can't add pearls after pouring tea"); return; }
            Use(aim, Station(IngredientStation.Kind.Boba)); return;
        }
        if (o.wantsIce && !cup.hasIce)
        {
            if (!cup.CanAddIce) { Done(false, "can't add ice after pouring tea"); return; }
            Use(aim, Station(IngredientStation.Kind.Ice)); return;
        }
        if (cup.sweetness != o.sweetness && cup.CanChangeSweetness) { Use(aim, Station(IngredientStation.Kind.Sweetness)); return; }
        if (o.wantsFoam && !cup.hasFoam) { Use(aim, Station(IngredientStation.Kind.Foam)); return; }
        if (o.blended && !cup.blended) { Use(aim, Station(IngredientStation.Kind.Blend)); return; }
        if (o.marshmallow && !cup.marshmallow) { Use(aim, Station(IngredientStation.Kind.Marshmallow)); return; }
        if (cup.CanSeal) { Use(aim, FindFirstObjectByType<SealerStation>()); return; }
        if (cup.State != CupController.CupState.LidClosed) return; // sealer still working

        if (!Use(aim, FindFirstObjectByType<ServeStation>())) return;
        served++;
        cup = null;
        Debug.Log($"[BOT] served #{served}: money {gm.money}, combo {ShopProgress.Instance.combo}, rep {ShopProgress.Instance.reputation:0.00}");

        if (served >= customersToServe)
        {
            ShopProgress.DayStats day = ShopProgress.Instance.today;
            TimeCyc tc = FindFirstObjectByType<TimeCyc>();
            int dayBefore = tc.Days;
            if (endDayWhenDone) gm.EndDay();
            bool ok = day.perfect == customersToServe && gm.money > moneyAtStart && (!endDayWhenDone || tc.Days == dayBefore + 1);
            Done(ok, $"perfect {day.perfect}/{customersToServe}, revenue {day.revenue}, tips {day.tips}, " +
                     $"money {moneyAtStart} -> {gm.money}, day {dayBefore} -> {tc.Days}, " +
                     $"event '{DailyEventSystem.Instance.Current.Title}'\n{DailyGoals.Instance.Describe()}");
        }
    }

    // Like a player: stand in front of the station, aim the crosshair at it and press Interact (real raycast)
    bool Use(RaycastAim aim, Component station)
    {
        if (Passed.HasValue) return false;
        Collider col = station.GetComponentInChildren<Collider>();
        Vector3 target = col.bounds.center;
        Transform player = aim.Player;
        CharacterController cc = player.GetComponent<CharacterController>();
        foreach (Behaviour b in new Behaviour[] { player.GetComponent<MovementWithJoy>(), aim.GetComponent<CameraMobile>() })
            if (b != null) b.enabled = false;   // the bot drives the camera itself

        // side-wall stations (fridge, seasonal cart, ice cooler, computer) are approached from the middle of the shop,
        // front/back counters from in front of them
        Vector3 stand;
        if (Mathf.Abs(target.x) > 1.4f) stand = new Vector3(target.x - Mathf.Sign(target.x) * 1.0f, player.position.y, target.z);
        else stand = new Vector3(target.x, player.position.y, target.z < -4f ? target.z + 1.1f : target.z - 1.0f);
        if (cc != null) cc.enabled = false;
        player.position = stand;
        if (cc != null) cc.enabled = true;
        Transform cam = aim.CameraMain;
        cam.rotation = Quaternion.LookRotation(target - cam.position);

        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
        if (!Physics.Raycast(ray, out RaycastHit hit, aim.maxDistance, aim.layerMask) ||
            hit.collider.GetComponentInParent<IInteractable>() != (IInteractable)station)
        {
            Done(false, $"crosshair on {station.name} hits '{(hit.collider != null ? hit.collider.name : "nothing")}' instead");
            return false;
        }
        aim.Interact();
        return true;
    }

    IngredientStation Station(IngredientStation.Kind kind, string flavor = null)
    {
        foreach (IngredientStation s in FindObjectsByType<IngredientStation>(FindObjectsSortMode.None))
            if (s.kind == kind && (flavor == null || s.flavorId == flavor)) return s;
        return null;
    }

    void Done(bool ok, string report)
    {
        Report = report;
        Passed = ok;
        Debug.Log((ok ? "[BOT] PASS " : "[BOT] FAIL ") + report);
    }
}
