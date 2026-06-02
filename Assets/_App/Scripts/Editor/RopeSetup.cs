using UnityEngine;
using UnityEditor;
using System.IO;

public class RopeSetup : MonoBehaviour {
    [MenuItem("Tools/Setup Rope Material")]
    public static void Setup() {
        string newTexDir = "Assets/_App/Resources/Textures";
        string newTexPath = newTexDir + "/RopeTexture.png";
        string matDir = "Assets/_App/Resources/Materials";
        string matPath = matDir + "/RopeMaterial.mat";

        if (!Directory.Exists(newTexDir)) Directory.CreateDirectory(newTexDir);
        if (!Directory.Exists(matDir)) Directory.CreateDirectory(matDir);
        
        // Move texture if exists
        string oldTexPath = "Assets/_App/Art/Textures/RopeTexture.png";
        if (File.Exists(oldTexPath)) {
            AssetDatabase.MoveAsset(oldTexPath, newTexPath);
        }
        
        AssetDatabase.Refresh();
        
        TextureImporter importer = AssetImporter.GetAtPath(newTexPath) as TextureImporter;
        if (importer != null) {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) {
            mat = new Material(Shader.Find("Unlit/Transparent"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(newTexPath);
        if (tex != null) {
            mat.mainTexture = tex;
            EditorUtility.SetDirty(mat);
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log("Rope setup complete!");
    }
}
