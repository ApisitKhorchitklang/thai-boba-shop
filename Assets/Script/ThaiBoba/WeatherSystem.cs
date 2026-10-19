using System;
using UnityEngine;

// Seasons + weather that changes during the day.
//   Seasons rotate every 10 in-game days: ฤดูร้อน (hot) -> ฤดูฝน (rainy) -> ฤดูหนาว (cool).
//   Each day gets a schedule: clouds build up, rain starts light, gets heavy (sometimes a thunderstorm), eases off,
//   then the streets slowly dry. Other systems read Overcast / Rain / Wetness / Storm.
public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    public enum Season { Hot, Rainy, Cool }
    public enum Sky { Clear, Cloudy, Rain, Storm }

    [Header("Season")]
    public int daysPerSeason = 10;
    [Range(0f, 1f)] public float rainChanceHot = 0.2f;
    [Range(0f, 1f)] public float rainChanceRainy = 0.75f;
    [Range(0f, 1f)] public float rainChanceCool = 0.08f;

    [Header("Effects (optional)")]
    public ParticleSystem rain;
    public ParticleSystem rainSplash;
    public float maxRainRate = 1900f;
    public float maxSplashRate = 450f;
    public Light lightning;
    public float lightningIntensity = 3.5f;

    // live values, 0..1
    public float Overcast { get; private set; }
    public float Rain { get; private set; }
    public float Wetness { get; private set; }
    public bool Storm { get; private set; }
    public Season CurrentSeason { get; private set; }
    public Sky Now => Rain > 0.15f ? (Storm ? Sky.Storm : Sky.Rain) : Overcast > 0.45f ? Sky.Cloudy : Sky.Clear;
    public bool IsRaining => Rain > 0.15f;
    public float Lightning { get; private set; }

    public static event Action OnLightning;

    // today's schedule (fractional hours); rainStart < 0 = dry day
    float rainStart = -1f, rainEnd = -1f, stormChance;
    bool stormToday;
    TimeCyc clock;
    float nextFlash;
    // manual override for tests / debug
    float forcedRain = -1f, forcedWet = -1f;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (clock != null) clock.OnNewDay -= PlanDay;
    }

    void Start()
    {
        clock = FindFirstObjectByType<TimeCyc>();
        if (clock != null) clock.OnNewDay += PlanDay;
        PlanDay(clock != null ? clock.Days : 1);
    }

    public static Season SeasonForDay(int day, int daysPerSeason = 10)
    {
        return (Season)(((Mathf.Max(1, day) - 1) / Mathf.Max(1, daysPerSeason)) % 3);
    }

    public static string SeasonTH(Season s) => s == Season.Hot ? Loc.T("ฤดูร้อน", "Hot season") : s == Season.Rainy ? Loc.T("ฤดูฝน", "Rainy season") : Loc.T("ฤดูหนาว", "Cool season");

    public string SkyTH
    {
        get
        {
            switch (Now)
            {
                case Sky.Storm: return Loc.T("พายุฝนฟ้าคะนอง", "Thunderstorm");
                case Sky.Rain: return Rain > 0.6f ? Loc.T("ฝนตกหนัก", "Heavy rain") : Loc.T("ฝนตก", "Rain");
                case Sky.Cloudy: return Loc.T("ฟ้าครึ้ม", "Overcast");
                default: return CurrentSeason == Season.Hot ? Loc.T("แดดจัด", "Blazing sun") : Loc.T("ฟ้าใส", "Clear sky");
            }
        }
    }

    public string ForecastTH
    {
        get
        {
            if (rainStart < 0f) return Loc.T("วันนี้ไม่มีฝน", "No rain today");
            int h = Mathf.FloorToInt(rainStart);
            return stormToday ? Loc.T($"ระวังพายุ ~{h:00}:00 น.", $"Storm likely ~{h:00}:00") : Loc.T($"ฝนอาจตก ~{h:00}:00 น.", $"Rain possible ~{h:00}:00");
        }
    }

    void PlanDay(int day)
    {
        CurrentSeason = SeasonForDay(day, daysPerSeason);
        System.Random rng = new System.Random(day * 3571 + 17);
        float chance = CurrentSeason == Season.Hot ? rainChanceHot : CurrentSeason == Season.Rainy ? rainChanceRainy : rainChanceCool;
        bool heavyRainEvent = DailyEventSystem.Instance != null && DailyEventSystem.Instance.Current.type == DailyEventSystem.EventType.HeavyRain;

        if (heavyRainEvent)
        {
            rainStart = 8f; rainEnd = 20.5f; stormToday = true;          // "ฝนตกหนัก" event: wet all day
        }
        else if (rng.NextDouble() < chance)
        {
            // afternoon showers are the classic Thai pattern
            rainStart = 10f + (float)rng.NextDouble() * 8f;
            rainEnd = rainStart + 0.75f + (float)rng.NextDouble() * (CurrentSeason == Season.Rainy ? 2.5f : 1.2f);
            stormToday = CurrentSeason == Season.Rainy && rng.NextDouble() < 0.45;
        }
        else
        {
            rainStart = rainEnd = -1f;
            stormToday = false;
        }
        Wetness = Mathf.Max(0f, Wetness * 0.2f);
    }

    // For tests / debugging: pin the weather (rain < 0 = back to the schedule)
    public void Force(float rainAmount, float wetness, bool storm)
    {
        forcedRain = rainAmount;
        forcedWet = wetness;
        Storm = storm;
        if (rainAmount >= 0f) { Rain = rainAmount; Overcast = Mathf.Max(Overcast, Mathf.Clamp01(rainAmount + 0.4f)); }
        if (wetness >= 0f) Wetness = wetness;
        ApplyEffects();
    }

    void Update()
    {
        float hour = clock != null ? clock.TimeOfDay : 12f;
        float targetCloud = CurrentSeason == Season.Rainy ? 0.35f : CurrentSeason == Season.Cool ? 0.2f : 0.1f;
        float targetRain = 0f;
        bool storm = false;

        if (forcedRain >= 0f)
        {
            targetRain = forcedRain;
            targetCloud = Mathf.Max(targetCloud, Mathf.Clamp01(forcedRain + 0.4f));
            storm = Storm;
        }
        else if (rainStart >= 0f)
        {
            float lead = 1.2f;                                            // clouds gather ~1 h before
            if (hour > rainStart - lead && hour < rainEnd + 0.5f)
                targetCloud = Mathf.Lerp(targetCloud, 1f, Mathf.Clamp01((hour - (rainStart - lead)) / lead));
            if (hour >= rainStart && hour <= rainEnd)
            {
                float dur = rainEnd - rainStart;
                float t = (hour - rainStart) / Mathf.Max(0.01f, dur);
                // ramp in, heavy middle, ease off
                targetRain = Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 1.3f);
                storm = stormToday && t > 0.2f && t < 0.75f;
            }
        }

        float gameRate = clock != null ? clock.timeScale : 1f;             // weather follows game time, not real time
        float dt = Time.deltaTime * Mathf.Max(0.5f, gameRate);
        Overcast = Mathf.MoveTowards(Overcast, targetCloud, dt * 0.02f);
        Rain = Mathf.MoveTowards(Rain, targetRain, dt * 0.03f);
        Storm = storm;
        if (forcedWet >= 0f) Wetness = forcedWet;
        else if (Rain > 0.05f) Wetness = Mathf.MoveTowards(Wetness, 1f, dt * 0.015f * (0.5f + Rain));
        else Wetness = Mathf.MoveTowards(Wetness, 0f, dt * (CurrentSeason == Season.Hot ? 0.006f : 0.003f));

        UpdateLightning();
        ApplyEffects();

        bool raining = IsRaining;
        if (raining != wasRaining)
        {
            wasRaining = raining;
            ShopHUD.Toast(raining ? Loc.T("ฝนตกแล้ว! คนแห่เข้ามาหลบฝน · เครื่องดื่มร้อนขายดี", "It's raining! People rush in to shelter · hot drinks sell well") : Loc.T("ฝนหยุดแล้ว ถนนยังเปียกอยู่ ระวังลื่นนะ", "The rain stopped, but the street is still wet, careful!"));
        }
    }

    bool wasRaining;

    void UpdateLightning()
    {
        Lightning = Mathf.MoveTowards(Lightning, 0f, Time.deltaTime * 6f);
        if (Storm && Rain > 0.4f && Time.time > nextFlash)
        {
            nextFlash = Time.time + UnityEngine.Random.Range(5f, 14f);
            Lightning = 1f;
            OnLightning?.Invoke();
        }
        if (lightning != null)
        {
            // double flicker
            float f = Lightning > 0.7f || (Lightning > 0.3f && Lightning < 0.45f) ? Lightning : Lightning * 0.3f;
            lightning.intensity = f * lightningIntensity;
            lightning.enabled = f > 0.01f;
        }
    }

    void ApplyEffects()
    {
        if (rain != null)
        {
            ParticleSystem.EmissionModule em = rain.emission;
            em.rateOverTimeMultiplier = Rain * maxRainRate * (GameSettings.Quality == 0 ? 0.4f : 1f);
            if (Rain > 0.01f && !rain.isPlaying) rain.Play();
        }
        if (rainSplash != null)
        {
            ParticleSystem.EmissionModule em = rainSplash.emission;
            em.rateOverTimeMultiplier = Rain * maxSplashRate;
            if (Rain > 0.01f && !rainSplash.isPlaying) rainSplash.Play();
        }
    }
}
