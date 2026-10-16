using System.Collections.Generic;
using UnityEngine;

// Neighbouring shophouses and condos come alive after dark: windows glow warm (lit rooms), shop signs light up,
// and every open shop throws light onto its footpath. Also switches on in heavy rain / storms when it gets dark.
// Materials are cloned at runtime, so the imported FBX materials are never touched.
public class ShopNightGlow : MonoBehaviour
{
    public Renderer[] renderers;              // shophouse / condo renderers whose glass + signs glow
    public Light[] shopLights;                // one warm light per open shopfront (skipped on low quality)
    public int onHour = 18, offHour = 6;
    public Color windowColor = new Color(1f, 0.82f, 0.55f);
    public float windowGlow = 1.4f;
    public float signGlow = 1.1f;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    readonly List<(Material mat, Color glow, Color day, bool dayEmits)> glowing = new List<(Material, Color, Color, bool)>();
    TimeCyc clock;
    bool? applied;
    int appliedQuality = -1;

    readonly List<(Renderer r, Material[] mats)> originals = new List<(Renderer, Material[])>();
    bool ready;

    void Start()
    {
        clock = FindFirstObjectByType<TimeCyc>();
        Init();
    }

    void Init()
    {
        if (ready) return;
        ready = true;
        var clones = new Dictionary<Material, Material>();
        if (renderers != null)
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    string n = m.name.ToLowerInvariant();
                    bool glass = n.Contains("glass"), sign = n.Contains("sign_");      // neon already glows by itself
                    if (!glass && !sign) continue;
                    if (!clones.TryGetValue(m, out Material c))
                    {
                        c = new Material(m) { name = m.name + " (night)" };
                        clones[m] = c;
                        Color baseCol = c.HasProperty(BaseColor) ? c.GetColor(BaseColor) : Color.white;
                        Color glow = glass ? windowColor * windowGlow : baseCol * signGlow;
                        Color day = c.HasProperty(EmissionColor) ? c.GetColor(EmissionColor) : Color.black;
                        glowing.Add((c, glow, day, c.IsKeywordEnabled("_EMISSION")));
                    }
                    mats[i] = c;
                    changed = true;
                }
                if (changed) { originals.Add((r, r.sharedMaterials)); r.sharedMaterials = mats; }
            }
    }

    void OnDestroy()
    {
        foreach (var (mat, _, _, _) in glowing) if (mat != null) Destroy(mat);
    }

    // Edit-mode preview (scene captures): light up now, then RestoreOriginals() puts the imported materials back
    public void Preview(bool night)
    {
        Init();
        Apply(night, 2);
    }

    public void RestoreOriginals()
    {
        foreach (var (r, mats) in originals) if (r != null) r.sharedMaterials = mats;
        foreach (var (mat, _, _, _) in glowing) if (mat != null) DestroyImmediate(mat);
        originals.Clear();
        glowing.Clear();
        if (shopLights != null) foreach (Light l in shopLights) if (l != null) l.enabled = false;
        ready = false;
        applied = null;
    }

    public bool IsLit => applied == true;

    void Update()
    {
        float h = clock != null ? clock.TimeOfDay : 12f;
        bool night = h >= onHour - 0.25f || h < offHour;
        WeatherSystem w = WeatherSystem.Instance;
        if (w != null && w.Rain > 0.6f) night = true;            // dark storm afternoons
        int q = GameSettings.Quality;
        if (applied == night && appliedQuality == q) return;
        Apply(night, q);
    }

    void Apply(bool night, int q)
    {
        applied = night;
        appliedQuality = q;
        foreach (var (mat, glow, day, dayEmits) in glowing)
        {
            if (night)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor(EmissionColor, glow);
            }
            else
            {
                // back to the imported daytime look
                mat.SetColor(EmissionColor, day);
                if (!dayEmits) mat.DisableKeyword("_EMISSION");
            }
        }
        if (shopLights != null)
            foreach (Light l in shopLights) if (l != null) l.enabled = night && q >= 1;
    }
}
