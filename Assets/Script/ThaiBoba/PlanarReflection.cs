using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Real reflections on the wet street: while it's wet, a second camera renders the scene mirrored in the road plane
// (half resolution, no shadows / post) into _TB_ReflectionTex; the WetReflection sheets on the road and footpaths
// show it, with raindrop ripples. Buildings, shop signs, lamps, cars, people and the falling rain all reflect.
// Off on Low graphics and whenever the street is dry.
[DefaultExecutionOrder(1000)]
public class PlanarReflection : MonoBehaviour
{
    [Tooltip("World height of the mirror plane (the road surface)")]
    public float planeHeight = -0.15f;
    [Tooltip("Reflection texture = screen size / downscale")]
    public int downscale = 2;
    public float farClip = 80f;
    [Tooltip("The wet sheets (road, footpaths); hidden while dry")]
    public Renderer[] surfaces = new Renderer[0];
    [Tooltip("Layer of the wet sheets (not drawn into their own reflection)")]
    public int sheetLayer = 4;

    // tests / screenshots: mirror this camera instead of the one on screen
    public static Camera ViewOverride;

    static readonly int ReflTexId = Shader.PropertyToID("_TB_ReflectionTex");
    static readonly int WetId = Shader.PropertyToID("_TB_Wet");
    static readonly int RainId = Shader.PropertyToID("_TB_Rain");

    Camera refl;
    RenderTexture rt;
    float shown;
    bool sheetsOn = true;

    public bool Rendering => refl != null && refl.enabled;
    // the reflection is only drawn while the street is wet; Active = it would be (true even without a GPU, e.g. batch tests)
    public bool Active => shown > 0.005f && GameSettings.Quality >= 1;
    // no GPU (servers, -nographics test runs): rendering into the reflection texture would crash, so never start it
    static bool Headless => SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
    public RenderTexture Texture => rt;

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
        SetSheets(false);
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        if (refl != null) refl.enabled = false;
        Shader.SetGlobalFloat(WetId, 0f);
    }

    void OnDestroy()
    {
        if (refl != null) Destroy(refl.gameObject);
        if (rt != null) { rt.Release(); Destroy(rt); }
    }

    // the mirrored view turns triangles inside out: flip culling for that camera only
    void BeginCamera(ScriptableRenderContext ctx, Camera cam) { if (cam == refl) GL.invertCulling = true; }
    void EndCamera(ScriptableRenderContext ctx, Camera cam) { if (cam == refl) GL.invertCulling = false; }

    static Camera ViewCamera()
    {
        if (ViewOverride != null) return ViewOverride;
        Camera main = Camera.main;
        if (main != null && main.isActiveAndEnabled && main.targetTexture == null) return main;
        Camera best = null;
        foreach (Camera c in Camera.allCameras)                       // e.g. the main-menu fly-over camera
            if (c.targetTexture == null && c.cameraType == CameraType.Game && (best == null || c.depth > best.depth)) best = c;
        return best;
    }

    void LateUpdate()
    {
        WeatherSystem w = WeatherSystem.Instance;
        float wet = w != null ? w.Wetness : 0f, rain = w != null ? w.Rain : 0f;
        Camera view = ViewCamera();
        bool allowed = GameSettings.Quality >= 1 && view != null;
        shown = Mathf.MoveTowards(shown, allowed && wet > 0.05f ? Mathf.Clamp01(wet * 1.15f) : 0f, Time.unscaledDeltaTime * 0.4f);
        Shader.SetGlobalFloat(WetId, shown);
        Shader.SetGlobalFloat(RainId, rain);
        SetSheets(shown > 0.005f && !Headless);
        if (shown <= 0.005f || !allowed || Headless)
        {
            if (refl != null) refl.enabled = false;
            return;
        }
        Setup(view);
        Mirror(view);
    }

    void SetSheets(bool on)
    {
        if (on == sheetsOn) return;
        sheetsOn = on;
        foreach (Renderer r in surfaces) if (r != null) r.enabled = on;
    }

    void Setup(Camera view)
    {
        if (refl == null)
        {
            GameObject go = new GameObject("WetReflectionCamera") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            refl = go.AddComponent<Camera>();
            refl.enabled = false;
            UniversalAdditionalCameraData data = refl.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresColorTexture = false;
            data.requiresDepthTexture = false;
            data.antialiasing = AntialiasingMode.None;
        }
        int w = Mathf.Max(64, view.pixelWidth / Mathf.Max(1, downscale)), h = Mathf.Max(64, view.pixelHeight / Mathf.Max(1, downscale));
        if (rt == null || rt.width != w || rt.height != h)
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 16, RenderTextureFormat.Default) { name = "_TB_ReflectionTex", useMipMap = false };
            rt.Create();
            refl.targetTexture = rt;
            Shader.SetGlobalTexture(ReflTexId, rt);
        }
        refl.CopyFrom(view);
        refl.targetTexture = rt;
        refl.cullingMask = view.cullingMask & ~(1 << sheetLayer) & ~(1 << 5);    // not the wet sheets, not UI
        refl.farClipPlane = Mathf.Min(view.farClipPlane, farClip);
        refl.depth = view.depth - 1f;
        refl.useOcclusionCulling = false;
        refl.allowMSAA = false;
        refl.allowHDR = false;
        refl.enabled = true;
    }

    // reflect the view camera in the plane y = planeHeight, clipping everything below it
    void Mirror(Camera view)
    {
        Vector3 normal = Vector3.up, point = new Vector3(0f, planeHeight, 0f);
        Vector4 plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
        Matrix4x4 r = Reflection(plane);
        refl.worldToCameraMatrix = view.worldToCameraMatrix * r;
        refl.projectionMatrix = view.CalculateObliqueMatrix(CameraSpacePlane(refl.worldToCameraMatrix, point, normal));
        refl.transform.position = r.MultiplyPoint(view.transform.position);          // for LOD / distance checks
        Vector3 e = view.transform.eulerAngles;
        refl.transform.eulerAngles = new Vector3(-e.x, e.y, e.z);
    }

    static Vector4 CameraSpacePlane(Matrix4x4 worldToCamera, Vector3 point, Vector3 normal)
    {
        Vector3 p = worldToCamera.MultiplyPoint(point + normal * 0.03f);
        Vector3 n = worldToCamera.MultiplyVector(normal).normalized;
        return new Vector4(n.x, n.y, n.z, -Vector3.Dot(p, n));
    }

    static Matrix4x4 Reflection(Vector4 p)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = 1f - 2f * p.x * p.x; m.m01 = -2f * p.x * p.y;      m.m02 = -2f * p.x * p.z;      m.m03 = -2f * p.w * p.x;
        m.m10 = -2f * p.y * p.x;     m.m11 = 1f - 2f * p.y * p.y; m.m12 = -2f * p.y * p.z;      m.m13 = -2f * p.w * p.y;
        m.m20 = -2f * p.z * p.x;     m.m21 = -2f * p.z * p.y;      m.m22 = 1f - 2f * p.z * p.z; m.m23 = -2f * p.w * p.z;
        return m;
    }
}
