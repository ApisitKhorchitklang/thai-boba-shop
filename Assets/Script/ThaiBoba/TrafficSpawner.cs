using UnityEngine;

// Keeps the soi busy: vehicles emerge from the tunnels, turn left/right into the street,
// drive past the shop and turn off into another tunnel where they disappear.
public class TrafficSpawner : MonoBehaviour
{
    public TrafficRoute[] routes;
    public GameObject[] vehiclePrefabs;
    [Tooltip("Relative chance of each prefab (same order). Empty = equal.")]
    public float[] weights;
    public Vector2 spawnInterval = new Vector2(2f, 5f);
    public Vector2 speedRange = new Vector2(6f, 10f);
    public int maxVehicles = 14;
    [Tooltip("Vehicles already on the street when the scene starts")]
    public int initialVehicles = 6;

    float timer;

    void Start()
    {
        for (int i = 0; i < initialVehicles * 3 && TrafficCar.All.Count < initialVehicles; i++)
        {
            TrafficRoute r = PickRoute(true);
            if (r != null) Spawn(r, Random.Range(0.3f, 0.6f) * r.Length);
        }
        timer = Random.Range(spawnInterval.x, spawnInterval.y);
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Random.Range(spawnInterval.x, spawnInterval.y);
        if (TrafficCar.All.Count >= maxVehicles) return;
        TrafficRoute r = PickRoute(false);
        if (r != null) Spawn(r, 0f);
    }

    TrafficRoute PickRoute(bool mainStreetOnly)
    {
        if (routes == null || routes.Length == 0) return null;
        float total = 0f;
        foreach (TrafficRoute r in routes) if (r != null && (!mainStreetOnly || r.passesShop)) total += r.weight;
        float x = Random.value * total;
        foreach (TrafficRoute r in routes)
        {
            if (r == null || (mainStreetOnly && !r.passesShop)) continue;
            x -= r.weight;
            if (x <= 0f) return r;
        }
        return null;
    }

    void Spawn(TrafficRoute route, float distance)
    {
        if (vehiclePrefabs == null || vehiclePrefabs.Length == 0 || route.points == null || route.points.Length < 2) return;

        // don't spawn on top of another vehicle
        GameObject go = Instantiate(PickPrefab(), transform);
        TrafficCar car = go.GetComponent<TrafficCar>();
        if (car == null) car = go.AddComponent<TrafficCar>();
        car.Init(route.points, distance, Random.Range(speedRange.x, speedRange.y));
        Vector3 f = go.transform.forward;
        foreach (TrafficCar o in TrafficCar.All)
            if (o != car && Vector3.Distance(o.transform.position, go.transform.position) < (o.length + car.length) * 0.5f + 2f &&
                Vector3.Dot(o.transform.forward, f) > 0.3f)
            {
                Destroy(go);
                return;
            }
    }

    GameObject PickPrefab()
    {
        if (weights == null || weights.Length != vehiclePrefabs.Length)
            return vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
        float total = 0f;
        foreach (float w in weights) total += w;
        float r = Random.value * total;
        for (int i = 0; i < weights.Length; i++)
        {
            r -= weights[i];
            if (r <= 0f) return vehiclePrefabs[i];
        }
        return vehiclePrefabs[vehiclePrefabs.Length - 1];
    }
}
