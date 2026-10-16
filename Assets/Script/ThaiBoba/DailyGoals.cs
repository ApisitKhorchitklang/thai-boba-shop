using System;
using System.Collections.Generic;
using UnityEngine;

// Three small missions per day with cash rewards (ภารกิจประจำวัน).
public class DailyGoals : MonoBehaviour
{
    public static DailyGoals Instance { get; private set; }

    public enum GoalType { ServeFlavor, ServeAny, PerfectServes, ReachCombo, EarnTips, NoAngry }

    [Serializable]
    public class Goal
    {
        public GoalType type;
        public string flavor;
        public int target;
        public int progress;
        public int reward;
        public bool done;

        public string Text
        {
            get
            {
                string body;
                switch (type)
                {
                    case GoalType.ServeFlavor:
                        DrinkMenu.Drink d = DrinkMenu.Get(flavor);
                        body = Loc.T($"ขาย{(d != null ? d.Name : flavor)} {target} แก้ว", $"Sell {target} {(d != null ? d.Name : flavor)}"); break;
                    case GoalType.ServeAny: body = Loc.T($"เสิร์ฟลูกค้า {target} คน", $"Serve {target} customers"); break;
                    case GoalType.PerfectServes: body = Loc.T($"ทำแก้วเป๊ะ {target} แก้ว", $"Make {target} perfect drinks"); break;
                    case GoalType.ReachCombo: body = Loc.T($"คอมโบให้ถึง x{target}", $"Reach a x{target} combo"); break;
                    case GoalType.EarnTips: body = Loc.T($"ได้ทิปรวม {target} ฿", $"Earn {target} ฿ in tips"); break;
                    default: body = Loc.T("ไม่มีลูกค้าเดินหนีทั้งวัน", "No customer walks out all day"); break;
                }
                string status = done ? Loc.T("[เสร็จ]", "[Done]") : (type == GoalType.NoAngry ? "" : $"({progress}/{target})");
                return $"{status} {body}  +{reward} ฿";
            }
        }
    }

    public List<Goal> goals = new List<Goal>();
    public static event Action OnGoalsChanged;
    public static event Action OnGoalCompleted;

    private TimeCyc timeCyc;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        timeCyc = FindFirstObjectByType<TimeCyc>();
        if (timeCyc != null) timeCyc.OnNewDay += Generate;
        ShopProgress.OnServed += HandleServed;
        ShopProgress.OnCustomerAngry += HandleAngry;
        Generate(timeCyc != null ? timeCyc.Days : 1);
    }

    void OnDestroy()
    {
        if (timeCyc != null) timeCyc.OnNewDay -= Generate;
        ShopProgress.OnServed -= HandleServed;
        ShopProgress.OnCustomerAngry -= HandleAngry;
        if (Instance == this) Instance = null;
    }

    public void Generate(int day)
    {
        System.Random rng = new System.Random(day * 104729);
        int level = ShopProgress.Instance != null ? ShopProgress.Instance.level : 1;
        List<DrinkMenu.Drink> menu = DrinkMenu.Unlocked(level);
        float scale = 1f + (day - 1) * 0.15f;

        List<GoalType> pool = new List<GoalType>((GoalType[])Enum.GetValues(typeof(GoalType)));
        goals.Clear();
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            GoalType t = pool[rng.Next(pool.Count)];
            pool.Remove(t);
            Goal g = new Goal { type = t };
            switch (t)
            {
                case GoalType.ServeFlavor:
                    g.flavor = menu[rng.Next(menu.Count)].id;
                    g.target = Mathf.RoundToInt(3 * scale); g.reward = 40; break;
                case GoalType.ServeAny:
                    g.target = Mathf.RoundToInt(8 * scale); g.reward = 50; break;
                case GoalType.PerfectServes:
                    g.target = Mathf.RoundToInt(5 * scale); g.reward = 60; break;
                case GoalType.ReachCombo:
                    g.target = Mathf.Min(10, 3 + day / 2); g.reward = 70; break;
                case GoalType.EarnTips:
                    g.target = Mathf.RoundToInt(60 * scale); g.reward = 50; break;
                default:
                    g.target = 1; g.reward = 80; break;
            }
            goals.Add(g);
        }
        OnGoalsChanged?.Invoke();
    }

    void HandleServed(CustomerController customer, ServeResult result)
    {
        ShopProgress sp = ShopProgress.Instance;
        foreach (Goal g in goals)
        {
            if (g.done) continue;
            switch (g.type)
            {
                case GoalType.ServeFlavor:
                    if (customer != null && customer.order.flavor == g.flavor) g.progress++; break;
                case GoalType.ServeAny: g.progress++; break;
                case GoalType.PerfectServes:
                    if (result.quality == ServeQuality.Perfect) g.progress++; break;
                case GoalType.ReachCombo:
                    if (sp != null) g.progress = Mathf.Max(g.progress, sp.combo); break;
                case GoalType.EarnTips:
                    if (sp != null) g.progress = sp.today.tips; break;
            }
            if (g.type != GoalType.NoAngry && g.progress >= g.target)
                Complete(g);
        }
        OnGoalsChanged?.Invoke();
    }

    void HandleAngry(CustomerController customer)
    {
        foreach (Goal g in goals)
            if (g.type == GoalType.NoAngry && !g.done) g.progress = -1;   // failed for today
        OnGoalsChanged?.Invoke();
    }

    public void EvaluateEndOfDay(ShopProgress.DayStats stats)
    {
        foreach (Goal g in goals)
            if (g.type == GoalType.NoAngry && !g.done && stats.angry == 0 && stats.served > 0)
                Complete(g);
        OnGoalsChanged?.Invoke();
    }

    void Complete(Goal g)
    {
        g.done = true;
        g.progress = g.target;
        OnGoalCompleted?.Invoke();
        if (ShopProgress.Instance != null)
            ShopProgress.Instance.AddBonus(g.reward, Loc.T("ภารกิจสำเร็จ!", "Goal complete!"));
        else if (GameManager.Instance != null)
            GameManager.Instance.AddMoney(g.reward);
    }

    public string Describe()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(Loc.T("ภารกิจวันนี้\n", "Today's goals\n"));
        foreach (Goal g in goals) sb.AppendLine(g.Text);
        return sb.ToString();
    }
}
