using UnityEngine;

// A path for traffic (world-space points): out of a tunnel, through the junctions, into another tunnel.
public class TrafficRoute : MonoBehaviour
{
    public Vector3[] points;
    [Tooltip("Relative chance of this route being picked")]
    public float weight = 1f;
    [Tooltip("Route uses the main street past the shop (used for vehicles already driving at scene start)")]
    public bool passesShop = true;

    public float Length
    {
        get
        {
            float l = 0f;
            for (int i = 1; i < points.Length; i++) l += Vector3.Distance(points[i - 1], points[i]);
            return l;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (points == null) return;
        Gizmos.color = Color.yellow;
        for (int i = 1; i < points.Length; i++) Gizmos.DrawLine(points[i - 1], points[i]);
    }
#endif
}
