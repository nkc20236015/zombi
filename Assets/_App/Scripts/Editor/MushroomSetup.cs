using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class MushroomSetup
{
    static MushroomSetup()
    {
        EditorApplication.delayCall += DoSetup;
    }

    static void DoSetup()
    {
        string path = "Assets/Low Poly Mushrooms Pack/Prefabs/Mushrooms/MushroomGroup.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;
        
        bool modified = false;
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        
        if (instance.GetComponent<BoxCollider>() == null)
        {
            var col = instance.AddComponent<BoxCollider>();
            col.size = new Vector3(0.6f, 0.6f, 0.6f);
            col.center = new Vector3(0f, 0.3f, 0f);
            modified = true;
        }
        if (instance.GetComponent<ResourceNode>() == null)
        {
            var node = instance.AddComponent<ResourceNode>();
            var type = typeof(ResourceNode);
            type.GetField("resourceType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(node, ResourceType.Food);
            type.GetField("minYield", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(node, 3);
            type.GetField("maxYield", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(node, 8);
            type.GetField("harvestAmountPerAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(node, 2);
            modified = true;
        }
        int resLayer = LayerMask.NameToLayer("Resource");
        if (instance.layer != resLayer)
        {
            instance.layer = resLayer;
            foreach(Transform child in instance.transform) child.gameObject.layer = resLayer;
            modified = true;
        }

        if (modified)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Debug.Log("[MushroomSetup] MushroomGroup.prefab configured successfully.");
        }
        GameObject.DestroyImmediate(instance);
    }
}
