using UnityEngine;

// Loy Krathong decor: sky lanterns (khom loy) drift up over the soi once it gets dark.
public class SkyLanterns : MonoBehaviour
{
    public ParticleSystem lanterns;
    public float startHour = 18.25f;
    public float rate = 3f;
    TimeCyc clock;

    void Start() { clock = FindFirstObjectByType<TimeCyc>(); }

    void Update()
    {
        if (lanterns == null) return;
        float h = clock != null ? clock.TimeOfDay : 20f;
        var em = lanterns.emission;
        em.rateOverTime = h >= startHour || h < 5f ? rate : 0f;
        if (!lanterns.isPlaying) lanterns.Play();
    }
}
