using UnityEngine;

// Turns street lamps (and anything else listed) on in the evening, following the TimeCyc clock.
public class NightLights : MonoBehaviour
{
    public Light[] lights;
    public GameObject[] nightObjects;
    public int onHour = 18;
    public int offHour = 6;

    TimeCyc timeCyc;
    bool? isOn;

    void Start()
    {
        timeCyc = FindFirstObjectByType<TimeCyc>();
    }

    void Update()
    {
        int h = timeCyc != null ? timeCyc.Hours : 12;
        bool on = h >= onHour || h < offHour;
        if (isOn == on) return;
        isOn = on;
        if (lights != null)
            foreach (Light l in lights) if (l != null) l.enabled = on;
        if (nightObjects != null)
            foreach (GameObject g in nightObjects) if (g != null) g.SetActive(on);
    }
}
