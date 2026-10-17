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
        { "M_konjac", 0.78f },
        { "M_jelly_green", 0.78f },
    };

    const string CharacterDir = "Assets/Models/ThaiBobaShop/Characters/";

    bool IsCharacter => assetPath.Replace('\\', '/').StartsWith(CharacterDir);

    // vehicle finishes (smoothness, metallic): glossy paint, dark glass, chrome, rubber
    static readonly Dictionary<string, Vector2> Finish = new Dictionary<string, Vector2>
    {
        { "M_car_glass", new Vector2(0.95f, 0.35f) }, { "M_mirror_glass", new Vector2(0.97f, 1f) },
        { "M_chrome", new Vector2(0.9f, 1f) }, { "M_rim", new Vector2(0.82f, 1f) }, { "M_rim_dark", new Vector2(0.6f, 0.8f) },
        { "M_tire", new Vector2(0.12f, 0f) }, { "M_underbody", new Vector2(0.05f, 0f) }, { "M_bed_liner", new Vector2(0.1f, 0f) },
        { "M_trim_black", new Vector2(0.35f, 0f) }, { "M_plastic_grey", new Vector2(0.25f, 0f) }, { "M_seat_vinyl", new Vector2(0.45f, 0f) },
        { "M_lamp_lens", new Vector2(0.92f, 0.2f) }, { "M_lamp_tail", new Vector2(0.88f, 0f) }, { "M_lamp_amber", new Vector2(0.88f, 0f) },
        { "M_engine", new Vector2(0.45f, 0.6f) },
    };

    bool IsSeated => IsCharacter && System.IO.Path.GetFileName(assetPath).StartsWith("SEAT_");

    public override uint GetVersion()
    {
        return 4;
    }

    // Rigged NPCs: Generic rig, looping clips named "Walk" / "Idle"
    void OnPreprocessModel()
    {
        if (!IsCharacter) return;
        ModelImporter mi = (ModelImporter)assetImporter;
        if (IsSeated)
        {
            // seated drivers / passengers: posed static meshes, no rig
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            return;
        }
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = true;
    }

    void OnPreprocessAnimation()
    {
        if (!IsCharacter || IsSeated) return;
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
            // baked albedo (cloth, skin, hair): matte, no specular sheen
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            return;
        }
        Vector2 fin;
        if (material.name.StartsWith("M_paint_")) fin = new Vector2(0.8f, 0.25f);          // clear-coated car paint
        else if (!Finish.TryGetValue(material.name, out fin)) fin = new Vector2(-1f, -1f);
        if (fin.x >= 0f)
        {
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", fin.x);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", fin.y);
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
