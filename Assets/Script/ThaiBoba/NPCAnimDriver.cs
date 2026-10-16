using UnityEngine;
using UnityEngine.AI;

// Feeds NavMeshAgent speed into the NPC Animator ("Speed" float: Idle <-> Walk) and syncs the walk cycle speed.
public class NPCAnimDriver : MonoBehaviour
{
    [Tooltip("Agent speed (m/s) at which the walk clip plays at normal speed")]
    public float walkClipSpeed = 1.3f;

    NavMeshAgent agent;
    Animator animator;

    void Start()
    {
        agent = GetComponentInParent<NavMeshAgent>();
    }

    void Update()
    {
        // CustomerAppearance enables one model on Start, so look up the active Animator lazily
        if (animator == null || !animator.isActiveAndEnabled)
        {
            animator = null;
            foreach (Animator a in GetComponentsInChildren<Animator>())
                if (a.isActiveAndEnabled) { animator = a; break; }
            if (animator == null) return;
            animator.applyRootMotion = false;
        }

        float speed = agent != null ? agent.velocity.magnitude : 0f;
        animator.SetFloat("Speed", speed);
        animator.speed = speed > 0.2f ? Mathf.Clamp(speed / walkClipSpeed, 0.6f, 1.8f) : 1f;
    }
}
