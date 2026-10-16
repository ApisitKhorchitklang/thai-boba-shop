using UnityEngine;

// Drifting low-poly clouds: few and white on a clear day, many and dark grey before/while it rains.
public class CloudLayer : MonoBehaviour
{
    public Transform[] clouds;
    public Vector3 wind = new Vector3(1.6f, 0f, 0.4f);
    public float wrapRadius = 170f;
    public Color fairColor = new Color(1f, 1f, 1f);
    public Color stormColor = new Color(0.33f, 0.35f, 0.4f);

    Renderer[][] renderers;
    MaterialPropertyBlock block;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        Init();
    }

    // also re-run after a script reload during play mode (non-serialized fields are reset then)
    void Init()
    {
        if (clouds == null) clouds = new Transform[0];
        block = new MaterialPropertyBlock();
        renderers = new Renderer[clouds.Length][];
        for (int i = 0; i < clouds.Length; i++)
            renderers[i] = clouds[i] != null ? clouds[i].GetComponentsInChildren<Renderer>() : new Renderer[0];
    }

    void Update()
    {
        if (block == null || renderers == null || renderers.Length != clouds.Length) Init();
        WeatherSystem w = WeatherSystem.Instance;
        float cover = w != null ? w.Overcast : 0.2f;
        float rain = w != null ? w.Rain : 0f;
        int visible = Mathf.RoundToInt(Mathf.Lerp(clouds.Length * 0.25f, clouds.Length, cover));
        Color c = Color.Lerp(fairColor, stormColor, Mathf.Clamp01(cover * 0.6f + rain * 0.6f));
        if (w != null && w.Lightning > 0.5f) c = Color.Lerp(c, Color.white, 0.6f);
        block.SetColor(BaseColor, c);
        float speed = 1f + rain * 1.5f;

        for (int i = 0; i < clouds.Length; i++)
        {
            Transform t = clouds[i];
            if (t == null) continue;
            bool on = i < visible;
            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            if (!on) continue;
            t.position += wind * speed * Time.deltaTime;
            Vector3 flat = new Vector3(t.position.x, 0f, t.position.z);
            if (flat.magnitude > wrapRadius)                      // re-enter on the upwind side
                t.position = new Vector3(-t.position.x * 0.98f, t.position.y, -t.position.z * 0.98f);
            foreach (Renderer r in renderers[i]) r.SetPropertyBlock(block);
        }
    }
}
