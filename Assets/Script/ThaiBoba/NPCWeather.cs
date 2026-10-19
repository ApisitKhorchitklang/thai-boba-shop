using UnityEngine;
using UnityEngine.AI;

// NPC reaction to rain: most open an umbrella, the rest hurry (run) to get out of the rain.
public class NPCWeather : MonoBehaviour
{
    [Tooltip("Umbrella prefab (SM_HandUmbrella); the grip sits at the right hand")]
    public GameObject umbrellaPrefab;
    [Range(0f, 1f)] public float umbrellaChance = 0.65f;
    public float hurryMultiplier = 1.7f;
    public Color[] umbrellaColors =
    {
        new Color(0.9f, 0.2f, 0.35f), new Color(0.2f, 0.45f, 0.9f), new Color(0.98f, 0.75f, 0.1f),
        new Color(0.2f, 0.65f, 0.35f), new Color(0.15f, 0.15f, 0.18f), new Color(0.6f, 0.35f, 0.8f)
    };

    NavMeshAgent agent;
    GameObject umbrella;
    bool hasUmbrella;
    float baseSpeed;
    bool hurrying;

    public bool UmbrellaOpen => umbrella != null && umbrella.activeSelf;
    // sitting under a roof / the market umbrella: no umbrella of their own
    [System.NonSerialized] public bool Indoors;

    void Start()
    {
        agent = GetComponentInParent<NavMeshAgent>();
        hasUmbrella = Random.value < umbrellaChance;
        if (hasUmbrella && umbrellaPrefab != null)
        {
            umbrella = Instantiate(umbrellaPrefab, transform);
            umbrella.transform.localPosition = new Vector3(0.24f, 1.08f, 0.08f);   // in the right hand, canopy over the head
            umbrella.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), -6f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", umbrellaColors[Random.Range(0, umbrellaColors.Length)]);
            foreach (Renderer r in umbrella.GetComponentsInChildren<Renderer>())
            {
                // only tint the canopy material
                for (int i = 0; i < r.sharedMaterials.Length; i++)
                    if (r.sharedMaterials[i] != null && r.sharedMaterials[i].name.StartsWith("M_umbrella") &&
                        !r.sharedMaterials[i].name.StartsWith("M_umbrella2"))
                        r.SetPropertyBlock(block, i);
            }
            umbrella.SetActive(false);
        }
    }

    void Update()
    {
        bool raining = WeatherSystem.Instance != null && WeatherSystem.Instance.IsRaining;
        bool open = raining && !Indoors;
        if (umbrella != null && umbrella.activeSelf != open) umbrella.SetActive(open);
        bool hurry = raining && !hasUmbrella;
        if (agent != null && hurry != hurrying)
        {
            hurrying = hurry;
            if (hurry) baseSpeed = agent.speed;          // walkers pick their own pace on Start
            agent.speed = hurry ? baseSpeed * hurryMultiplier : baseSpeed;
        }
    }
}
