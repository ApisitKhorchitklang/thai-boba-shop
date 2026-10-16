using System;
using UnityEngine;

// Player options, stored in PlayerPrefs: volumes, controls (touch / PC), mouse sensitivity, graphics, fullscreen.
public static class GameSettings
{
    public enum ControlMode { Auto, Touch, PC }

    public static event Action Changed;

    public static float Master { get => PlayerPrefs.GetFloat("tb_master", 1f); set { PlayerPrefs.SetFloat("tb_master", value); Apply(); } }
    public static float Music { get => PlayerPrefs.GetFloat("tb_music", 0.6f); set { PlayerPrefs.SetFloat("tb_music", value); Apply(); } }
    public static float Sfx { get => PlayerPrefs.GetFloat("tb_sfx", 0.9f); set { PlayerPrefs.SetFloat("tb_sfx", value); Apply(); } }
    public static float MouseSensitivity { get => PlayerPrefs.GetFloat("tb_mouse", 1f); set { PlayerPrefs.SetFloat("tb_mouse", value); Apply(); } }
    public static ControlMode Controls { get => (ControlMode)PlayerPrefs.GetInt("tb_controls", 0); set { PlayerPrefs.SetInt("tb_controls", (int)value); Apply(); } }
    // 0 = low (no post-processing, no shadows), 1 = medium, 2 = high
    public static int Quality { get => PlayerPrefs.GetInt("tb_quality", Application.isMobilePlatform ? 1 : 2); set { PlayerPrefs.SetInt("tb_quality", Mathf.Clamp(value, 0, 2)); Apply(); } }
    public static bool Fullscreen { get => PlayerPrefs.GetInt("tb_fullscreen", 1) == 1; set { PlayerPrefs.SetInt("tb_fullscreen", value ? 1 : 0); Apply(); } }

    // What the game actually uses after resolving "Auto"
    public static bool UsePCControls
    {
        get
        {
            switch (Controls)
            {
                case ControlMode.PC: return true;
                case ControlMode.Touch: return false;
                default: return !Application.isMobilePlatform;      // touch-screen laptops still play as PC; Device Simulator counts as mobile
            }
        }
    }

    public static string ControlsLabel
    {
        get
        {
            switch (Controls)
            {
                case ControlMode.PC: return Loc.T("PC (เมาส์ + คีย์บอร์ด)", "PC (mouse + keyboard)");
                case ControlMode.Touch: return Loc.T("จอสัมผัส", "Touch screen");
                default: return UsePCControls ? Loc.T("อัตโนมัติ (PC)", "Auto (PC)") : Loc.T("อัตโนมัติ (จอสัมผัส)", "Auto (touch)");
            }
        }
    }

    public static string QualityLabel => Quality == 0 ? Loc.T("ต่ำ", "Low") : Quality == 1 ? Loc.T("กลาง", "Medium") : Loc.T("สูง", "High");

    public static void Apply()
    {
        PlayerPrefs.Save();
        AudioListener.volume = Master;
        if (!Application.isMobilePlatform && !Application.isEditor && Screen.fullScreen != Fullscreen)
            Screen.fullScreen = Fullscreen;
        Changed?.Invoke();
    }
}
