using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// A stool a served customer can sit on with their drink (the seating upgrade out front, the tables in the shop next
// door after the expansion). The customer walks the NavMesh to the door point, then follows the route inside
// (straight lines, NavMeshAgent off), sits facing the table, and walks the same way back out.
public class SeatSpot : MonoBehaviour
{
    public static readonly List<SeatSpot> All = new List<SeatSpot>();

    [Tooltip("Faces this (the table)")]
    public Transform table;
    [Tooltip("Inside the shop: footpath point the customer walks to first (on the NavMesh)")]
    public bool hasDoor;
    public Vector3 door;
    [Tooltip("World points from the door to the stool (the stool itself is added at the end)")]
    public Vector3[] route = new Vector3[0];
    [Tooltip("Under a roof or an umbrella: umbrellas close while sitting here")]
    public bool covered = true;

    [System.NonSerialized] public CustomerController taken;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); if (taken != null) taken = null; }

    public bool Free => taken == null && isActiveAndEnabled;

    // root of the sitting customer: on the floor at the stool, facing the table
    public Vector3 SeatPosition => transform.position;

    public Quaternion Facing
    {
        get
        {
            Vector3 f = table != null ? table.position - transform.position : transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f.normalized) : transform.rotation;
        }
    }

    // stools out front (the seating set can be moved): step up to the stool from the side away from the table
    Vector3 OutsideApproach
    {
        get
        {
            Vector3 away = transform.position - (table != null ? table.position : transform.position - transform.forward);
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : -transform.forward;
            return transform.position + away * 0.55f;
        }
    }

    // where the NavMesh walk ends
    public Vector3 Entry
    {
        get
        {
            Vector3 p = hasDoor ? door : OutsideApproach;
            return NavMesh.SamplePosition(p, out NavMeshHit hit, 1.2f, NavMesh.AllAreas) ? hit.position : p;
        }
    }

    // the walk from the entry to the stool (entry first, stool last)
    public List<Vector3> Path()
    {
        var path = new List<Vector3> { Entry };
        if (hasDoor) path.AddRange(route);
        path.Add(SeatPosition);
        return path;
    }

    public static SeatSpot FindFree(Vector3 from)
    {
        SeatSpot best = null;
        float bestD = float.MaxValue;
        foreach (SeatSpot s in All)
        {
            if (!s.Free) continue;
            float d = (s.transform.position - from).sqrMagnitude + Random.Range(0f, 6f);     // not always the nearest
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    public static int Sitting
    {
        get { int n = 0; foreach (SeatSpot s in All) if (s.taken != null && s.taken.IsSeated) n++; return n; }
    }
}
