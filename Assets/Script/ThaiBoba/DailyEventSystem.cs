using System;
using UnityEngine;

// Every morning the soi gets a different vibe: heat wave, monsoon rain, payday, Songkran...
public class DailyEventSystem : MonoBehaviour
{
    public static DailyEventSystem Instance { get; private set; }

    public enum EventType { Normal, HeatWave, HeavyRain, Payday, Songkran, SchoolHoliday, InfluencerVisit, NightMarket, LoyKrathong,
                            ChineseNewYear, MothersDay, Vegetarian }

    // festivals count for the "festival goer" medal and get the festival icon
    public static bool IsFestival(EventType t) => t == EventType.Songkran || t == EventType.LoyKrathong || t == EventType.NightMarket ||
                                                 t == EventType.ChineseNewYear || t == EventType.MothersDay || t == EventType.Vegetarian;

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
        public float auntieMultiplier = 1f;
        [Tooltip("Share of customers who eat vegetarian (เจ): no cheese foam, no pudding")]
        public float vegetarianChance;
    }

    [Tooltip("Optional: enabled only on rainy days (rain particles, puddles...)")]
    public GameObject rainEffect;
    [Tooltip("Optional: enabled only on Songkran (water guns, flags...)")]
    public GameObject songkranDecor;
    [Tooltip("Optional: enabled only on night-market days (stalls, string lights...)")]
    public GameObject nightMarketDecor;
    [Tooltip("Optional: enabled only on Loy Krathong (lanterns, sky lanterns after dark)")]
    public GameObject loyKrathongDecor;
    [Tooltip("Optional: red lanterns for Chinese New Year")]
    public GameObject chineseNewYearDecor;
    [Tooltip("Optional: light blue flags and jasmine garlands for Mother's Day")]
    public GameObject mothersDayDecor;
    [Tooltip("Optional: yellow เจ flags for the vegetarian festival")]
    public GameObject vegetarianDecor;

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
        else if (day % 30 == 10) type = EventType.Songkran;          // end of the hot season (April)
        else if (day % 30 == 27) type = EventType.LoyKrathong;       // cool season full moon (November)
        else if (day % 30 == 29) type = EventType.ChineseNewYear;    // end of the cool season (January / February)
        else if (day % 30 == 17) type = EventType.MothersDay;        // rainy season (12 August)
        else if (day % 30 == 19) type = EventType.Vegetarian;        // end of the rainy season (October)
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
        if (loyKrathongDecor != null) loyKrathongDecor.SetActive(type == EventType.LoyKrathong);
        if (chineseNewYearDecor != null) chineseNewYearDecor.SetActive(type == EventType.ChineseNewYear);
        if (mothersDayDecor != null) mothersDayDecor.SetActive(type == EventType.MothersDay);
        if (vegetarianDecor != null) vegetarianDecor.SetActive(type == EventType.Vegetarian);
        // first day of a season: tell the player what is on the seasonal menu
        if (day > 1 && (day - 1) % 10 == 0)
        {
            WeatherSystem.Season season = WeatherSystem.SeasonForDay(day);
            ShopHUD.Toast(season == WeatherSystem.Season.Hot ? Loc.T("เข้าหน้าร้อนแล้ว! เมนูพิเศษ: เครื่องดื่มปั่น ที่รถเข็นเมนูตามฤดู", "Hot season! Seasonal menu: blended frappes at the seasonal cart")
                        : season == WeatherSystem.Season.Cool ? Loc.T("เข้าหน้าหนาวแล้ว! เมนูพิเศษ: เครื่องดื่มร้อนใส่มาร์ชเมลโลว์", "Cool season! Seasonal menu: hot drinks with marshmallows")
                        : Loc.T("เข้าหน้าฝนแล้ว! ฝนตกบ่อย ลูกค้าแวะหลบฝน", "Rainy season! Frequent showers bring people in"));
        }

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
        if (Current.type == EventType.LoyKrathong && hour >= 18)
            return 2f;
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
            case EventType.LoyKrathong:
                return new DayEvent { type = t, titleTH = "คืนลอยกระทง", descriptionTH = "หลัง 18:00 คนออกมาเที่ยวเต็มซอย โคมลอยเต็มฟ้า ชาไทยท็อปโฟมขายดี",
                    titleEN = "Loy Krathong night", descriptionEN = "After 18:00 the soi fills up, lanterns float up and Thai tea with cheese foam sells out",
                    tipMultiplier = 1.3f, touristMultiplier = 2f };
            case EventType.ChineseNewYear:
                return new DayEvent { type = t, titleTH = "ตรุษจีน", descriptionTH = "โคมแดงเต็มซอย ลูกค้าอารมณ์ดีแจกอั่งเปา ทิปเพิ่ม มีจุดประทัดเช้า-เย็น",
                    titleEN = "Chinese New Year", descriptionEN = "Red lanterns everywhere, cheerful customers hand out ang pao, better tips, firecrackers morning and evening",
                    spawnMultiplier = 1.3f, tipMultiplier = 1.5f, officeMultiplier = 1.3f };
            case EventType.MothersDay:
                return new DayEvent { type = t, titleTH = "วันแม่", descriptionTH = "คนซื้อชาไปฝากแม่ คุณป้าคุณแม่มาเยอะ ใจดี รอได้นาน",
                    titleEN = "Mother's Day", descriptionEN = "People buy tea for their mums; lots of mothers and aunties come by, patient and kind",
                    spawnMultiplier = 1.2f, tipMultiplier = 1.3f, patienceMultiplier = 1.15f, auntieMultiplier = 3f };
            case EventType.Vegetarian:
                return new DayEvent { type = t, titleTH = "เทศกาลกินเจ", descriptionTH = "ธงเหลืองเต็มซอย ลูกค้าที่กินเจ (เจ) ห้ามใส่ชีสโฟม/พุดดิ้ง ดูป้ายเจที่ออเดอร์ให้ดี",
                    titleEN = "Vegetarian Festival", descriptionEN = "Yellow flags all over the soi; vegetarian customers (marked) take no cheese foam and no pudding",
                    spawnMultiplier = 1.1f, vegetarianChance = 0.6f };
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
