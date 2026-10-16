using UnityEngine;
using UnityEngine.AI;

// Keeps a footpath busy with passers-by (used for the opposite side of the street, where nobody shops).
public class AmbientPedestrianSpawner : MonoBehaviour
{
    public GameObject pedestrianPrefab;
    [Tooltip("The two ends of the footpath")]
    public Transform endA;
    public Transform endB;
    public Vector2 spawnInterval = new Vector2(3f, 7f);
    public int maxAlive = 7;
    [Tooltip("People already walking when the scene starts")]
    public int initialCount = 4;

    float timer;
    readonly System.Collections.Generic.List<GameObject> alive = new System.Collections.Generic.List<GameObject>();

    void Start()
    {
        for (int i = 0; i < initialCount; i++)
            Spawn(Vector3.Lerp(endA.position, endB.position, Random.Range(0.1f, 0.9f)));
        timer = Random.Range(spawnInterval.x, spawnInterval.y);
    }

    void Update()
    {
        alive.RemoveAll(g => g == null);
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Random.Range(spawnInterval.x, spawnInterval.y);
        if (alive.Count < maxAlive)
            Spawn(Random.value < 0.5f ? endA.position : endB.position);
    }

    void Spawn(Vector3 around)
    {
        if (pedestrianPrefab == null) return;
        Vector3 p = around + new Vector3(0f, 0f, Random.Range(-0.6f, 0.6f));
        if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return;
        alive.Add(Instantiate(pedestrianPrefab, hit.position, Quaternion.identity, transform));
    }
}
