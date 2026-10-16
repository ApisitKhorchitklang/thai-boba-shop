using System.Collections.Generic;
using UnityEngine;

public enum Sfx
{
    Click, Open, Close, Confirm, Error,
    Cup, Boba, Ice, Sweet, Pour, SealStart, SealDone, Trash,
    Coins, Tip, Combo, Angry, Reject, Bless,
    LevelUp, Goal, DayEnd, Start, Step, Thunder
}

// Music, ambience (traffic / rain / boiling pot) and one-shot sound effects. All clips are CC0 (see Assets/Audio/ThaiBoba/CREDITS.txt).
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    [System.Serializable]
    public class Entry
    {
        public Sfx id;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Tooltip("Cut long clips (e.g. the pour loop) after this many seconds; 0 = full length")]
        public float maxSeconds;
    }

    public List<Entry> effects = new List<Entry>();
    public AudioClip music;
    public AudioClip trafficLoop;
    public AudioClip rainLoop;
    public AudioClip boilingLoop;
    [Header("Ambience by time of day")]
    public AudioClip birdsLoop;          // daytime birds
    public AudioClip nightLoop;          // crickets / night insects
    public AudioClip marketLoop;         // people chatting along the soi
    [Tooltip("3D position of the boiling pot")]
    public Transform boilingPoint;

    AudioSource musicSrc, trafficSrc, rainSrc, boilSrc, birdsSrc, nightSrc, marketSrc;
    TimeCyc clock;
    readonly List<AudioSource> pool = new List<AudioSource>();
    readonly Dictionary<Sfx, Entry> map = new Dictionary<Sfx, Entry>();

    void Awake()
    {
        Instance = this;
        foreach (Entry e in effects) map[e.id] = e;
        musicSrc = Loop(music, false);
        trafficSrc = Loop(trafficLoop, false);
        rainSrc = Loop(rainLoop, false);
        birdsSrc = Loop(birdsLoop, false);
        nightSrc = Loop(nightLoop, false);
        marketSrc = Loop(marketLoop, false);
        foreach (AudioSource a in new[] { birdsSrc, nightSrc, marketSrc })
            if (a != null) { a.volume = 0f; a.time = Random.Range(0f, a.clip.length * 0.9f); }
        if (boilingLoop != null)
        {
            boilSrc = Loop(boilingLoop, true);
            if (boilingPoint != null) boilSrc.transform.position = boilingPoint.position;
        }
        for (int i = 0; i < 8; i++) pool.Add(gameObject.AddComponent<AudioSource>());
        GameSettings.Changed += ApplyVolumes;
        GameSettings.Apply();
    }

    void OnDestroy()
    {
        GameSettings.Changed -= ApplyVolumes;
        if (Instance == this) Instance = null;
    }

    AudioSource Loop(AudioClip clip, bool spatial)
    {
        if (clip == null) return null;
        GameObject go = new GameObject("Loop_" + clip.name);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = spatial ? 1f : 0f;
        s.minDistance = 1.5f;
        s.maxDistance = 12f;
        s.Play();
        return s;
    }

    void Update()
    {
        // rain fades in on rainy days, traffic hum always, music a little quieter at night
        float sfx = GameSettings.Sfx;
        float rain = WeatherSystem.Instance != null ? WeatherSystem.Instance.Rain : 0f;
        if (rainSrc != null) rainSrc.volume = Mathf.MoveTowards(rainSrc.volume, rain * 0.75f * sfx, Time.unscaledDeltaTime * 0.3f);
        if (trafficSrc != null) trafficSrc.volume = 0.22f * sfx;
        if (boilSrc != null) boilSrc.volume = (BobaPot.Instance != null && BobaPot.Instance.IsCooking ? 0.55f : 0.12f) * sfx;   // rolling boil while cooking

        // birds by day, crickets at night, chatter while the soi's shops are open; rain drowns them out
        if (clock == null) clock = FindFirstObjectByType<TimeCyc>();
        float h = clock != null ? clock.TimeOfDay : 12f;
        float dry = 1f - rain * 0.8f;
        float day = Mathf.Clamp01((h - 5.5f) / 1f) * Mathf.Clamp01((18f - h) / 1f);
        float night = Mathf.Clamp01((h - 18.5f) / 1f) + Mathf.Clamp01((5.5f - h) / 1f);
        bool nightMarket = DailyEventSystem.Instance != null && DailyEventSystem.Instance.Current != null &&
                           DailyEventSystem.Instance.Current.type == DailyEventSystem.EventType.NightMarket;
        float open = Mathf.Clamp01((h - 7.5f) / 1f) * Mathf.Clamp01((21.5f - h) / 1f);
        float busy = open * (nightMarket && h > 16f ? 0.32f : 0.14f);
        Fade(birdsSrc, day * 0.2f * dry * sfx);
        Fade(nightSrc, Mathf.Clamp01(night) * 0.24f * dry * sfx);
        Fade(marketSrc, busy * (1f - rain * 0.6f) * sfx);
    }

    static void Fade(AudioSource s, float target)
    {
        if (s != null) s.volume = Mathf.MoveTowards(s.volume, target, Time.unscaledDeltaTime * 0.1f);
    }

    void ApplyVolumes()
    {
        if (musicSrc != null) musicSrc.volume = 0.45f * GameSettings.Music;
    }

    public static void Play(Sfx id)
    {
        if (Instance != null) Instance.PlayInternal(id);
    }

    void PlayInternal(Sfx id)
    {
        if (!map.TryGetValue(id, out Entry e) || e.clips == null || e.clips.Length == 0) return;
        AudioClip clip = e.clips[Random.Range(0, e.clips.Length)];
        if (clip == null) return;
        AudioSource src = pool[0];
        foreach (AudioSource s in pool) if (!s.isPlaying) { src = s; break; }
        src.Stop();
        src.clip = clip;
        src.volume = e.volume * GameSettings.Sfx;
        src.pitch = id == Sfx.Step ? Random.Range(0.9f, 1.1f) : 1f;
        src.time = 0f;
        src.Play();
        if (e.maxSeconds > 0f) src.SetScheduledEndTime(AudioSettings.dspTime + e.maxSeconds);
    }

    // ---------------------------------------------------------------- game events
    void OnEnable()
    {
        ShopProgress.OnServed += Served;
        ShopProgress.OnCustomerAngry += Angry;
        ShopProgress.OnLevelUp += LevelUp;
        ShopProgress.OnDaySummary += DayEnd;
        DailyGoals.OnGoalCompleted += Goal;
        WeatherSystem.OnLightning += Lightning;
    }

    void OnDisable()
    {
        ShopProgress.OnServed -= Served;
        ShopProgress.OnCustomerAngry -= Angry;
        ShopProgress.OnLevelUp -= LevelUp;
        ShopProgress.OnDaySummary -= DayEnd;
        DailyGoals.OnGoalCompleted -= Goal;
        WeatherSystem.OnLightning -= Lightning;
    }

    void Served(CustomerController c, ServeResult r)
    {
        Play(Sfx.Coins);
        if (r.quality == ServeQuality.Perfect) Play(Sfx.Tip);
        if (ShopProgress.Instance != null && ShopProgress.Instance.combo >= 2) Play(Sfx.Combo);
    }

    void Angry(CustomerController c) { Play(Sfx.Angry); }
    void LevelUp(int level) { Play(Sfx.LevelUp); }
    void DayEnd(ShopProgress.DayStats s) { Play(Sfx.DayEnd); }
    void Goal() { Play(Sfx.Goal); }

    // thunder rolls in a moment after the flash
    void Lightning() { Invoke(nameof(Thunder), Random.Range(0.4f, 1.8f)); }
    void Thunder() { Play(Sfx.Thunder); }
}
