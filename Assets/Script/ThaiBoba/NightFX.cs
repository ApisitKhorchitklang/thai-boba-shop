using UnityEngine;

// Shared bits for the night lighting: how dark it is (0 = day, 1 = night), a flat quad mesh and the
// "reflection streak" trick for wet asphalt: a long soft glow on the ground under a light, stretched toward the
// camera, like the shimmering columns of light you see on a rainy Bangkok street at night.
public static class NightFX
{
    static TimeCyc clock;
    static Mesh quad;
    static int cachedFrame = -1;
    static float cachedNight;

    public static float Night01
    {
        get
        {
            if (Time.frameCount == cachedFrame) return cachedNight;
            cachedFrame = Time.frameCount;
            if (clock == null) clock = Object.FindFirstObjectByType<TimeCyc>();
            float h = clock != null ? clock.TimeOfDay : 12f;
            float dusk = Mathf.Clamp01((h - 17.8f) / 1.2f), dawn = Mathf.Clamp01((6.2f - h) / 1.2f);
            float storm = WeatherSystem.Instance != null ? WeatherSystem.Instance.Rain * 0.35f : 0f;   // dark skies in a storm
            cachedNight = Mathf.Clamp01(Mathf.Max(Mathf.Max(dusk, dawn), storm));
            return cachedNight;
        }
    }

    public static float Wet01 => WeatherSystem.Instance != null ? WeatherSystem.Instance.Wetness : 0f;

    // unit quad in the XZ plane (lying flat, facing up), centred on the origin
    public static Mesh FlatQuad
    {
        get
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "NightFX_FlatQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            return quad;
        }
    }

    public static GameObject MakeQuad(string name, Transform parent, Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = FlatQuad;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    static MaterialPropertyBlock block;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public static void SetColor(Renderer r, Color c)
    {
        if (block == null) block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetColor(BaseColor, c);
        r.SetPropertyBlock(block);
    }

    // lay a streak on the ground under a light, its long side pointing at the camera
    public static void OrientStreak(Transform streak, Vector3 light, float groundY, float length, float width)
    {
        Camera cam = Camera.main;
        Vector3 g = new Vector3(light.x, groundY, light.z);
        Vector3 to = cam != null ? cam.transform.position - g : Vector3.back;
        to.y = 0f;
        Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector3.back;
        float len = Mathf.Min(length, Mathf.Max(0.5f, to.magnitude - 0.5f));
        streak.SetPositionAndRotation(g + dir * len * 0.5f, Quaternion.LookRotation(dir));
        streak.localScale = new Vector3(width, 1f, len);
    }
}
