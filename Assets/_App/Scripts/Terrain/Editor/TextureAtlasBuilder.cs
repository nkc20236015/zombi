using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Cartoon_Texture_Packのテクスチャを1枚のアトラスに結合するエディタツール。
/// メニュー: Tools > Terrain > テクスチャアトラス生成
/// </summary>
public class TextureAtlasBuilder : EditorWindow
{
    // テクスチャパス（Cartoon_Texture_Pack内のBasecolorテクスチャ）
    private static readonly string[] sourceTexturePaths = new string[]
    {
        "Assets/Cartoon_Texture_Pack/GRASS/GRASS_Dense/GRASS_Dense_Tint_01/Textures/Grass_Dense_Tint_01_Base_Basecolor_A.png",
        "Assets/Cartoon_Texture_Pack/DIRT/Dirt_Path/Textures/Dirt_Path_Basecolor.png",
        "Assets/Cartoon_Texture_Pack/ROCKS/ROCKS_Cliff/Textures/Rocks_Cliff_A_Basecolor_A.png",
        "Assets/Cartoon_Texture_Pack/SAND/SAND_Beach/Textures/Sand_Beach_Base_Basecolor.png",
        "", // Water: 単色で生成
    };

    private static readonly string[] tileNames = new string[]
    {
        "GrassTop", "Dirt", "Stone", "Sand", "Water"
    };

    private static readonly Color waterColor = new Color(0.30f, 0.60f, 0.85f);

    private const int TileSize = 256;
    private const string OutputAtlasPath = "Assets/Textures/TerrainAtlas.png";
    private const string OutputMaterialPath = "Assets/Materials/TerrainAtlas.mat";

    [MenuItem("Tools/Terrain/テクスチャアトラス生成")]
    public static void BuildAtlas()
    {
        int tileCount = sourceTexturePaths.Length;
        int atlasWidth = TileSize * tileCount;
        int atlasHeight = TileSize;

        Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, true);
        atlas.filterMode = FilterMode.Point;
        atlas.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < tileCount; i++)
        {
            Color[] tilePixels;

            if (string.IsNullOrEmpty(sourceTexturePaths[i]))
            {
                // 単色タイル（Water）
                tilePixels = new Color[TileSize * TileSize];
                for (int p = 0; p < tilePixels.Length; p++)
                    tilePixels[p] = waterColor;
                Debug.Log($"[AtlasBuilder] {tileNames[i]}: 単色(水色)で生成");
            }
            else
            {
                // テクスチャを読み込んでリサイズ
                tilePixels = LoadAndResizeTexture(sourceTexturePaths[i], TileSize, TileSize);
                if (tilePixels == null)
                {
                    Debug.LogError($"[AtlasBuilder] {tileNames[i]}: テクスチャ読み込み失敗 ({sourceTexturePaths[i]})");
                    // フォールバック: 灰色
                    tilePixels = new Color[TileSize * TileSize];
                    for (int p = 0; p < tilePixels.Length; p++)
                        tilePixels[p] = Color.gray;
                }
                else
                {
                    Debug.Log($"[AtlasBuilder] {tileNames[i]}: 読み込み成功 ({sourceTexturePaths[i]})");
                }
            }

            atlas.SetPixels(i * TileSize, 0, TileSize, TileSize, tilePixels);
        }

        atlas.Apply();

        // PNGとして保存
        string directory = Path.GetDirectoryName(OutputAtlasPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        byte[] pngData = atlas.EncodeToPNG();
        File.WriteAllBytes(OutputAtlasPath, pngData);
        DestroyImmediate(atlas);

        AssetDatabase.Refresh();

        // テクスチャのインポート設定
        TextureImporter importer = AssetImporter.GetAtPath(OutputAtlasPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        // マテリアル作成
        CreateTerrainMaterial();

        Debug.Log($"[AtlasBuilder] アトラス生成完了: {OutputAtlasPath}");
        EditorUtility.DisplayDialog("テクスチャアトラス生成", "アトラスとマテリアルの生成が完了しました。\nVoxelWorldのTerrainMaterialにセットしてください。", "OK");
    }

    private static Color[] LoadAndResizeTexture(string assetPath, int targetWidth, int targetHeight)
    {
        // テクスチャを読み込み可能に設定
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return null;

        bool wasReadable = importer.isReadable;
        if (!wasReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (source == null) return null;

        // RenderTextureでリサイズ
        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D resized = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        resized.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        resized.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color[] pixels = resized.GetPixels();
        DestroyImmediate(resized);

        // 元の読み込み設定に戻す
        if (!wasReadable)
        {
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        return pixels;
    }

    private static void CreateTerrainMaterial()
    {
        string matDir = Path.GetDirectoryName(OutputMaterialPath);
        if (!Directory.Exists(matDir))
            Directory.CreateDirectory(matDir);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = "TerrainAtlas";

        Texture2D atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputAtlasPath);
        if (atlasTexture != null)
        {
            mat.mainTexture = atlasTexture;
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", atlasTexture);
        }

        AssetDatabase.CreateAsset(mat, OutputMaterialPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AtlasBuilder] マテリアル生成完了: {OutputMaterialPath}");
    }
}
