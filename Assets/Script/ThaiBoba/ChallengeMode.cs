using UnityEngine;

// Daily challenge: a fresh shop from 11:00 to 15:00 where every player gets the same customers and orders that day
// (seeded by today's date). Score = profit + 20 per perfect drink + 5 per combo step. It never touches the saves.
public class ChallengeMode : MonoBehaviour
{
    public static bool Active { get; private set; }
    public static int Seed { get; private set; }
    public static string DateLabel { get; private set; } = "";
    public const int StartHour = 11, EndHour = 15, StartMoney = 300, StartLevel = 3;

    static System.Random rng;
    bool ended;
    TimeCyc clock;

    public static int TodaySeed => int.Parse(System.DateTime.Now.ToString("yyyyMMdd"));

    // seeded randomness for who comes in and what they order (falls back to Unity's when not in a challenge)
    public static float Value01() => Active && rng != null ? (float)rng.NextDouble() : Random.value;
    public static int Range(int minInclusive, int maxExclusive) => Active && rng != null ? rng.Next(minInclusive, maxExclusive) : Random.Range(minInclusive, maxExclusive);

    public static void Begin(int seed)
    {
        Active = true;
        Seed = seed;
        DateLabel = System.DateTime.ParseExact(seed.ToString(), "yyyyMMdd", null).ToString("dd/MM/yyyy");
        rng = new System.Random(seed);
        Random.InitState(seed);
    }

    public static void End() { Active = false; rng = null; }

    public static int Score(ShopProgress.DayStats s) => Mathf.Max(0, s.Profit) + s.perfect * 20 + s.bestCombo * 5;

    static string BestKey(int seed) => "tb_challenge_" + seed;
    public static int Best(int seed) => PlayerPrefs.GetInt(BestKey(seed), 0);

    // records the score; returns true for a new personal best today
    public static bool Record(int score)
    {
        bool best = score > Best(Seed);
        if (best) { PlayerPrefs.SetInt(BestKey(Seed), score); PlayerPrefs.Save(); }
        return best;
    }

    public static string ShareText(ShopProgress.DayStats s, int score) =>
        Loc.T($"บ้านชานมไข่มุก · ท้าทายรายวัน {DateLabel} · {score} คะแนน (เป๊ะ {s.perfect} แก้ว, คอมโบ x{s.bestCombo}) #ThaiBobaShop",
              $"Thai Boba Shop · Daily challenge {DateLabel} · {score} points ({s.perfect} perfect, combo x{s.bestCombo}) #ThaiBobaShop");

    void Start() { clock = FindFirstObjectByType<TimeCyc>(); }

    void Update()
    {
        if (!Active || ended || !GameMenus.GameRunning || clock == null) return;
        if (clock.Hours >= EndHour)
        {
            ended = true;
            CustomerController.SendAllHome(false);
            if (ShopProgress.Instance != null) ShopProgress.Instance.FinalizeDay();   // shows the challenge result
        }
    }
}
