using System.Collections.Generic;
using UnityEngine;

// A vehicle following a TrafficRoute: slows for corners, keeps distance from the vehicle ahead,
// and despawns at the end of the route (deep inside a tunnel).
public class TrafficCar : MonoBehaviour
{
    public float cruiseSpeed = 8f;
    public float cornerSpeed = 4.5f;
    public float acceleration = 4f;
    public float brakeDistance = 7f;
    public float length = 4.5f;

    public float Speed { get; private set; }

    public static readonly List<TrafficCar> All = new List<TrafficCar>();

    Vector3[] path;
    int next;               // index of the waypoint we're heading to

    VehicleSound sound;

    void Awake() { sound = GetComponent<VehicleSound>(); }

    public void Init(Vector3[] routePoints, float startDistance, float speed)
    {
        path = routePoints;
        cruiseSpeed = speed;
        Speed = speed;

        // place along the route
        Vector3 pos = path[0];
        next = 1;
        float d = startDistance;
        while (next < path.Length)
        {
            float seg = Vector3.Distance(path[next - 1], path[next]);
            if (d <= seg) { pos = Vector3.Lerp(path[next - 1], path[next], seg > 0 ? d / seg : 0); break; }
            d -= seg;
            next++;
        }
        if (next >= path.Length) { next = path.Length - 1; pos = path[next]; }
        Vector3 dir = (path[next] - pos).sqrMagnitude > 0.0001f ? (path[next] - pos).normalized : (path[next] - path[next - 1]).normalized;
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));

        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) if (!(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds);
        length = Mathf.Max(1.5f, Vector3.Dot(b.size, new Vector3(Mathf.Abs(dir.x), 0, Mathf.Abs(dir.z))));
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // Free road ahead: only vehicles roughly in front of us and going roughly the same way count,
    // so cars never wait for each other head-on in a junction.
    public static float GapAhead(Vector3 pos, Vector3 forward, float myLength, TrafficCar self = null)
    {
        float gap = float.MaxValue;
        foreach (TrafficCar o in All)
        {
            if (o == self) continue;
            Vector3 d = o.transform.position - pos;
            float along = Vector3.Dot(d, forward);
            if (along <= 0f) continue;
            float lateral = (d - forward * along).magnitude;
            if (lateral > 1.4f) continue;
            if (Vector3.Dot(o.transform.forward, forward) < 0.3f) continue;
            gap = Mathf.Min(gap, along - (myLength + o.length) * 0.5f);
        }
        return gap;
    }

    void Update()
    {
        if (path == null) return;
        Vector3 target = path[next];
        Vector3 to = target - transform.position;
        Vector3 fwd = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;

        // slow down before a bend in the route
        float desired = cruiseSpeed;
        if (next + 1 < path.Length)
        {
            Vector3 after = (path[next + 1] - target).normalized;
            float bend = 1f - Mathf.Clamp01(Vector3.Dot(fwd, after));
            if (bend > 0.02f && to.magnitude < 9f) desired = Mathf.Lerp(cruiseSpeed, cornerSpeed, Mathf.Clamp01(bend * 6f));
        }
        float gap = GapAhead(transform.position, transform.forward, length, this);
        if (gap < brakeDistance)
        {
            desired = Mathf.Min(desired, cruiseSpeed * Mathf.Clamp01((gap - 1.5f) / (brakeDistance - 1.5f)));
            if (gap < 3f && Speed > 2f && sound != null) sound.Honk();     // stuck behind someone: toot!
        }

        Speed = Mathf.MoveTowards(Speed, desired, (desired < Speed ? acceleration * 2.5f : acceleration) * Time.deltaTime);
        float step = Speed * Time.deltaTime;

        // advance along the polyline, possibly passing several waypoints in one frame
        Vector3 pos = transform.position;
        while (step > 0f && next < path.Length)
        {
            float dist = Vector3.Distance(pos, path[next]);
            if (step < dist) { pos = Vector3.MoveTowards(pos, path[next], step); step = 0f; }
            else { pos = path[next]; step -= dist; next++; }
        }
        if (next >= path.Length) { Destroy(gameObject); return; }

        Vector3 look = path[next] - pos;
        transform.position = pos;
        if (look.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look.normalized), Time.deltaTime * 6f);
    }
}
