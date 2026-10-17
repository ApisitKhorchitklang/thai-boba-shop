using UnityEngine;

// Rainy nights: street lamps, shop signs and neon throw long shimmering reflections across the wet road and
// footpath, stretched toward the viewer. Each source gets one soft additive streak on the ground under it.
public class NightReflections : MonoBehaviour
{
    public Material glowMaterial;
    public Transform[] sources;
    public Color[] colors;
    public float length = 6f, width = 0.6f;

    Renderer[] streaks;
    float[] groundY;
    float[] flicker;
    bool on;

    void Start()
    {
        if (glowMaterial == null || sources == null) return;
        streaks = new Renderer[sources.Length];
        groundY = new float[sources.Length];
        flicker = new float[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null) continue;
            streaks[i] = NightFX.MakeQuad("WetReflection_" + i, transform, glowMaterial).GetComponent<Renderer>();
            streaks[i].enabled = false;
            Vector3 p = sources[i].position;
            groundY[i] = Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 12f) ? hit.point.y + 0.02f : 0.02f;
            flicker[i] = Random.value * 10f;
        }
    }

    void LateUpdate()
    {
        if (streaks == null) return;
        float k = NightFX.Night01 * NightFX.Wet01;
        bool show = k > 0.04f;
        if (show != on)
        {
            on = show;
            foreach (Renderer r in streaks) if (r != null) r.enabled = show;
        }
        if (!show) return;
        float t = Time.time;
        for (int i = 0; i < streaks.Length; i++)
        {
            if (streaks[i] == null) continue;
            float shimmer = 0.85f + 0.15f * Mathf.Sin(t * 2.3f + flicker[i]) * Mathf.Sin(t * 3.7f + flicker[i] * 2f);
            NightFX.OrientStreak(streaks[i].transform, sources[i].position, groundY[i], length * (0.7f + 0.3f * k), width);
            Color c = colors != null && i < colors.Length ? colors[i] : new Color(1f, 0.8f, 0.5f);
            NightFX.SetColor(streaks[i], c * 0.5f * k * shimmer);
        }
    }
}
