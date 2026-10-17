using UnityEngine;
using UnityEngine.AI;

// Passer-by: walks to the far end of the footpath it spawned on and disappears.
[RequireComponent(typeof(NavMeshAgent))]
public class PedestrianWalker : MonoBehaviour
{
    public Vector2 speedRange = new Vector2(1.0f, 1.6f);

    NavMeshAgent agent;
    bool started;
    float stuckTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = Random.Range(speedRange.x, speedRange.y);

        PedestrianEnd[] ends = FindObjectsByType<PedestrianEnd>(FindObjectsSortMode.None);
        // which footpath am I on? -> the group of the nearest end
        PedestrianEnd nearest = null;
        foreach (PedestrianEnd e in ends)
            if (nearest == null || Dist(e) < Dist(nearest)) nearest = e;
        if (nearest == null) { Destroy(gameObject, 20f); return; }

        // walk to the end of the same footpath that is farthest away
        PedestrianEnd target = null;
        foreach (PedestrianEnd e in ends)
            if (e.group == nearest.group && (target == null || Dist(e) > Dist(target))) target = e;

        agent.SetDestination(target.transform.position + Vector3.right * Random.Range(-0.4f, 0.4f));
        started = true;
        // some people walk along looking at their phone (not with an umbrella up)
        NPCAnimDriver anim = GetComponentInChildren<NPCAnimDriver>();
        bool raining = WeatherSystem.Instance != null && WeatherSystem.Instance.IsRaining;
        if (anim != null && !raining && Random.value < 0.18f) anim.Play(NPCAnimDriver.Gesture.Phone);
    }

    float Dist(PedestrianEnd e)
    {
        return Vector3.Distance(transform.position, e.transform.position);
    }

    void Update()
    {
        if (started && !agent.pathPending && agent.remainingDistance < 0.6f)
            Destroy(gameObject);
        // blocked somewhere: leave quietly rather than standing frozen
        stuckTimer = started && agent.velocity.sqrMagnitude < 0.01f ? stuckTimer + Time.deltaTime : 0f;
        if (stuckTimer > 4f)
            Destroy(gameObject);
    }
}
