using System.Collections;
using UnityEngine;

// The chicken-rice shop next door keeps its own hours: the shutter rolls up early in the morning, the uncle chops
// chicken at the stall out front (cleaver sounds), riders wait for lunch orders around noon, and after lunch the
// stall gets its blue tarp and the shutter comes down again.
public class NeighbourStall : MonoBehaviour
{
    public float openHour = 6.5f, closeHour = 14.5f;
    public float lunchFrom = 11f, lunchTo = 13.5f;
    public Transform shutterDoor;             // SM_RollShutter/Shutter_Door (origin at the top: scale Y rolls it)
    public GameObject food, tarp;             // Stall_Food / Stall_Tarp
    public GameObject vendor;                 // NPC chopping at the stall
    public Transform chopPoint;               // where the cleaver sound comes from
    public GameObject[] lunchRiders;          // riders + scooters waiting for orders around noon
    public float rollSeconds = 3f;

    TimeCyc clock;
    bool? open;
    bool? lunch;
    NPCAnimDriver vendorAnim;
    float chopTimer, chopBurst;
    Coroutine rolling;

    public bool IsOpen => open == true;

    void Start()
    {
        clock = FindFirstObjectByType<TimeCyc>();
        if (vendor != null) vendorAnim = vendor.GetComponentInChildren<NPCAnimDriver>(true);
        foreach (GameObject r in lunchRiders ?? new GameObject[0])
        {
            if (r == null) continue;
            NPCAnimDriver a = r.GetComponentInChildren<NPCAnimDriver>(true);
            if (a != null) a.Play(NPCAnimDriver.Gesture.Phone);
        }
    }

    void Update()
    {
        float h = clock != null ? clock.TimeOfDay : 9f;
        bool nowOpen = h >= openHour && h < closeHour;
        if (open != nowOpen) SetOpen(nowOpen, open.HasValue);
        bool nowLunch = nowOpen && h >= lunchFrom && h < lunchTo;
        if (lunch != nowLunch)
        {
            lunch = nowLunch;
            foreach (GameObject r in lunchRiders ?? new GameObject[0]) if (r != null) r.SetActive(nowLunch);
        }
        if (nowOpen) Chop();
    }

    void SetOpen(bool on, bool animate)
    {
        open = on;
        if (food != null) food.SetActive(on);
        if (tarp != null) tarp.SetActive(!on);
        if (vendor != null) vendor.SetActive(on);
        if (shutterDoor != null)
        {
            if (rolling != null) StopCoroutine(rolling);
            if (animate && isActiveAndEnabled) rolling = StartCoroutine(Roll(on ? 0.02f : 1f));
            else SetShutter(on ? 0.02f : 1f);
        }
    }

    // the shutter rolls into its box at the top (origin there), so scaling Y opens / closes it
    IEnumerator Roll(float target)
    {
        float from = shutterDoor.localScale.y;
        for (float e = 0f; e < rollSeconds; e += Time.deltaTime)
        {
            SetShutter(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, e / rollSeconds)));
            yield return null;
        }
        SetShutter(target);
        rolling = null;
    }

    void SetShutter(float y)
    {
        Vector3 s = shutterDoor.localScale;
        s.y = y;
        shutterDoor.localScale = s;
        shutterDoor.gameObject.SetActive(y > 0.03f);
    }

    // bursts of chopping with pauses in between, matched to the vendor's arm
    void Chop()
    {
        if (vendorAnim == null) return;
        if (chopBurst > 0f)
        {
            chopBurst -= Time.deltaTime;
            chopTimer -= Time.deltaTime;
            if (chopTimer <= 0f)
            {
                chopTimer = 0.5f;                                     // the Chop clip lands twice a second
                if (chopPoint != null) GameAudio.PlayAt(Sfx.Chop, chopPoint.position, 0.55f, Random.Range(0.92f, 1.08f));
            }
            if (chopBurst <= 0f) { vendorAnim.Play(NPCAnimDriver.Gesture.None); chopTimer = Random.Range(3f, 8f); }
            return;
        }
        chopTimer -= Time.deltaTime;
        if (chopTimer > 0f) return;
        chopBurst = Random.Range(2f, 4.5f);
        chopTimer = 0.3f;
        vendorAnim.Play(NPCAnimDriver.Gesture.Chop);
    }
}
