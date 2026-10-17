using UnityEngine;

// After dark a traffic vehicle switches its lights on: a warm pool of light on the road ahead, glowing headlight
// and tail-light flares, and on a wet road their reflections stretch across the asphalt toward you.
// Light positions are measured from the lamp materials of the model by the scene builder.
public class VehicleLights : MonoBehaviour
{
    public Material glowMaterial;                       // additive soft glow (FX)
    public Vector3[] headlights = new Vector3[0];       // local positions
    public Vector3[] taillights = new Vector3[0];
    public float poolLength = 7f, poolWidth = 3.2f;
    public Color headColor = new Color(1f, 0.92f, 0.75f);
    public Color tailColor = new Color(1f, 0.08f, 0.05f);

    Renderer pool;
    Renderer[] heads, tails, streaks;
    Vector3[] streakSources;
    bool built;

    void Start() { Build(); }

    void Build()
    {
        if (built || glowMaterial == null) return;
        built = true;
        float front = 0f;
        foreach (Vector3 h in headlights) front = Mathf.Max(front, h.z);
        GameObject p = NightFX.MakeQuad("LightPool", transform, glowMaterial);
        p.transform.localPosition = new Vector3(0f, 0.03f, front + poolLength * 0.5f);
        p.transform.localScale = new Vector3(poolWidth, 1f, poolLength);
        pool = p.GetComponent<Renderer>();
        heads = new Renderer[headlights.Length];
        for (int i = 0; i < headlights.Length; i++) heads[i] = Flare("HeadFlare", headlights[i], 0.55f);
        tails = new Renderer[taillights.Length];
        for (int i = 0; i < taillights.Length; i++) tails[i] = Flare("TailFlare", taillights[i], 0.4f);
        int n = headlights.Length + taillights.Length;
        streaks = new Renderer[n];
        streakSources = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            streaks[i] = NightFX.MakeQuad("WetStreak", transform, glowMaterial).GetComponent<Renderer>();
            streakSources[i] = i < headlights.Length ? headlights[i] : taillights[i - headlights.Length];
        }
        SetAll(false);
    }

    // a small quad standing up, turned to the camera every frame
    Renderer Flare(string name, Vector3 local, float size)
    {
        GameObject f = NightFX.MakeQuad(name, transform, glowMaterial);
        f.transform.localPosition = local;
        f.transform.localScale = Vector3.one * size;
        return f.GetComponent<Renderer>();
    }

    bool on;

    void SetAll(bool v)
    {
        on = v;
        if (pool != null) pool.enabled = v;
        foreach (Renderer r in heads) if (r != null) r.enabled = v;
        foreach (Renderer r in tails) if (r != null) r.enabled = v;
        foreach (Renderer r in streaks) if (r != null) r.enabled = v;
    }

    void LateUpdate()
    {
        if (!built) return;
        float night = NightFX.Night01;
        if (night < 0.05f) { if (on) SetAll(false); return; }
        if (!on) SetAll(true);
        float wet = NightFX.Wet01;
        Camera cam = Camera.main;
        NightFX.SetColor(pool, headColor * (0.32f + 0.25f * wet) * night);
        foreach (Renderer r in heads) Billboard(r, cam, headColor * 1.6f * night);
        foreach (Renderer r in tails) Billboard(r, cam, tailColor * 1.4f * night);
        float groundY = transform.position.y + 0.025f;
        for (int i = 0; i < streaks.Length; i++)
        {
            bool head = i < headlights.Length;
            bool show = wet > 0.08f;
            streaks[i].enabled = show;
            if (!show) continue;
            Vector3 src = transform.TransformPoint(streakSources[i]);
            NightFX.OrientStreak(streaks[i].transform, src, groundY, head ? 4.5f : 3f, head ? 0.45f : 0.32f);
            NightFX.SetColor(streaks[i], (head ? headColor : tailColor) * 0.55f * wet * night);
        }
    }

    static void Billboard(Renderer r, Camera cam, Color c)
    {
        if (r == null) return;
        if (cam != null) r.transform.rotation = Quaternion.LookRotation(cam.transform.up, -cam.transform.forward);   // quad's up faces the camera
        NightFX.SetColor(r, c);
    }
}
