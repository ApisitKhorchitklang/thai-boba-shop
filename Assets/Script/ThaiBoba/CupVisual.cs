using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Shows the contents of Cup_Modular.fbx based on CupController state:
// children Liquid_<flavor>, Cup_Pearls/2/3 (one per scoop), Cup_Ice, Cup_Foam, Cup_SealFilm, Cup_Straw.
// Changes are animated: tea pours in with a stream and the level rises, pearls and ice drop in and bounce,
// cheese foam puffs up, the seal film presses down and the straw slides in.
[RequireComponent(typeof(CupController))]
public class CupVisual : MonoBehaviour
{
    private CupController cup;
    private GameObject ice, seal, straw, foam, frappe, marsh;
    private readonly GameObject[] pearls = new GameObject[3];     // Cup_Pearls, Cup_Pearls2, Cup_Pearls3: one per scoop
    private readonly Dictionary<string, GameObject> liquids = new Dictionary<string, GameObject>();
    private readonly Dictionary<Transform, Vector3> restPos = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> restScale = new Dictionary<Transform, Vector3>();

    // what the cup looked like last time, to know what just changed
    bool primed;
    string lastFlavor = "";
    int lastBoba;
    bool lastIce, lastFoam, lastSealed, lastBlended, lastMarsh;

    LiquidSlosh slosh;
    GameObject stream;
    MaterialPropertyBlock block;
    static Material streamMat;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        cup = GetComponent<CupController>();
        slosh = GetComponent<LiquidSlosh>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n.StartsWith("Liquid_")) liquids[n.Substring(7)] = t.gameObject;
            else if (n == "Cup_Pearls") pearls[0] = t.gameObject;
            else if (n == "Cup_Pearls2") pearls[1] = t.gameObject;
            else if (n == "Cup_Pearls3") pearls[2] = t.gameObject;
            else if (n == "Cup_Ice") ice = t.gameObject;
            else if (n == "Cup_Foam") foam = t.gameObject;
            else if (n == "Cup_Frappe") frappe = t.gameObject;
            else if (n == "Cup_Marshmallow") marsh = t.gameObject;
            else if (n == "Cup_SealFilm") seal = t.gameObject;
            else if (n == "Cup_Straw") straw = t.gameObject;
            else continue;
            restPos[t] = t.localPosition;
            restScale[t] = t.localScale;
        }
        cup.OnChanged += Refresh;
        Refresh(cup);
        primed = true;
    }

    void OnDestroy()
    {
        if (cup != null) cup.OnChanged -= Refresh;
    }

    void Refresh(CupController c)
    {
        for (int i = 0; i < pearls.Length; i++)
            if (pearls[i] != null) pearls[i].SetActive(c.bobaCount > i);
        if (ice != null) ice.SetActive(c.hasIce);
        if (foam != null) foam.SetActive(c.hasFoam);
        if (frappe != null) frappe.SetActive(c.blended);
        if (marsh != null) marsh.SetActive(c.marshmallow);

        bool filled = c.State == CupController.CupState.Filled || c.State == CupController.CupState.LidClosed;
        foreach (KeyValuePair<string, GameObject> kv in liquids)
            kv.Value.SetActive(filled && kv.Key == c.flavor);

        bool sealedCup = c.State == CupController.CupState.LidClosed;
        if (seal != null) seal.SetActive(sealedCup);
        if (straw != null) straw.SetActive(sealedCup);

        if (primed && isActiveAndEnabled)
        {
            if (string.IsNullOrEmpty(lastFlavor) && !string.IsNullOrEmpty(c.flavor)) StartCoroutine(Pour(c.flavor));
            if (c.bobaCount > lastBoba && c.bobaCount <= pearls.Length && pearls[c.bobaCount - 1] != null)
                StartCoroutine(Drop(pearls[c.bobaCount - 1].transform, 0.09f, 0.32f));
            if (c.hasIce && !lastIce && ice != null) StartCoroutine(Drop(ice.transform, 0.12f, 0.35f));
            if (c.hasFoam && !lastFoam && foam != null) StartCoroutine(Puff(foam.transform));
            if (c.blended && !lastBlended && frappe != null) StartCoroutine(Puff(frappe.transform));
            if (c.marshmallow && !lastMarsh && marsh != null) StartCoroutine(Drop(marsh.transform, 0.1f, 0.3f));
            if (sealedCup && !lastSealed)
            {
                if (seal != null) StartCoroutine(Press(seal.transform));
                if (straw != null) StartCoroutine(Drop(straw.transform, 0.16f, 0.25f, 0.12f));
            }
        }
        lastFlavor = c.flavor ?? "";
        lastBoba = c.bobaCount;
        lastIce = c.hasIce;
        lastFoam = c.hasFoam;
        lastBlended = c.blended;
        lastMarsh = c.marshmallow;
        lastSealed = sealedCup;
    }

    // ---------------------------------------------------------------- animations
    IEnumerator Drop(Transform t, float height, float seconds, float delay = 0f)
    {
        Vector3 rest = restPos[t];
        t.localPosition = rest + Vector3.up * height;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        for (float e = 0f; e < seconds; e += Time.deltaTime)
        {
            float k = e / seconds;
            // fall, then a small bounce
            float y = k < 0.7f ? 1f - (k / 0.7f) * (k / 0.7f) : Mathf.Sin((k - 0.7f) / 0.3f * Mathf.PI) * 0.12f;
            t.localPosition = rest + Vector3.up * height * y;
            yield return null;
        }
        t.localPosition = rest;
    }

    IEnumerator Puff(Transform t)
    {
        Vector3 rest = restScale[t];
        for (float e = 0f; e < 0.35f; e += Time.deltaTime)
        {
            float k = e / 0.35f;
            float s = k < 0.6f ? Mathf.Lerp(0f, 1.18f, k / 0.6f) : Mathf.Lerp(1.18f, 1f, (k - 0.6f) / 0.4f);
            t.localScale = new Vector3(rest.x * Mathf.Lerp(0.6f, 1f, k), rest.y * s, rest.z * Mathf.Lerp(0.6f, 1f, k));
            yield return null;
        }
        t.localScale = rest;
    }

    IEnumerator Press(Transform t)
    {
        Vector3 rest = restPos[t], scale = restScale[t];
        for (float e = 0f; e < 0.18f; e += Time.deltaTime)
        {
            float k = e / 0.18f;
            t.localPosition = rest + Vector3.up * 0.04f * (1f - k);
            t.localScale = Vector3.Lerp(scale * 1.25f, scale, k);
            yield return null;
        }
        t.localPosition = rest;
        t.localScale = scale;
    }

    // a stream of tea from above while the level rises (LiquidSlosh.AnimateFill)
    IEnumerator Pour(string flavor)
    {
        const float seconds = 0.75f;
        if (slosh != null) slosh.AnimateFill(seconds);
        if (stream == null)
        {
            stream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stream.name = "PourStream";
            Destroy(stream.GetComponent<Collider>());
            stream.transform.SetParent(transform, false);
            if (streamMat == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit != null) streamMat = new Material(lit);
            }
            Renderer sr = stream.GetComponent<Renderer>();
            if (streamMat != null) sr.sharedMaterial = streamMat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            block = new MaterialPropertyBlock();
        }
        DrinkMenu.Drink d = DrinkMenu.Get(flavor);
        Renderer r = stream.GetComponent<Renderer>();
        r.GetPropertyBlock(block);
        block.SetColor(BaseColor, d != null ? d.color : new Color(0.8f, 0.5f, 0.3f));
        r.SetPropertyBlock(block);
        stream.SetActive(true);
        for (float e = 0f; e < seconds; e += Time.deltaTime)
        {
            float level = 0.01f + 0.13f * (slosh != null ? slosh.Fill : e / seconds);   // liquid surface height in the cup
            float top = 0.34f;
            float thin = e > seconds * 0.8f ? Mathf.Lerp(1f, 0.2f, (e - seconds * 0.8f) / (seconds * 0.2f)) : 1f;
            stream.transform.localPosition = new Vector3(0f, (top + level) * 0.5f, 0f);
            stream.transform.localScale = new Vector3(0.011f * thin, (top - level) * 0.5f, 0.011f * thin);
            yield return null;
        }
        stream.SetActive(false);
    }
}
