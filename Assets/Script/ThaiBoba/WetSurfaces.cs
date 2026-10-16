using System.Collections.Generic;
using UnityEngine;

// Wet streets: road / footpath materials get darker and glossy (they reflect the sky, lamps and neon) as
// WeatherSystem.Wetness rises, and puddles spread out. Works on runtime copies so the FBX materials stay untouched.
public class WetSurfaces : MonoBehaviour
{
    [Tooltip("Renderers whose materials get wet (road tiles, footpaths, curbs)")]
    public Renderer[] surfaces;
    [Tooltip("Puddle objects; they grow from nothing to full size with wetness")]
    public Transform[] puddles;
    public float wetSmoothness = 0.86f;
    [Range(0f, 1f)] public float wetDarken = 0.38f;

    class WetMat
    {
        public Material mat;
        public Color dry;
        public float drySmooth;
    }

    readonly List<WetMat> mats = new List<WetMat>();
    Vector3[] puddleScale;
    float applied = -1f;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

    void Start()
    {
        var copies = new Dictionary<Material, Material>();
        if (surfaces != null)
            foreach (Renderer r in surfaces)
            {
                if (r == null) continue;
                Material[] shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    Material src = shared[i];
                    if (src == null || !src.HasProperty(BaseColor)) continue;
                    if (!copies.TryGetValue(src, out Material copy))
                    {
                        copy = new Material(src) { name = src.name + " (wet)" };
                        copies[src] = copy;
                        mats.Add(new WetMat { mat = copy, dry = src.GetColor(BaseColor),
                                              drySmooth = src.HasProperty(Smoothness) ? src.GetFloat(Smoothness) : 0.2f });
                    }
                    shared[i] = copy;
                }
                r.sharedMaterials = shared;
            }
        if (puddles != null)
        {
            puddleScale = new Vector3[puddles.Length];
            for (int i = 0; i < puddles.Length; i++)
                if (puddles[i] != null) puddleScale[i] = puddles[i].localScale;
        }
        Apply(0f);
    }

    void Update()
    {
        float w = WeatherSystem.Instance != null ? WeatherSystem.Instance.Wetness : 0f;
        if (Mathf.Abs(w - applied) > 0.005f) Apply(w);
    }

    void Apply(float w)
    {
        applied = w;
        foreach (WetMat m in mats)
        {
            m.mat.SetColor(BaseColor, Color.Lerp(m.dry, m.dry * (1f - wetDarken), w));
            m.mat.SetFloat(Smoothness, Mathf.Lerp(m.drySmooth, wetSmoothness, w));
        }
        if (puddles != null)
            for (int i = 0; i < puddles.Length; i++)
            {
                if (puddles[i] == null) continue;
                float s = Mathf.Clamp01((w - 0.25f) / 0.6f);
                puddles[i].gameObject.SetActive(s > 0.02f);
                puddles[i].localScale = new Vector3(puddleScale[i].x * s, puddleScale[i].y, puddleScale[i].z * s);
            }
    }

    // Read by tests
    public float CurrentSmoothness => mats.Count > 0 ? mats[0].mat.GetFloat(Smoothness) : 0f;

    void OnDestroy()
    {
        foreach (WetMat m in mats) if (m.mat != null) Destroy(m.mat);
    }
}
