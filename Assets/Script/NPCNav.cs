using UnityEngine;
using UnityEngine.AI;

public class NPCNav : MonoBehaviour
{
    public Transform[] points;

    private NavMeshAgent agent;
    private int currentPoint;

    void Start()
    {
        points = GameObject.Find("Waypoint").GetComponentsInChildren<Transform>();
        agent = GetComponent<NavMeshAgent>();
        agent.SetDestination(points[0].position);
        
    }

    void Update()
    {
        if (!agent.pathPending &&
            agent.remainingDistance < 0.5f)
        {
            currentPoint++;

            if (currentPoint >= points.Length)
            {
                Destroy(gameObject);
                return;
            }

            agent.SetDestination(points[currentPoint].position);
        }
    }
}
