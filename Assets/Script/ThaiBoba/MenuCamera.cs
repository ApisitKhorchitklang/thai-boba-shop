using UnityEngine;

// Slow cinematic drift along the street behind the main menu (stays above the road, never inside buildings).
public class MenuCamera : MonoBehaviour
{
    [Tooltip("Height above the road")]
    public float height = 5.2f;
    [Tooltip("Distance out over the road (Unity z; the road runs z 3.2 .. 10.2)")]
    public float roadZ = 9.5f;
    [Tooltip("How far left/right the camera drifts along the street")]
    public float sweep = 14f;
    public float secondsPerSweep = 40f;
    public Vector3 focus = new Vector3(0f, 2f, 0f);

    float t;

    void OnEnable()
    {
        t = 0.15f;
    }

    void LateUpdate()
    {
        t += Time.unscaledDeltaTime / secondsPerSweep;
        float x = Mathf.Sin(t * Mathf.PI * 2f) * sweep;
        transform.position = new Vector3(x, height, roadZ);
        transform.LookAt(new Vector3(x * 0.35f + focus.x, focus.y, focus.z));
    }
}
