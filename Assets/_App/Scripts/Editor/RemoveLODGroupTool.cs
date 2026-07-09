using UnityEngine;
using UnityEditor;

public class FixLODGroupTool
{
    [MenuItem("Tools/Fix Tree LODGroups (Prevent Popping)")]
    public static void FixAllLODGroups()
    {
        string[] searchFolders = new string[] { "Assets" };
        string[] guids = AssetDatabase.FindAssets("t:Prefab", searchFolders);
        
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            // 特にAnimated Trees Packageの中を対象にする
            if (!path.Contains("Animated Trees Package") && !path.Contains("Trees"))
                continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                LODGroup lod = prefab.GetComponent<LODGroup>();
                if (lod != null)
                {
                    LOD[] lods = lod.GetLODs();
                    if (lods.Length > 0)
                    {
                        // 最初のLOD（一番高品質なLOD0）だけを残し、画面にどれだけ小さく映っても消えないようにする
                        LOD lod0 = lods[0];
                        lod0.screenRelativeTransitionHeight = 0.001f; // ほぼ0%までLOD0を描画

                        lod.SetLODs(new LOD[] { lod0 });
                        EditorUtility.SetDirty(prefab);
                        fixedCount++;
                        Debug.Log($"Fixed LODGroup for: {prefab.name}");
                    }
                }
            }
        }

        if (fixedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>LODGroupの修正が完了しました！ 計 {fixedCount} 個のプレハブを修正し、サイズ変化を防ぎました。</color>");
        }
        else
        {
            Debug.Log("LODGroupを持つプレハブは見つかりませんでした。");
        }
    }
}
