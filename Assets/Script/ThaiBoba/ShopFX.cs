using UnityEngine;

// Celebration effects: coin sparkle on every sale, confetti on combos and level-ups.
public class ShopFX : MonoBehaviour
{
    public GameObject coinBurst;
    public GameObject confetti;
    [Tooltip("Where sale effects pop (top of the counter)")]
    public Transform counterPoint;
    public int confettiCombo = 3;

    void OnEnable()
    {
        ShopProgress.OnServed += Served;
        ShopProgress.OnLevelUp += LevelUp;
    }

    void OnDisable()
    {
        ShopProgress.OnServed -= Served;
        ShopProgress.OnLevelUp -= LevelUp;
    }

    Vector3 Point(CustomerController c)
    {
        if (counterPoint != null) return counterPoint.position;
        return c != null ? c.transform.position + Vector3.up * 1.2f : transform.position;
    }

    void Served(CustomerController customer, ServeResult result)
    {
        Spawn(coinBurst, Point(customer));
        if (result.quality == ServeQuality.Perfect && ShopProgress.Instance != null &&
            ShopProgress.Instance.combo >= confettiCombo)
            Spawn(confetti, Point(customer) + Vector3.up * 0.4f);
    }

    void LevelUp(int level)
    {
        Spawn(confetti, Point(null) + Vector3.up * 0.4f);
    }

    public static void Spawn(GameObject fx, Vector3 position)
    {
        if (fx == null) return;
        GameObject go = Instantiate(fx, position, fx.transform.rotation);
        Destroy(go, 6f);
    }
}
