using UnityEngine;
using UnityEngine.Rendering;

// Applies the graphics setting from GameSettings: low = no post-processing / shadows (old phones),
// medium = bloom + hard shadows, high = everything.
public class GraphicsQuality : MonoBehaviour
{
    public Volume postProcessing;
    public Light sun;

    void Start()
    {
        GameSettings.Changed += Apply;
        Apply();
    }

    void OnDestroy()
    {
        GameSettings.Changed -= Apply;
    }

    void Apply()
    {
        int q = GameSettings.Quality;
        if (postProcessing != null) postProcessing.enabled = q >= 1;
        if (sun != null) sun.shadows = q == 0 ? LightShadows.None : q == 1 ? LightShadows.Hard : LightShadows.Soft;
        QualitySettings.shadowDistance = q == 2 ? 60f : 35f;

    }
}
