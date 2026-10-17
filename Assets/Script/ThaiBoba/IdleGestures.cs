using UnityEngine;

// Background people who just stand around (riders waiting for food, the rival's queue): every so often they check
// their phone or their watch, so they never stand frozen.
public class IdleGestures : MonoBehaviour
{
    public float phoneShare = 0.7f;
    NPCAnimDriver anim;
    float timer;

    void OnEnable()
    {
        if (anim == null) anim = GetComponentInChildren<NPCAnimDriver>();
        timer = Random.Range(0.2f, 2f);
    }

    void Update()
    {
        if (anim == null) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        float r = Random.value;
        if (r < phoneShare) { anim.Play(NPCAnimDriver.Gesture.Phone, Random.Range(5f, 12f)); timer = Random.Range(8f, 15f); }
        else if (r < phoneShare + 0.15f) { anim.Play(NPCAnimDriver.Gesture.Watch, 2f); timer = Random.Range(4f, 8f); }
        else timer = Random.Range(3f, 6f);
    }
}
