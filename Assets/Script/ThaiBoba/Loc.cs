using System;
using UnityEngine;

// Thai / English. Text in code is written as Loc.T("ไทย", "English"); fixed UI labels carry a LocalizedText.
// The choice is saved in PlayerPrefs; Thai by default (switch on the title screen or in Settings).
public static class Loc
{
    public enum Lang { TH, EN }
    const string Key = "tb_lang";

    static Lang? current;
    public static event Action Changed;

    public static Lang Current
    {
        get
        {
            if (current == null)
            {
                int saved = PlayerPrefs.GetInt(Key, -1);
                current = saved >= 0 ? (Lang)saved : Lang.TH;
            }
            return current.Value;
        }
        set
        {
            if (current == value) return;
            current = value;
            PlayerPrefs.SetInt(Key, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static bool EN => Current == Lang.EN;

    public static string T(string th, string en) => EN ? en : th;

    public static void Toggle() => Current = EN ? Lang.TH : Lang.EN;

    public static string Label => EN ? "English" : "ไทย";
}
