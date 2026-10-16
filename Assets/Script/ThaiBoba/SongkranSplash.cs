using UnityEngine;

// Songkran water fights: every so often kids run up and splash the shop front. A warning comes first; anyone standing
// near the counter with an open (unsealed) cup gets it soaked and has to make it again. Step back or seal first!
public class SongkranSplash : MonoBehaviour
{
    public ParticleSystem[] blasts;             // water bursts at the counter front
    public float minGap = 28f, maxGap = 48f;    // seconds between splashes
    public float warning = 2.5f;
    [Tooltip("Unity z: the player is in the splash zone when closer to the front than this")]
    public float safeZ = -2.6f;

    public static event System.Action<bool> OnSplash;   // true = the player's cup got soaked

    float timer = 15f;
    bool warned;
    TimeCyc clock;
    Transform player;
    RaycastAim aim;

    void Start()
    {
        clock = FindFirstObjectByType<TimeCyc>();
        aim = FindFirstObjectByType<RaycastAim>();
        MovementWithJoy m = FindFirstObjectByType<MovementWithJoy>();
        player = m != null ? m.transform : aim != null ? aim.transform : null;
    }

    bool Active
    {
        get
        {
            DailyEventSystem ev = DailyEventSystem.Instance;
            int h = clock != null ? clock.Hours : 12;
            return GameMenus.GameRunning && !GameMenus.IsOpen && ev != null && ev.Current.type == DailyEventSystem.EventType.Songkran && h >= 9 && h < 19;
        }
    }

    void Update()
    {
        if (!Active) { warned = false; return; }
        timer -= Time.deltaTime;
        if (!warned && timer <= warning)
        {
            warned = true;
            GameAudio.Play(Sfx.Error);
            ShopHUD.ToastNow(Loc.T("เด็กๆ ถือปืนฉีดน้ำมาแล้ว! ถอยห่างเคาน์เตอร์หรือซีลแก้วก่อน", "Kids with water guns incoming! Step back from the counter or seal your cup"), warning);
        }
        if (timer <= 0f)
        {
            timer = Random.Range(minGap, maxGap);
            warned = false;
            Splash();
        }
    }

    public void Splash()
    {
        foreach (ParticleSystem ps in blasts) if (ps != null) ps.Emit(90);
        GameAudio.Play(Sfx.Pour);
        bool inZone = player != null && player.position.z > safeZ;
        CupController cup = aim != null ? aim.HeldCup : null;
        bool soaked = false;
        if (inZone && cup != null && cup.State != CupController.CupState.LidClosed && cup.State != CupController.CupState.Empty)
        {
            cup.Splash();
            soaked = true;
            ShopHUD.ToastNow(Loc.T("โดนสาดน้ำ! แก้วนี้เปียกแล้ว ทิ้งแล้วทำใหม่", "Splash! That cup is soaked, throw it out and make a new one"), 2.2f);
        }
        else
            ShopHUD.ToastNow(inZone ? Loc.T("สาดมาแล้ว! โชคดีแก้วปลอดภัย", "Splash! Your drink survived") : Loc.T("หลบทัน! สุขสันต์วันสงกรานต์", "Dodged it! Happy Songkran"));
        OnSplash?.Invoke(soaked);
    }
}
