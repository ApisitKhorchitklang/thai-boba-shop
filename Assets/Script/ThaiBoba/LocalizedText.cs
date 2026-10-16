using TMPro;
using UnityEngine;

// Fixed UI label in two languages (set up by the scene builder); switches when Loc.Current changes.
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [TextArea] public string th;
    [TextArea] public string en;

    TMP_Text text;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        Apply();
    }

    void OnEnable()
    {
        Loc.Changed += Apply;
        Apply();
    }

    void OnDisable()
    {
        Loc.Changed -= Apply;
    }

    public void Apply()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        string s = Loc.T(th, string.IsNullOrEmpty(en) ? th : en);
        if (text.text != s) text.text = s;
    }
}
