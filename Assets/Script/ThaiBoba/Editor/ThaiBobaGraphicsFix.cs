using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The Unity 6 editor on this PC (RTX 4060, driver 32.0.16.1088) hits "D3D12 device removed / DEVICE_HUNG (887a0006)"
// while drawing ThaiBobaStreet. DirectX 11 renders the same scene fine, so put D3D11 first for Windows
// (the editor restarts in DX11). Undo: Project Settings > Player > Windows > Graphics APIs.
public static class ThaiBobaGraphicsFix
{
    [MenuItem("Tools/Thai Boba Shop/Use DirectX 11 on Windows (fix DX12 crash)")]
    public static void UseD3D11()
    {
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
            new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12 });
        AssetDatabase.SaveAssets();
        Debug.Log("Windows graphics APIs: " + string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64)));
    }
}
