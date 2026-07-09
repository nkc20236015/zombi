using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 木のマテリアルにDither Fadeシェーダーを適用するエディタツール。
/// 元のテクスチャやプロパティはそのまま保持し、シェーダーだけを差し替えます。
/// </summary>
public class ApplyDitherFadeShaderTool
{
    private const string DITHER_SHADER_NAME = "Custom/URP_DitherFade_Lit";

    [MenuItem("Tools/Apply Dither Fade Shader to Tree Materials")]
    public static void ApplyDitherFadeShader()
    {
        Shader ditherShader = Shader.Find(DITHER_SHADER_NAME);
        if (ditherShader == null)
        {
            Debug.LogError($"[DitherFade] シェーダー '{DITHER_SHADER_NAME}' が見つかりません。Assets/_App/Shaders/ にシェーダーファイルがあるか確認してください。");
            return;
        }

        // 木のマテリアルがあるフォルダを指定
        string[] materialFolders = new string[]
        {
            "Assets/Animated Trees Package/Materials",
        };

        int appliedCount = 0;
        List<string> appliedMaterials = new List<string>();

        foreach (string folder in materialFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;

            string[] guids = AssetDatabase.FindAssets("t:Material", new string[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Billboardsフォルダは対象外
                if (path.Contains("Billboards")) continue;

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                // 既にDitherシェーダーが適用されていたらスキップ
                if (mat.shader == ditherShader) continue;

                // 元のプロパティを保持しつつシェーダーだけ差し替え
                Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
                float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;
                bool isAlphaClip = mat.IsKeywordEnabled("_ALPHATEST_ON");
                bool hasNormalMap = mat.IsKeywordEnabled("_NORMALMAP");

                // シェーダーを差し替え
                mat.shader = ditherShader;

                // プロパティを再設定
                if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
                mat.SetColor("_BaseColor", baseColor);
                if (bumpMap != null) mat.SetTexture("_BumpMap", bumpMap);
                mat.SetFloat("_BumpScale", bumpScale);
                mat.SetFloat("_Cutoff", cutoff);

                // キーワードの復元
                if (isAlphaClip)
                    mat.EnableKeyword("_ALPHATEST_ON");
                if (hasNormalMap)
                    mat.EnableKeyword("_NORMALMAP");

                // 葉のマテリアルは両面描画
                if (path.Contains("leaves"))
                {
                    mat.SetFloat("_Cull", 0); // Off (両面)
                }

                // Dither Fade のデフォルト距離設定
                mat.SetFloat("_DitherFadeStart", 8.0f);
                mat.SetFloat("_DitherFadeEnd", 3.0f);

                EditorUtility.SetDirty(mat);
                appliedCount++;
                appliedMaterials.Add(mat.name);
            }
        }

        if (appliedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[DitherFade] シェーダー適用完了！ {appliedCount} 個のマテリアルに適用しました:</color>");
            foreach (string name in appliedMaterials)
            {
                Debug.Log($"  • {name}");
            }
        }
        else
        {
            Debug.Log("[DitherFade] 適用対象のマテリアルが見つかりませんでした。");
        }
    }

    [MenuItem("Tools/Revert Tree Materials to URP Lit")]
    public static void RevertToURPLit()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("[DitherFade] URP Litシェーダーが見つかりません。");
            return;
        }

        Shader ditherShader = Shader.Find(DITHER_SHADER_NAME);

        string[] materialFolders = new string[]
        {
            "Assets/Animated Trees Package/Materials",
        };

        int revertedCount = 0;

        foreach (string folder in materialFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;

            string[] guids = AssetDatabase.FindAssets("t:Material", new string[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Billboards")) continue;

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader != ditherShader) continue;

                mat.shader = urpLit;
                EditorUtility.SetDirty(mat);
                revertedCount++;
            }
        }

        if (revertedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=yellow>[DitherFade] {revertedCount} 個のマテリアルを URP Lit に戻しました。</color>");
        }
        else
        {
            Debug.Log("[DitherFade] 戻す対象のマテリアルが見つかりませんでした。");
        }
    }
}
