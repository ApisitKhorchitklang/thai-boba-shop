using System;
using UnityEngine;

// Every morning the soi gets a different vibe: heat wave, monsoon rain, payday, Songkran...
public class DailyEventSystem : MonoBehaviour
{
    public static DailyEventSystem Instance { get; private set; }

    public enum EventType { Normal, HeatWave, HeavyRain, Payday, Songkran, SchoolHoliday, InfluencerVisit, NightMarket }

    [Serializable]
    public class DayEvent
    {
        public EventType type;
        public string titleTH;
        public string descriptionTH;
        public string titleEN;
        public string descriptionEN;
        public string Title => Loc.T(titleTH, string.IsNullOrEmpty(titleEN) ? titleTH : titleEN);
        public string Description => Loc.T(descriptionTH, string.IsNullOrEmpty(descriptionEN) ? descriptionTH : descriptionEN);
        public float spawnMultiplier = 1f;
        public float tipMultiplier = 1f;
        public float patienceMultiplier = 1f;
        public bool forceIce;
        public float studentMultiplier = 1f;
        public float officeMultiplier = 1f;
        public float touristMultiplier = 1f;
    }

    [Tooltip("Optional: enabled only on rainy days (rain particles, puddles...)")]
    public GameObject rainEffect;
    [Tooltip("Optional: enabled only on Songkran (water guns, flags...)")]
    public GameObject songkranDecor;
    [Tooltip("Optional: enabled only on night-market days (stalls, string lights...)")]
    public GameObject nightMarketDecor;

    public DayEvent Current { get; private set; } = Make(EventType.Normal);
    public static event Action<DayEvent> OnEventStarted;

    private bool influencerPending;
    private TimeCyc timeCyc;

    void Awake()
    {
        // Scene-local singleton: the newest scene's instance wins (GameManager is the one that persists)
        Instance = this;
    }

    void Start()
    {
        timeCyc = FindFirstObjectByType<TimeCyc>();
        if (timeCyc != null)
            timeCyc.OnNewDay += RollForDay;
        RollForDay(timeCyc != null ? timeCyc.Days : 1);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (timeCyc != null)
            timeCyc.OnNewDay -= RollForDay;
    }

    public void RollForDay(int day)
    {
        EventType type;
        if (day <= 1) type = EventType.Normal;                  // calm first day to learn
        else if (day % 10 == 0) type = EventType.Songkran;
        else if (day % 15 == 0 || day % 30 == 25) type = EventType.Payday;
        else
        {
            System.Random rng = new System.Random(day * 7919);
            int r = rng.Next(100);
            if (r < 35) type = EventType.Normal;
            else if (r < 50) type = EventType.HeatWave;
            else if (r < 65) type = EventType.HeavyRain;
            else if (r < 75) type = EventType.SchoolHoliday;
            else if (r < 85) type = EventType.InfluencerVisit;
            else type = EventType.NightMarket;
        }

        Current = Make(type);
        influencerPending = type == EventType.InfluencerVisit;

        if (rainEffect != null) rainEffect.SetActive(type == EventType.HeavyRain);
        if (songkranDecor != null) songkranDecor.SetActive(type == EventType.Songkran);
        if (nightMarketDecor != null) nightMarketDecor.SetActive(type == EventType.NightMarket);

        ShopHUD.Toast(Loc.T($"วันที่ {day}: ", $"Day {day}: ") + $"{Current.Title} — {Current.Description}");
        OnEventStarted?.Invoke(Current);
    }

    // The influencer shows up once, as the next customer after 10:00
    public bool ConsumeInfluencer()
    {
        if (!influencerPending) return false;
        if (timeCyc != null && timeCyc.Hours < 10) return false;
        influencerPending = false;
        return true;
    }

    public float HourSpawnMultiplier(int hour)
    {
        if (Current.type == EventType.NightMarket && hour >= 17)
            return 1.8f;
        return 1f;
    }

    static DayEvent Make(EventType t)
    {
        switch (t)
        {
            case EventType.HeatWave:
                return new DayEvent { type = t, titleTH = "ร้อนตับแตก 39°C", descriptionTH = "คนแห่ซื้อน้ำเย็น ทุกแก้วต้องใส่น้ำแข็ง แต่ใจร้อนกว่าปกติ",
                    titleEN = "Scorching 39°C", descriptionEN = "Everyone wants something cold: every drink needs ice, and tempers are short",
                    spawnMultiplier = 1.4f, patienceMultiplier = 0.85f, forceIce = true };
            case EventType.HeavyRain:
                return new DayEvent { type = t, titleTH = "ฝนตกหนัก", descriptionTH = "ลูกค้าน้อยลง แต่คนที่มาใจดี ให้ทิปเยอะ",
                    titleEN = "Heavy rain", descriptionEN = "Fewer customers, but the ones who come tip well",
                    spawnMultiplier = 0.6f, tipMultiplier = 1.5f, patienceMultiplier = 1.2f };
            case EventType.Payday:
                return new DayEvent { type = t, titleTH = "เงินเดือนออก!", descriptionTH = "มนุษย์เงินเดือนกระเป๋าตุง ทิปคูณสอง",
                    titleEN = "Payday!", descriptionEN = "Office workers have full wallets: double tips",
                    spawnMultiplier = 1.2f, tipMultiplier = 2f, officeMultiplier = 2.5f };
            case EventType.Songkran:
                return new DayEvent { type = t, titleTH = "เทศกาลสงกรานต์", descriptionTH = "นักท่องเที่ยวเต็มซอย ลูกค้าเยอะสุดๆ",
                    titleEN = "Songkran festival", descriptionEN = "The soi is full of tourists: a huge rush of customers",
                    spawnMultiplier = 2f, tipMultiplier = 1.3f, forceIce = true, touristMultiplier = 4f };
            case EventType.SchoolHoliday:
                return new DayEvent { type = t, titleTH = "ปิดเทอม", descriptionTH = "นักเรียนหายไป แต่นักท่องเที่ยวมาแทน",
                    titleEN = "School holidays", descriptionEN = "No students today, but tourists take their place",
                    studentMultiplier = 0.2f, touristMultiplier = 2.5f };
            case EventType.InfluencerVisit:
                return new DayEvent { type = t, titleTH = "รีวิวเวอร์มาเยือน", descriptionTH = "ทำแก้วให้เป๊ะ! ได้ 5 ดาวร้านดังทันที",
                    titleEN = "A reviewer is coming", descriptionEN = "Make it perfect! 5 stars makes the shop famous",
                    spawnMultiplier = 1.1f };
            case EventType.NightMarket:
                return new DayEvent { type = t, titleTH = "ตลาดนัดหน้าร้าน", descriptionTH = "หลัง 17:00 คนเดินตลาดแวะซื้อเพียบ",
                    titleEN = "Night market", descriptionEN = "After 17:00 market crowds stop by for drinks",
                    touristMultiplier = 1.5f };
            default:
                return new DayEvent { type = EventType.Normal, titleTH = "วันธรรมดา", descriptionTH = "ขายให้ปัง ตั้งใจทำทุกแก้ว",
                    titleEN = "A normal day", descriptionEN = "Sell lots and make every drink with care" };
        }
    }
}
