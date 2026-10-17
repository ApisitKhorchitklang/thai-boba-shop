using UnityEngine;

// Festival moments that aren't just decor:
//   Chinese New Year: strings of firecrackers in the street at 9:00 and 18:00 (sound + sparks + smoke) and
//   cheerful customers who sometimes hand the shop an ang pao (อั่งเปา, red envelope) on top of the tip.
public class FestivalFX : MonoBehaviour
{
    [Tooltip("Where the firecrackers go off (in the street, in front of the shop)")]
    public Transform[] firecrackerSpots;
    public ParticleSystem firecrackerSparks;
    [Range(0f, 1f)] public float angPaoChance = 0.15f;
    static readonly int[] LuckyAmounts = { 20, 28, 50, 66, 88, 99 };

    TimeCyc clock;
    int lastHour = -1;

    bool ChineseNewYear => DailyEventSystem.Instance != null && DailyEventSystem.Instance.Current.type == DailyEventSystem.EventType.ChineseNewYear;

    void Start() { clock = FindFirstObjectByType<TimeCyc>(); }
    void OnEnable() { ShopProgress.OnServed += Served; }
    void OnDisable() { ShopProgress.OnServed -= Served; }

    void Served(CustomerController c, ServeResult r)
    {
        if (!ChineseNewYear || r.quality != ServeQuality.Perfect || ShopProgress.Instance == null) return;
        if (ChallengeMode.Value01() >= angPaoChance) return;
        int amount = LuckyAmounts[ChallengeMode.Range(0, LuckyAmounts.Length)];
        ShopProgress.Instance.today.angPao++;
        StatsSystem.Add(StatsSystem.Stat.AngPao);
        ShopProgress.Instance.AddBonus(amount, Loc.T("ลูกค้าให้อั่งเปา! ซินเจียยู่อี่", "An ang pao from a customer! Happy New Year"));
    }

    void Update()
    {
        if (clock == null || !GameMenus.GameRunning) return;
        int h = clock.Hours;
        if (h == lastHour) return;
        lastHour = h;
        if (ChineseNewYear && (h == 9 || h == 18)) Fire();
    }

    // light a string of firecrackers at one of the spots
    public void Fire()
    {
        if (firecrackerSpots == null || firecrackerSpots.Length == 0) return;
        Transform spot = firecrackerSpots[Random.Range(0, firecrackerSpots.Length)];
        GameAudio.PlayAt(Sfx.Firecracker, spot.position, 1f);
        if (firecrackerSparks != null)
        {
            firecrackerSparks.transform.position = spot.position;
            firecrackerSparks.Play();
        }
        ShopHUD.Toast(Loc.T("ปัง ปัง ปัง! จุดประทัดรับปีใหม่ ไล่สิ่งไม่ดี", "Bang bang bang! Firecrackers to chase the bad luck away"));
    }
}
