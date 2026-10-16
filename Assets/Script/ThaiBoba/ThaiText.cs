using System.Collections.Generic;
using TMPro;
using UnityEngine.TextCore;

// Thai needs the OpenType mark positioning features, otherwise a tone mark sitting on an upper vowel
// (ที่, นี้, ชื่อ) is drawn on top of the vowel and disappears. TMP Settings only enables "kern" by default.
public static class ThaiText
{
    static readonly OTL_FeatureTag[] Needed = { OTL_FeatureTag.kern, OTL_FeatureTag.mark, OTL_FeatureTag.mkmk };

    public static void Enable(TMP_Text text)
    {
        if (text == null) return;
        List<OTL_FeatureTag> features = new List<OTL_FeatureTag>(text.fontFeatures);
        bool changed = false;
        foreach (OTL_FeatureTag f in Needed)
            if (!features.Contains(f)) { features.Add(f); changed = true; }
        if (changed) text.fontFeatures = features;
    }
}
