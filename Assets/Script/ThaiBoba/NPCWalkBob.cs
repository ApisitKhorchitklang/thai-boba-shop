using UnityEngine;
using UnityEngine.AI;

// Cheap procedural walk for the static NPC models: bob up/down and sway while the agent moves.
public class NPCWalkBob : MonoBehaviour
{
    public float bobHeight = 0.05f;
    public float swayDegrees = 4f;
    public float stepsPerMeter = 1.6f;

    NavMeshAgent agent;
    Vector3 baseLocalPos;
    float phase;

    void Start()
    {
        agent = GetComponentInParent<NavMeshAgent>();
        baseLocalPos = transform.localPosition;
        phase = Random.value * 10f;
    }

    void Update()
    {
        float speed = agent != null ? agent.velocity.magnitude : 0f;
        float amount = Mathf.Clamp01(speed / 1.2f);
        phase += speed * stepsPerMeter * Mathf.PI * Time.deltaTime;

        transform.localPosition = baseLocalPos + Vector3.up * Mathf.Abs(Mathf.Sin(phase)) * bobHeight * amount;
        transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase) * swayDegrees * amount);
    }
}
