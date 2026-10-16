using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Effects for the demo street: particles (steam, coins, confetti, rain, exhaust, Songkran splash),
// post-processing (bloom on the neon), haze/fog and night street lights.
public static partial class ThaiBobaSetup
{
    const string FxDir = "Assets/Prefabs/ThaiBoba/FX/";

    static Texture2D SoftTexture()
    {
        string path = FxDir + "FX_Soft.png";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) return tex;
        Directory.CreateDirectory(FxDir);
        Texture2D t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                float a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1, 1, 1, a * a * (3 - 2 * a)));
            }
        File.WriteAllBytes(path, t.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material ParticleMaterial()
    {
        string path = FxDir + "M_FX_Particle.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        m = new Material(sh);
        m.SetTexture("_BaseMap", SoftTexture());
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject FxPrefab(string name, System.Action<ParticleSystem, ParticleSystemRenderer> setup)
    {
        Directory.CreateDirectory(FxDir);
        GameObject go = new GameObject(name);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMaterial();
        setup(ps, r);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, FxDir + name + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static Gradient Fade(Color c, float peak = 1f)
    {
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(peak, 0.15f), new GradientAlphaKey(0, 1) });
        return g;
    }

    public class FxSet
    {
        public GameObject steam, coins, confetti, rain, exhaust, splash, rainSplash;
    }

    public static FxSet CreateFxPrefabs()
    {
        FxSet fx = new FxSet();
        fx.steam = FxPrefab("FX_Steam", (ps, r) =>
        {
            var main = ps.main;
            main.startLifetime = 2.2f; main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f); main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            var em = ps.emission; em.rateOverTime = 7f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.12f;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 2.6f));
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, 0.35f);
            ps.transform.rotation = Quaternion.Euler(-90, 0, 0);
        });
        fx.exhaust = FxPrefab("FX_Exhaust", (ps, r) =>
        {
            var main = ps.main;
            main.startLifetime = 1.3f; main.startSpeed = 0.4f; main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.maxParticles = 25; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 9f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 15f; sh.radius = 0.03f;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 3f));
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(new Color(0.45f, 0.45f, 0.45f), 0.3f);
            ps.transform.rotation = Quaternion.Euler(0, 180, 0);     // out of the back
        });
        fx.coins = FxPrefab("FX_CoinBurst", (ps, r) =>
        {
            var main = ps.main;
            main.loop = false; main.duration = 0.5f; main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.2f); main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.98f, 0.6f));
            main.gravityModifier = 0.9f;
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.1f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white);
            ps.transform.rotation = Quaternion.Euler(-90, 0, 0);
        });
        fx.confetti = FxPrefab("FX_Confetti", (ps, r) =>
        {
            var main = ps.main;
            main.loop = false; main.duration = 0.5f; main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5.5f); main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.3f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.33f),
                              new GradientColorKey(new Color(0.3f, 0.9f, 1f), 0.66f), new GradientColorKey(new Color(0.5f, 1f, 0.4f), 1f) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
            main.gravityModifier = 0.5f;
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 90) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.1f;
            ps.transform.rotation = Quaternion.Euler(-90, 0, 0);
        });
        fx.rain = FxPrefab("FX_Rain", (ps, r) =>
        {
            var main = ps.main;
            main.startLifetime = 1.3f; main.startSpeed = 14f; main.startSize = 0.035f; main.maxParticles = 2500;
            main.startColor = new Color(0.8f, 0.88f, 1f, 0.55f);
            var em = ps.emission; em.rateOverTime = 1100f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(70f, 22f, 1f);
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 1f;
            ps.transform.rotation = Quaternion.Euler(90, 0, 0);
        });
        fx.rainSplash = FxPrefab("FX_RainSplash", (ps, r) =>
        {
            var main = ps.main;
            main.startLifetime = 0.25f; main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f); main.maxParticles = 600;
            main.startColor = new Color(0.85f, 0.9f, 1f, 0.6f); main.gravityModifier = 0.6f;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(70f, 18f, 0.05f);
            ps.transform.rotation = Quaternion.Euler(-90, 0, 0);
        });
        fx.splash = FxPrefab("FX_WaterSplash", (ps, r) =>
        {
            var main = ps.main;
            main.startLifetime = 1f; main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f); main.gravityModifier = 1.2f;
            main.startColor = new Color(0.6f, 0.85f, 1f, 0.8f);
            var em = ps.emission; em.rateOverTime = 35f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25f; sh.radius = 0.05f;
            ps.transform.rotation = Quaternion.Euler(-60, 0, 0);
        });
        return fx;
    }

    static void AddChildFx(GameObject fxPrefab, Transform parent, Vector3 localPos)
    {
        GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(fxPrefab, parent);
        g.transform.localPosition = localPos;
    }

    // Everything visual that makes the street feel alive
    public static void AddEffects(StreetRefs refs, Camera cam, FxSet fx)
    {
        Transform root = refs.root.transform;

        // steam: boba pot, food stalls, chicken-rice shop pots
        AddChildFx(fx.steam, root, P(0.5f, 6.5f, 1.22f));
        foreach (Transform stall in refs.steamSpots)
            AddChildFx(fx.steam, stall, P(-0.4f, 0, 1.22f));
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
            if (t.name.StartsWith("SM_Shop_ChickenRice"))
                AddChildFx(fx.steam, t, P(-1.6f, -0.55f, 1.25f));

        // sale sparkle + confetti
        Transform counterTop = Marker("FX_CounterPoint", root, 0, 1.1f, 0);
        counterTop.localPosition += Vector3.up * 1.25f;
        ShopFX shopFx = refs.root.AddComponent<ShopFX>();
        shopFx.coinBurst = fx.coins;
        shopFx.confetti = fx.confetti;
        shopFx.counterPoint = counterTop;

        // weather / festival hooks of the daily events
        DailyEventSystem events = Object.FindFirstObjectByType<DailyEventSystem>();
        // rain itself is driven by WeatherSystem (BuildWeather); the daily event only switches the Songkran decor
        GameObject songkran = new GameObject("SongkranSplash");
        songkran.transform.SetParent(root, false);
        foreach (float bx in new[] { -3f, 3.5f, 9f, -9f })
            AddChildFx(fx.splash, songkran.transform, P(bx, -2.6f, 0.3f));
        songkran.SetActive(false);
        if (events != null)
        {
            events.songkranDecor = songkran;
        }

        // Songkran water fights: bursts aimed at the counter front (fired by SongkranSplash, never on their own)
        GameObject blastRoot = new GameObject("SongkranWaterFight");
        blastRoot.transform.SetParent(root, false);
        var blasts = new List<ParticleSystem>();
        foreach (float bx in new[] { -0.7f, 0.7f })
        {
            GameObject b = (GameObject)PrefabUtility.InstantiatePrefab(fx.splash, blastRoot.transform);
            b.transform.localPosition = P(bx, 0.9f, 1.05f);
            ParticleSystem ps = b.GetComponent<ParticleSystem>();
            var em = ps.emission; em.rateOverTime = 0f;
            var main = ps.main; main.playOnAwake = true; main.loop = true;
            blasts.Add(ps);
        }
        blastRoot.AddComponent<SongkranSplash>().blasts = blasts.ToArray();

        // Loy Krathong night: sky lanterns rising over the soi + warm lights along the shopfronts
        GameObject loy = new GameObject("LoyKrathongDecor");
        loy.transform.SetParent(root, false);
        GameObject lanternAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "SM_SkyLantern.fbx");
        if (lanternAsset != null)
        {
            MeshFilter lmf = lanternAsset.GetComponentInChildren<MeshFilter>();
            MeshRenderer lmr = lanternAsset.GetComponentInChildren<MeshRenderer>();
            GameObject sky = FxPrefab("FX_SkyLanterns", (ps, r) =>
            {
                var main = ps.main;
                main.startLifetime = 28f; main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.1f); main.maxParticles = 120;
                main.gravityModifier = -0.01f; main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = 0f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(70f, 6f, 0.1f);
                ps.transform.rotation = Quaternion.Euler(-90, 0, 0);
                var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.1f;
                r.renderMode = ParticleSystemRenderMode.Mesh;
                r.mesh = lmf != null ? lmf.sharedMesh : null;
                r.alignment = ParticleSystemRenderSpace.World;
                if (lmr != null) r.sharedMaterial = lmr.sharedMaterial;
            });
            GameObject skyInst = (GameObject)PrefabUtility.InstantiatePrefab(sky, loy.transform);
            skyInst.transform.localPosition = P(0, -6.7f, 6f);
            loy.AddComponent<SkyLanterns>().lanterns = skyInst.GetComponent<ParticleSystem>();
        }
        for (int k = -3; k <= 3; k++)
        {
            if (k == 0 || k == -1) continue;                                 // our shop / the expansion have their own
            Place("SM_FairyLights", k * 4.6f, -0.3f, 2.62f, 0, loy.transform, Col.None);
            Place("SM_FairyLights", k * 4.6f, -13.1f, 2.62f, 180, loy.transform, Col.None);
        }
        loy.SetActive(false);
        if (events != null) events.loyKrathongDecor = loy;

        // street lamps light up at night
        var lights = new List<Light>();
        foreach (GameObject lamp in refs.lamps)
        {
            GameObject lg = new GameObject("LampLight");
            lg.transform.SetParent(lamp.transform, false);
            lg.transform.localPosition = P(0, -1.45f, 6.6f);
            lg.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Spot;
            l.range = 13f;
            l.spotAngle = 110f;
            l.intensity = 6f;
            l.color = new Color(1f, 0.82f, 0.55f);
            l.enabled = false;
            lights.Add(l);
        }
        refs.root.AddComponent<NightLights>().lights = lights.ToArray();

        // post-processing: bloom makes the neon and string lights glow; neutral tonemap + a touch of saturation
        string profPath = FxDir + "ThaiBobaPost.asset";
        AssetDatabase.DeleteAsset(profPath);
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profPath);
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.65f);
        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
        ColorAdjustments ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.Override(12f);
        ca.contrast.Override(8f);
        Vignette vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.2f);
        foreach (VolumeComponent c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        GameObject vgo = new GameObject("PostFX Volume");
        Volume vol = vgo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;
        if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        // warm city haze hides the far ends of the street
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 50f;
        RenderSettings.fogEndDistance = 150f;
        RenderSettings.fogColor = new Color(0.8f, 0.84f, 0.9f);
    }
}
