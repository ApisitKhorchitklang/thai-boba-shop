using UnityEngine;

// Rain everywhere you look: the rain (and its splashes on the ground) is emitted in a box that follows the camera,
// so it falls on the street ends, the junctions, the side roads and the footpaths too. Drops inside the dry zones
// (the shophouse rows with their awnings, the tunnels) are removed every frame, so it never rains in the shops.
[DefaultExecutionOrder(900)]
public class RainFollow : MonoBehaviour
{
    [Tooltip("Height above the camera (or the ground) the drops start from")]
    public float height = 14f;
    [Tooltip("Splashes: stay on the ground under the camera")]
    public bool groundLevel;
    public float groundY = 0.02f;
    [Tooltip("World-space boxes where no rain falls (set by the scene builder)")]
    public Bounds[] dryZones = new Bounds[0];

    // tests / screenshots: follow this camera instead of the one on screen
    public static Camera ViewOverride;

    ParticleSystem ps;
    ParticleSystem.Particle[] buf;

    void Awake() { ps = GetComponent<ParticleSystem>(); }

    void LateUpdate()
    {
        Camera cam = ViewOverride != null ? ViewOverride : Camera.main;
        if (cam != null)
        {
            Vector3 c = cam.transform.position;
            transform.position = groundLevel ? new Vector3(c.x, groundY, c.z) : new Vector3(c.x, Mathf.Max(c.y, 0f) + height, c.z);
        }
        Cull();
    }

    // runs after this frame's particle update, so drops born inside a dry zone are never drawn
    void Cull()
    {
        if (ps == null || dryZones.Length == 0 || ps.particleCount == 0) return;
        if (buf == null || buf.Length < ps.main.maxParticles) buf = new ParticleSystem.Particle[ps.main.maxParticles];
        int n = ps.GetParticles(buf);
        bool world = ps.main.simulationSpace == ParticleSystemSimulationSpace.World;
        bool changed = false;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = world ? buf[i].position : transform.TransformPoint(buf[i].position);
            for (int z = 0; z < dryZones.Length; z++)
            {
                if (!dryZones[z].Contains(p)) continue;
                buf[i].remainingLifetime = -1f;                  // gone
                changed = true;
                break;
            }
        }
        if (changed) ps.SetParticles(buf, n);
    }
}
