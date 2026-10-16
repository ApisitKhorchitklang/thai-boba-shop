using UnityEngine;

// Drives the directional light, ambient light, sky and fog from the TimeCyc clock:
// sunrise 06:00 in the east, noon overhead, sunset ~18:30 (orange), then a dim blue moon at night.
// Rainy days (DailyEventSystem HeavyRain) get an overcast, darker, foggier look.
public class DayNightCycle : MonoBehaviour
{
    public TimeCyc clock;
    public Light sun;
    public float sunriseHour = 6f;
    public float sunsetHour = 18.5f;
    [Tooltip("Compass direction of the sun's path")]
    public float heading = -30f;

    [Header("Light")]
    public float noonIntensity = 1.35f;
    public Gradient sunColor = DefaultSunColor();
    public Color moonColor = new Color(0.55f, 0.65f, 1f);
    public float moonIntensity = 0.22f;

    [Header("Ambient / fog (night -> day)")]
    public Color nightSky = new Color(0.07f, 0.09f, 0.18f);
    public Color nightEquator = new Color(0.06f, 0.06f, 0.1f);
    public Color nightGround = new Color(0.03f, 0.03f, 0.05f);
    public Color daySky = new Color(0.62f, 0.75f, 0.92f);
    public Color dayEquator = new Color(0.62f, 0.6f, 0.55f);
    public Color dayGround = new Color(0.35f, 0.33f, 0.3f);
    public Color nightFog = new Color(0.08f, 0.09f, 0.15f);
    public Color dayFog = new Color(0.8f, 0.84f, 0.9f);
    public Color sunsetFog = new Color(0.95f, 0.65f, 0.45f);

    [Header("Sky (procedural skybox exposure)")]
    public float daySkyExposure = 1.3f;
    public float nightSkyExposure = 0.05f;

    [Header("Weather")]
    [Range(0f, 1f)] public float overcast;       // set automatically on rainy days

    Material skyInstance;
    float lightningFlash;

    // Daylight 0 (night) .. 1 (full day): used by other systems (e.g. NightLights)
    public float Daylight { get; private set; } = 1f;

    static Gradient DefaultSunColor()
    {
        Gradient g = new Gradient();
        g.SetKeys(new[]
        {
            new GradientColorKey(new Color(1f, 0.55f, 0.3f), 0f),
            new GradientColorKey(new Color(1f, 0.9f, 0.75f), 0.18f),
            new GradientColorKey(new Color(1f, 0.97f, 0.9f), 0.5f),
            new GradientColorKey(new Color(1f, 0.85f, 0.65f), 0.82f),
            new GradientColorKey(new Color(1f, 0.45f, 0.2f), 1f),
        }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
        return g;
    }

    void OnEnable()
    {
        if (clock == null) clock = FindFirstObjectByType<TimeCyc>();
        if (sun == null && clock != null) sun = clock.GetComponent<Light>();
        if (clock != null) clock.rotateSun = false;
        if (Application.isPlaying && RenderSettings.skybox != null)
        {
            skyInstance = new Material(RenderSettings.skybox);   // don't modify the skybox asset
            RenderSettings.skybox = skyInstance;
        }
    }

    void LateUpdate()
    {
        if (Application.isPlaying)
        {
            WeatherSystem w = WeatherSystem.Instance;
            overcast = w != null ? Mathf.Clamp01(w.Overcast * 0.7f + w.Rain * 0.5f) : 0f;
            lightningFlash = w != null ? w.Lightning : 0f;
        }
        Apply(clock != null ? clock.TimeOfDay : 12f);
    }

    // Editor previews pass a temporary skybox copy so the asset itself is never changed
    public void SetSkyMaterial(Material m)
    {
        skyInstance = m;
    }

    // Public so editor tools/tests can preview any hour
    public void Apply(float hour)
    {
        if (sun == null) return;
        bool day = hour >= sunriseHour && hour <= sunsetHour;
        float dayLen = sunsetHour - sunriseHour;
        float f;
        if (day)
        {
            f = (hour - sunriseHour) / dayLen;                               // 0 sunrise .. 1 sunset
            sun.transform.rotation = Quaternion.Euler(f * 180f, heading, 0f);
        }
        else
        {
            float nightLen = 24f - dayLen;
            f = Mathf.Repeat(hour - sunsetHour, 24f) / nightLen;            // 0 dusk .. 1 dawn
            sun.transform.rotation = Quaternion.Euler(20f + f * 140f, heading + 180f, 0f);   // moon arc
        }

        float arc = Mathf.Sin(f * Mathf.PI);                                 // 0 at the horizon, 1 at the top
        float k = day ? Mathf.Clamp01(arc * 2.5f) : 0f;                      // daylight with soft twilight
        Daylight = k * (1f - 0.35f * overcast);

        if (day)
        {
            sun.color = Color.Lerp(sunColor.Evaluate(f), new Color(0.75f, 0.78f, 0.82f), overcast);
            sun.intensity = noonIntensity * Mathf.Clamp01(arc * 2.5f) * (1f - 0.6f * overcast);
        }
        else
        {
            sun.color = moonColor;
            sun.intensity = moonIntensity * Mathf.Clamp01(arc * 3f);
        }
        sun.shadowStrength = Mathf.Lerp(0.35f, 0.9f, k) * (1f - 0.6f * overcast);

        Color grey = new Color(0.55f, 0.58f, 0.62f);
        RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(nightSky, daySky, k), grey * Mathf.Max(0.15f, k), overcast * 0.6f);
        if (lightningFlash > 0.01f)
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, new Color(0.8f, 0.85f, 1f), lightningFlash * 0.7f);
        RenderSettings.ambientEquatorColor = Color.Lerp(nightEquator, dayEquator, k);
        RenderSettings.ambientGroundColor = Color.Lerp(nightGround, dayGround, k);

        // fog: warm at dusk/dawn, blue-black at night, grey and closer in the rain
        float twilight = day ? Mathf.Clamp01(1f - arc * 3f) : 0f;
        Color fog = Color.Lerp(Color.Lerp(nightFog, dayFog, k), sunsetFog, twilight * 0.6f);
        RenderSettings.fogColor = Color.Lerp(fog, grey * Mathf.Max(0.2f, k), overcast * 0.7f);
        if (RenderSettings.fog)
            RenderSettings.fogEndDistance = Mathf.Lerp(150f, 60f, overcast);

        Material sky = skyInstance != null ? skyInstance : null;
        if (sky != null && sky.HasProperty("_Exposure"))
            sky.SetFloat("_Exposure", Mathf.Lerp(nightSkyExposure, daySkyExposure, k) * (1f - 0.5f * overcast));
    }
}
