using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Makes the see-through materials of the Blender models (glass, plastic cup, ice, seal film, fan blades)
// transparent on URP Lit when Assets/Models/ThaiBobaShop/*.fbx is imported.
public class ThaiBobaModelPostprocessor : AssetPostprocessor
{
    static readonly Dictionary<string, float> TransparentAlpha = new Dictionary<string, float>
    {
        { "M_glass", 0.3f },
        { "M_plastic_clear", 0.28f },
        { "M_ice", 0.55f },
        { "M_seal_film", 0.6f },
        { "M_fan_blue", 0.8f },
    };

    const string CharacterDir = "Assets/Models/ThaiBobaShop/Characters/";

    bool IsCharacter => assetPath.Replace('\\', '/').StartsWith(CharacterDir);

    public override uint GetVersion()
    {
        return 2;
    }

    // Rigged NPCs: Generic rig, looping clips named "Walk" / "Idle"
    void OnPreprocessModel()
    {
        if (!IsCharacter) return;
        ModelImporter mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = true;
    }

    void OnPreprocessAnimation()
    {
        if (!IsCharacter) return;
        ModelImporter mi = (ModelImporter)assetImporter;
        ModelImporterClipAnimation[] clips = mi.defaultClipAnimations;
        foreach (ModelImporterClipAnimation c in clips)
        {
            int bar = c.takeName.LastIndexOf('|');
            c.name = bar >= 0 ? c.takeName.Substring(bar + 1) : c.takeName;
            c.loopTime = true;
        }
        mi.clipAnimations = clips;
    }

    void OnPostprocessMaterial(Material material)
    {
        if (!assetPath.Replace('\\', '/').StartsWith("Assets/Models/ThaiBobaShop/")) return;
        if (IsCharacter)
        {
            // PS2 look: matte baked texture, no specular sheen
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            return;
        }
        if (!TransparentAlpha.TryGetValue(material.name, out float alpha)) return;
        if (!material.HasProperty("_Surface")) return;

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;

        if (material.HasProperty("_BaseColor"))
        {
            Color c = material.GetColor("_BaseColor");
            c.a = alpha;
            material.SetColor("_BaseColor", c);
        }
    }
}
