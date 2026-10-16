using UnityEngine;

public class CustomerMovement : MonoBehaviour
{
    public Transform target;
    public float speed = 1.5f;
    public float stopDistance = 0.5f;

    void Update()
    {
        if (target == null) return;
        Vector3 dir = target.position - transform.position;
        float dist = dir.magnitude;
        if (dist > stopDistance)
        {
            Vector3 move = dir.normalized * speed * Time.deltaTime;
            transform.position += move;
            transform.forward = Vector3.Lerp(transform.forward, dir.normalized, 0.1f);
        }
    }
}
