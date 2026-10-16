using System.Collections;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public GameObject npcPrefab;
    public GameObject customerPrefab;

    public Transform[] spawnPoints;
    public Transform enterArea;
    public Transform orderArea;
    public Transform exitArea;
    public Transform[] customerWaypoints;

    public int maxCharacters = 20;
    private int currentCharacters;

    [Tooltip("Base seconds between spawns. Divided by reputation / daily event / rush-hour multipliers.")]
    public float spawnInterval = 5f;
    [Tooltip("Customers stop coming (pedestrians only) while this many are already queuing.")]
    public int maxQueue = 5;
    public TimeCyc timeCyc;
    [Tooltip("Shop opening hours (customers only spawn between these)")]
    public int openHour = 8;
    public int closeHour = 21;

    bool started;

    // Tutorial (day 1): exactly one customer with a fixed, easy order; everyone else just walks past
    public static bool TutorialActive;
    public static Order TutorialOrder;
    public static bool TutorialCustomerSpawned;

    private void Start()
    {
        if (timeCyc == null)
            timeCyc = FindFirstObjectByType<TimeCyc>();

        started = true;
        StartCoroutine(SpawnLoop());
        if (timeCyc != null) timeCyc.OnNewDay += NewDay;
        ShopProgress.OnDaySummary += ClosingTime;
    }

    void OnDestroy()
    {
        if (timeCyc != null) timeCyc.OnNewDay -= NewDay;
        ShopProgress.OnDaySummary -= ClosingTime;
    }

    // Shop closes: the line goes home. Next morning: nobody from yesterday is left standing around.
    void ClosingTime(ShopProgress.DayStats stats) { CustomerController.SendAllHome(false); }
    void NewDay(int day) { CustomerController.SendAllHome(true); }

    // Coroutines die when the object is disabled or scripts are reloaded mid-play (editing code while playing):
    // start the loop again so customers keep coming.
    private void OnEnable()
    {
        if (started) StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(1f);
        while (true)
        {
            SpawnCharacter();
            float mult = CurrentSpawnMultiplier();
            yield return new WaitForSeconds(spawnInterval / Mathf.Max(0.25f, mult));
        }
    }

    // Thai rush hours: morning commute, lunch, after school
    float RushHourMultiplier(int h)
    {
        if (h >= 7 && h <= 8) return 1.4f;
        if (h >= 11 && h <= 12) return 1.6f;
        if (h >= 15 && h <= 17) return 1.5f;
        return 1f;
    }

    float CurrentSpawnMultiplier()
    {
        int h = timeCyc != null ? timeCyc.Hours : 12;
        float mult = RushHourMultiplier(h);
        if (ShopProgress.Instance != null)
            mult *= ShopProgress.Instance.SpawnRateMultiplier;
        if (DailyEventSystem.Instance != null)
            mult *= DailyEventSystem.Instance.HourSpawnMultiplier(h);
        if (WeatherSystem.Instance != null)
            mult *= 1f + 0.8f * WeatherSystem.Instance.Rain;      // people duck into the shop to escape the rain
        return mult;
    }

    void SpawnCharacter()
    {
        if (!GameMenus.GameRunning) return;     // main menu is up

        // Only spawn customers during business hours
        int h = 12;
        if (timeCyc != null)
        {
            h = timeCyc.Hours;
            if (h < openHour || h > closeHour) return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0) return;

        Transform spawn = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject prefab = npcPrefab;

        // 60% chance to spawn a customer prefab if available (and the queue isn't full)
        bool queueFull = CustomerController.Queue.Count >= maxQueue;
        bool tutorialCustomer = false;
        if (TutorialActive)
        {
            tutorialCustomer = !TutorialCustomerSpawned && customerPrefab != null;
            prefab = tutorialCustomer ? customerPrefab : npcPrefab;
        }
        else if (customerPrefab != null && !queueFull && (npcPrefab == null || Random.value < 0.6f))
            prefab = customerPrefab;

        if (prefab == null) return;

        GameObject spawnedObject = Instantiate(prefab, spawn.position, spawn.rotation);

        CustomerController customerController = spawnedObject.GetComponent<CustomerController>();
        if (customerController != null)
        {
            customerController.customerType = CustomerProfiles.Roll(h);
            if (tutorialCustomer)
            {
                TutorialCustomerSpawned = true;
                customerController.customerType = CustomerType.Student;
                customerController.order = new Order(TutorialOrder.bobaCount, TutorialOrder.wantsIce, TutorialOrder.flavor, TutorialOrder.sweetness);
            }
            customerController.AssignSpawnPoint(spawn);
            customerController.AssignEnterArea(enterArea);
            customerController.AssignOrderArea(orderArea);
            customerController.AssignExitArea(exitArea);
            customerController.AssignWaypoints(customerWaypoints);
        }

        currentCharacters++;
    }
}
