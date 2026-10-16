using System.Collections;
using UnityEngine;

// Emote bubble that pops up over a customer's head (Illustrator sprites emote_*).
// The reviewer also lifts a phone: camera emote + a white flash + shutter sound.
public class CustomerEmote : MonoBehaviour
{
    public enum Kind { Love, Happy, Meh, Angry, Camera }

    public Sprite love, happy, meh, angry, camera;
    public float height = 2.25f;
    public float size = 0.42f;

    SpriteRenderer bubble;
    Light flash;
    Coroutine running;

    public Kind? Showing { get; private set; }

    void Awake()
    {
        GameObject go = new GameObject("EmoteBubble");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * height;
        bubble = go.AddComponent<SpriteRenderer>();
        bubble.enabled = false;
        bubble.sortingOrder = 50;
    }

    Sprite SpriteFor(Kind k) => k == Kind.Love ? love : k == Kind.Happy ? happy : k == Kind.Meh ? meh : k == Kind.Angry ? angry : camera;

    public void Show(Kind k, float seconds = 2f, float delay = 0f)
    {
        if (!isActiveAndEnabled) return;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Pop(k, seconds, delay));
    }

    // reviewer: snap a photo of the drink
    public void TakePhoto()
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(Photo());
    }

    IEnumerator Pop(Kind k, float seconds, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Sprite s = SpriteFor(k);
        if (s == null) yield break;
        bubble.sprite = s;
        bubble.enabled = true;
        Showing = k;
        Transform t = bubble.transform;
        float baseScale = size / Mathf.Max(0.01f, s.bounds.size.y);
        for (float e = 0f; e < seconds; e += Time.deltaTime)
        {
            // pop in with an overshoot, bob gently, shrink away at the end
            float pop = e < 0.18f ? Mathf.Lerp(0.2f, 1.15f, e / 0.18f) : e < 0.3f ? Mathf.Lerp(1.15f, 1f, (e - 0.18f) / 0.12f) : 1f;
            float outK = e > seconds - 0.2f ? Mathf.Clamp01((seconds - e) / 0.2f) : 1f;
            t.localScale = Vector3.one * baseScale * pop * outK;
            t.localPosition = Vector3.up * (height + Mathf.Sin(e * 5f) * 0.03f);
            Camera cam = Camera.main;
            if (cam != null) t.rotation = cam.transform.rotation;
            yield return null;
        }
        bubble.enabled = false;
        Showing = null;
        running = null;
    }

    IEnumerator Photo()
    {
        yield return new WaitForSeconds(0.5f);
        Show(Kind.Camera, 1.6f);
        yield return new WaitForSeconds(0.45f);
        if (flash == null)
        {
            GameObject f = new GameObject("PhotoFlash");
            f.transform.SetParent(transform, false);
            f.transform.localPosition = new Vector3(0f, 1.5f, 0.3f);
            flash = f.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.range = 4f;
            flash.color = Color.white;
            flash.shadows = LightShadows.None;
        }
        GameAudio.Play(Sfx.Camera);
        flash.enabled = true;
        for (float e = 0f; e < 0.15f; e += Time.deltaTime)
        {
            flash.intensity = Mathf.Lerp(12f, 0f, e / 0.15f);
            yield return null;
        }
        flash.enabled = false;
    }
}
