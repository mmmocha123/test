using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TeddyBearMaterialSetup
{
    private const string Root = "Assets/Art/Teddy_Bear";
    private const string SessionKey = "TeddyBearMaterialSetup.Applied.v1";

    private sealed class MaterialSet
    {
        public readonly string MaterialName;
        public readonly string TexturePrefix;
        public readonly bool HasPbrMaps;

        public MaterialSet(string materialName, string texturePrefix, bool hasPbrMaps = true)
        {
            MaterialName = materialName;
            TexturePrefix = texturePrefix;
            HasPbrMaps = hasPbrMaps;
        }
    }

    private static readonly MaterialSet[] Sets =
    {
        new MaterialSet("Body_Baked", "Body"),
        new MaterialSet("Ear L_Baked", "Ear L", false),
        new MaterialSet("Ear R_Baked", "Ear R", false),
        new MaterialSet("Eyes_Baked", "Eyes"),
        new MaterialSet("Face_Baked", "Face"),
        new MaterialSet("Hands_Baked", "Hands"),
        new MaterialSet("Legs_Baked", "Legs"),
        new MaterialSet("Mouth_Baked", "Mouth")
    };

    static TeddyBearMaterialSetup()
    {
        if (!SessionState.GetBool(SessionKey, false))
            EditorApplication.delayCall += ApplyAutomatically;
    }

    [MenuItem("Tools/Teddy Bear/Apply PBR Textures")]
    public static void Apply()
    {
        try
        {
            foreach (MaterialSet set in Sets)
                Apply(set);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SessionState.SetBool(SessionKey, true);
            Debug.Log("Teddy Bear materials: all texture maps were assigned successfully.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void ApplyAutomatically()
    {
        EditorApplication.delayCall -= ApplyAutomatically;
        Apply();
    }

    private static void Apply(MaterialSet set)
    {
        string materialPath = $"{Root}/Material/{set.MaterialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
            throw new FileNotFoundException($"Material not found: {materialPath}");

        string diffuseName = set.TexturePrefix.StartsWith("Ear ", StringComparison.Ordinal)
            ? set.TexturePrefix.Replace(" ", "_") + "_Bake1_PBR_Diffuse.png"
            : set.TexturePrefix + "_Bake1_PBR_Diffuse.png";
        Texture2D diffuse = LoadTexture($"{Root}/textures/{diffuseName}");
        Texture2D emission = LoadTexture($"{Root}/textures/{set.TexturePrefix}_Bake1_PBR_Emission.png");

        ConfigureTexture(diffuse, false, true);
        ConfigureTexture(emission, false, true);
        material.SetTexture("_BaseMap", diffuse);
        material.SetTexture("_MainTex", diffuse);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
        material.SetTexture("_EmissionMap", emission);
        material.SetColor("_EmissionColor", Color.white);
        material.EnableKeyword("_EMISSION");

        if (set.HasPbrMaps)
        {
            Texture2D normal = LoadTexture($"{Root}/textures/{set.TexturePrefix}_Bake1_PBR_Bump.png");
            Texture2D metalness = LoadTexture($"{Root}/textures/{set.TexturePrefix}_Bake1_PBR_Metalness.png");
            Texture2D roughness = LoadTexture($"{Root}/textures/{set.TexturePrefix}_Bake1_PBR_Roughness.png");

            ConfigureTexture(normal, true, false);
            ConfigureTexture(metalness, false, false, true);
            ConfigureTexture(roughness, false, false, true);

            string packedPath = $"{Root}/textures/{set.TexturePrefix}_MetallicSmoothness.png";
            CreateMetallicSmoothness(metalness, roughness, packedPath);
            Texture2D packed = LoadTexture(packedPath);
            ConfigureTexture(packed, false, false);

            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", packed);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        else
        {
            material.SetTexture("_BumpMap", null);
            material.SetTexture("_MetallicGlossMap", null);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.5f);
            material.DisableKeyword("_NORMALMAP");
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
        }

        EditorUtility.SetDirty(material);
    }

    private static Texture2D LoadTexture(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
            throw new FileNotFoundException($"Texture not found: {path}");
        return texture;
    }

    private static void ConfigureTexture(Texture2D texture, bool normalMap, bool sRgb, bool readable = false)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"Texture importer not found: {path}");

        bool changed = importer.textureType != (normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default)
            || importer.sRGBTexture != sRgb
            || importer.isReadable != readable;
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = sRgb;
        importer.isReadable = readable;
        if (changed)
            importer.SaveAndReimport();
    }

    private static void CreateMetallicSmoothness(Texture2D metalness, Texture2D roughness, string outputPath)
    {
        if (metalness.width != roughness.width || metalness.height != roughness.height)
            throw new InvalidOperationException($"Metalness and roughness sizes differ for {outputPath}.");

        Color32[] metalPixels = metalness.GetPixels32();
        Color32[] roughPixels = roughness.GetPixels32();
        Color32[] packedPixels = new Color32[metalPixels.Length];
        for (int i = 0; i < packedPixels.Length; i++)
        {
            byte metallic = metalPixels[i].r;
            byte smoothness = (byte)(255 - roughPixels[i].r);
            packedPixels[i] = new Color32(metallic, metallic, metallic, smoothness);
        }

        Texture2D packed = new Texture2D(metalness.width, metalness.height, TextureFormat.RGBA32, false, true);
        packed.SetPixels32(packedPixels);
        packed.Apply(false, false);
        File.WriteAllBytes(outputPath, packed.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(packed);
        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
    }
}
