using UnityEngine;
using UnityEditor;
using System.IO;

public class RemoveLODGroupTool
{
    [MenuItem("Tools/Remove LODGroup from Trees")]
    public static void RemoveAllLODGroups()
    {
        string[] searchFolders = new string[] { "Assets" };
        string[] guids = AssetDatabase.FindAssets("t:Prefab", searchFolders);
        
        int removedCount = 0;

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
                    Object.DestroyImmediate(lod, true);
                    EditorUtility.SetDirty(prefab);
                    removedCount++;
                    Debug.Log($"Removed LODGroup from: {prefab.name}");
                }
            }
        }

        if (removedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>LODGroupの削除が完了しました！ 計 {removedCount} 個のプレハブを修正しました。</color>");
        }
        else
        {
            Debug.Log("LODGroupを持つプレハブは見つかりませんでした。");
        }
    }
}
