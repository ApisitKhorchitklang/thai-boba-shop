using System.Collections.Generic;
using UnityEngine;

// Sloshing drink: the liquid surface stays level when the cup tilts, and swings like a damped pendulum when the
// cup accelerates or turns (walking, stopping, looking around, throwing). Ice and pearls sway with it.
// Works with the LiquidWobble shader: each Liquid_<flavor> renderer gets a copy of liquidMaterial in its colour.
public class LiquidSlosh : MonoBehaviour
{
    [Tooltip("Material using the ThaiBoba/LiquidWobble shader (copied per liquid)")]
    public Material liquidMaterial;
    [Tooltip("Slosh frequency in Hz (a small cup sloshes fast)")]
    public float frequency = 2.6f;
    [Tooltip("0 = never settles, 1 = no swing at all")]
    [Range(0.01f, 1f)] public float damping = 0.12f;
    [Tooltip("How strongly cup acceleration tilts the surface")]
    public float response = 1f;
    [Tooltip("How much spinning the cup (turning the camera) stirs the liquid")]
    public float turnStir = 0.035f;
    public float maxSlope = 0.65f;
    [Tooltip("Surface sits this far below the top of the liquid mesh (m)")]
    public float topInset = 0.006f;
    [Tooltip("Ice/pearl sway relative to the surface tilt")]
    public float iceFollow = 0.35f, pearlFollow = 0.12f;

    static readonly int FillPoint = Shader.PropertyToID("_FillPoint");
    static readonly int Slope = Shader.PropertyToID("_Slope");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int TopColorId = Shader.PropertyToID("_TopColor");
    static readonly int FoamColorId = Shader.PropertyToID("_FoamColor");

    class Liquid { public Renderer r; public Material mat; public float topY, bottomY; }

    // pour animation: the surface rises from the bottom of the cup to the top
    float fill = 1f, fillRate;
    public float Fill => fill;
    public void AnimateFill(float seconds) { fill = 0f; fillRate = 1f / Mathf.Max(0.05f, seconds); }
    readonly List<Liquid> liquids = new List<Liquid>();
    Transform ice;
    Quaternion iceRest;
    readonly List<(Transform t, Quaternion rest)> pearls = new List<(Transform, Quaternion)>();

    Vector2 slope, slopeVel;          // surface slope along world X/Z and its rate
    Vector3 lastPos, lastVel;
    Quaternion lastRot;
    bool primed;

    public Vector2 CurrentSlope => slope;

    void Awake()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name;
            if (n == "Cup_Ice") { ice = r.transform; iceRest = ice.localRotation; continue; }
            if (n.StartsWith("Cup_Pearls")) { pearls.Add((r.transform, r.transform.localRotation)); continue; }
            if (!n.StartsWith("Liquid_") || liquidMaterial == null) continue;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Color c = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId) ? r.sharedMaterial.GetColor(BaseColorId) : Color.white;
            Material m = new Material(liquidMaterial) { name = "Liquid (" + n.Substring(7) + ")" };
            m.SetColor(BaseColorId, c);
            m.SetColor(TopColorId, Color.Lerp(c, Color.white, 0.22f));
            m.SetColor(FoamColorId, Color.Lerp(c, Color.white, 0.6f));
            r.sharedMaterial = m;
            liquids.Add(new Liquid { r = r, mat = m, topY = mf.sharedMesh.bounds.max.y - topInset, bottomY = mf.sharedMesh.bounds.min.y + 0.004f });
        }
    }

    void OnDestroy()
    {
        foreach (Liquid l in liquids) if (l.mat != null) Destroy(l.mat);
    }

    void OnEnable() { primed = false; }

    // Kick the liquid (e.g. when a cup is put down hard or thrown)
    public void Splash(Vector2 impulse) { slopeVel += impulse; }

    void LateUpdate()
    {
        if (fill < 1f) fill = Mathf.Min(1f, fill + Time.deltaTime * fillRate);
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;
        if (!primed)
        {
            lastPos = pos; lastVel = Vector3.zero; lastRot = rot; primed = true;
            Apply();
            return;
        }

        Vector3 vel = (pos - lastPos) / dt;
        Vector3 acc = (vel - lastVel) / dt;
        lastPos = pos;
        lastVel = vel;
        // turning on the spot swirls the drink a little
        (rot * Quaternion.Inverse(lastRot)).ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        float yawRate = float.IsNaN(axis.y) ? 0f : angle * axis.y / dt;
        lastRot = rot;

        // a liquid at rest tilts against the acceleration: slope = -a_horizontal / g
        acc = Vector3.ClampMagnitude(acc, 60f);
        Vector2 target = new Vector2(-acc.x, -acc.z) / 9.81f * response;
        Vector2 swirl = new Vector2(-slope.y, slope.x) * (yawRate * Mathf.Deg2Rad) * turnStir;

        // damped spring towards that tilt
        float w = 2f * Mathf.PI * frequency;
        Vector2 accel = w * w * (target - slope) - 2f * damping * w * slopeVel;
        slopeVel += (accel + swirl * w) * dt;
        slope += slopeVel * dt;
        if (slope.magnitude > maxSlope) { slope = slope.normalized * maxSlope; slopeVel *= 0.5f; }
        Apply();
    }

    void Apply()
    {
        foreach (Liquid l in liquids)
        {
            if (!l.r.gameObject.activeInHierarchy) continue;
            Vector3 fill = l.r.transform.TransformPoint(new Vector3(0f, Mathf.Lerp(l.bottomY, l.topY, this.fill), 0f));
            l.mat.SetVector(FillPoint, fill);
            l.mat.SetVector(Slope, new Vector4(slope.x, slope.y, 0f, 0f));
        }
        // ice floats with the surface, pearls sway a little at the bottom (rotations relative to the cup)
        Vector3 surfaceUp = new Vector3(-slope.x, 1f, -slope.y).normalized;
        Quaternion tilt = Quaternion.FromToRotation(transform.up, surfaceUp);
        if (ice != null && ice.gameObject.activeSelf)
            ice.localRotation = Quaternion.Inverse(transform.rotation) * Quaternion.Slerp(Quaternion.identity, tilt, iceFollow) * transform.rotation * iceRest;
        Quaternion pearlTilt = Quaternion.Inverse(transform.rotation) * Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(tilt), pearlFollow) * transform.rotation;
        foreach (var (t, rest) in pearls)
            if (t.gameObject.activeSelf) t.localRotation = pearlTilt * rest;
    }
}
