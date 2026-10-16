using UnityEngine;

// 3D pass-by sound for a traffic vehicle: when it approaches the listener it plays a pass-by recording
// (engine rising and fading), and now and then gives a toot of the horn, the way Thai traffic does.
[RequireComponent(typeof(TrafficCar))]
public class VehicleSound : MonoBehaviour
{
    public AudioClip[] passBy;
    public AudioClip[] horns;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Range(0f, 1f)] public float hornChance = 0.12f;
    public float pitch = 1f;
    [Tooltip("Start the pass-by this far before the vehicle reaches the listener")]
    public float triggerDistance = 16f;

    AudioSource engine, horn;
    TrafficCar car;
    Transform listener;
    bool armed = true;
    float hornCooldown;

    void Awake()
    {
        car = GetComponent<TrafficCar>();
        engine = MakeSource();
        horn = MakeSource();
    }

    AudioSource MakeSource()
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 4f;
        s.maxDistance = 38f;
        s.dopplerLevel = 0f;                     // the recordings already carry their own doppler
        return s;
    }

    void Update()
    {
        if (listener == null || !listener.gameObject.activeInHierarchy)
        {
            AudioListener l = FindFirstObjectByType<AudioListener>();
            listener = l != null ? l.transform : null;
            if (listener == null) return;
        }
        hornCooldown -= Time.deltaTime;
        Vector3 to = listener.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        bool approaching = Vector3.Dot(transform.forward, to) > 0f;

        if (armed && approaching && dist < triggerDistance && car.Speed > 1f && passBy != null && passBy.Length > 0)
        {
            armed = false;
            engine.clip = passBy[Random.Range(0, passBy.Length)];
            engine.pitch = pitch * Random.Range(0.92f, 1.08f) * Mathf.Lerp(0.85f, 1.1f, Mathf.InverseLerp(3f, 10f, car.Speed));
            engine.volume = volume * GameSettings.Sfx * Random.Range(0.8f, 1f);
            engine.Play();
            if (horns != null && horns.Length > 0 && hornCooldown <= 0f && Random.value < hornChance)
            {
                horn.clip = horns[Random.Range(0, horns.Length)];
                horn.pitch = pitch * Random.Range(0.95f, 1.1f);
                horn.volume = volume * 0.8f * GameSettings.Sfx;
                horn.PlayDelayed(Random.Range(0.1f, 0.8f));
                hornCooldown = 6f;
            }
        }
        else if (!armed && (dist > triggerDistance * 1.6f || !approaching && dist > 6f && !engine.isPlaying))
            armed = true;                                   // re-arm once it has gone by
    }

    // Blocked behind another vehicle: impatient honk (called by TrafficCar when it brakes hard)
    public void Honk()
    {
        if (hornCooldown > 0f || horns == null || horns.Length == 0 || Random.value > 0.35f) return;
        horn.clip = horns[Random.Range(0, horns.Length)];
        horn.pitch = pitch * Random.Range(0.95f, 1.1f);
        horn.volume = volume * 0.8f * GameSettings.Sfx;
        horn.Play();
        hornCooldown = 8f;
    }
}
