using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using System.Reflection;

public class SetupXRayFeature
{
    [MenuItem("Tools/Setup NPC X-Ray")]
    public static void Setup()
    {
        // 1. Ensure Layer 'NPC' exists
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        bool hasLayer = false;
        int targetLayer = -1;
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layerSP = layers.GetArrayElementAtIndex(i);
            if (layerSP.stringValue == "NPC")
            {
                hasLayer = true;
                targetLayer = i;
                break;
            }
        }
        if (!hasLayer)
        {
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layerSP = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerSP.stringValue))
                {
                    layerSP.stringValue = "NPC";
                    targetLayer = i;
                    break;
                }
            }
            tagManager.ApplyModifiedProperties();
        }

        // 2. Set Layer on NPC prefabs
        string[] prefabs = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab.GetComponentInChildren<NPCController>() != null || prefab.name.Contains("NPC") || prefab.name.Contains("Villager") || prefab.name.Contains("Survivor"))
            {
                SetLayerRecursively(prefab, targetLayer);
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }

        // 3. Create Shaders and Materials
        if (!System.IO.Directory.Exists("Assets/_App/Materials"))
            System.IO.Directory.CreateDirectory("Assets/_App/Materials");

        // Stencil Writer Shader
        string shaderPath = "Assets/_App/Materials/XRayStencilWriter.shader";
        if (!System.IO.File.Exists(shaderPath))
        {
            string shaderCode = @"
Shader ""Hidden/XRayStencilWriter"" {
    SubShader {
        Tags { ""RenderType""=""Opaque"" ""RenderPipeline""=""UniversalPipeline"" }
        Pass {
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); return o; }
            fixed4 frag(v2f i) : SV_Target { return fixed4(0,0,0,0); }
            ENDCG
        }
    }
}
";
            System.IO.File.WriteAllText(shaderPath, shaderCode);
            AssetDatabase.ImportAsset(shaderPath);
        }

        // Stencil Writer Material
        string maskMatPath = "Assets/_App/Materials/XRay_StencilWriter.mat";
        Material maskMat = AssetDatabase.LoadAssetAtPath<Material>(maskMatPath);
        if (maskMat == null)
        {
            maskMat = new Material(Shader.Find("Hidden/XRayStencilWriter"));
            AssetDatabase.CreateAsset(maskMat, maskMatPath);
        }

        // X-Ray Material
        string xrayMatPath = "Assets/_App/Materials/XRay_NPC.mat";
        Material xrayMat = AssetDatabase.LoadAssetAtPath<Material>(xrayMatPath);
        if (xrayMat == null)
        {
            xrayMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            xrayMat.SetColor("_BaseColor", new Color(0.2f, 0.6f, 1.0f, 0.4f)); 
            xrayMat.SetFloat("_Surface", 1); 
            xrayMat.SetFloat("_Blend", 0); 
            xrayMat.SetFloat("_ZWrite", 0); 
            xrayMat.renderQueue = 3000;
            AssetDatabase.CreateAsset(xrayMat, xrayMatPath);
        }

        // 4. Add Render Features to URP Renderer
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (rp != null)
        {
            FieldInfo rendererDataListField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (rendererDataListField != null)
            {
                var dataArray = (ScriptableRendererData[])rendererDataListField.GetValue(rp);
                foreach (var rendererData in dataArray)
                {
                    if (rendererData != null)
                    {
                        AddRenderFeatures(rendererData, maskMat, xrayMat, targetLayer);
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("X-Ray Setup with Stencil Complete!");
    }

    private static void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (null == obj) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (null == child) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    private static void AddRenderFeatures(ScriptableRendererData rendererData, Material maskMat, Material xrayMat, int layerMask)
    {
        // Remove old features if exist
        rendererData.rendererFeatures.RemoveAll(f => f.name == "NPC X-Ray" || f.name == "NPC Stencil Mask");

        // 1. Stencil Mask Pass (Visible parts write Stencil=1)
        RenderObjects stencilFeature = ScriptableObject.CreateInstance<RenderObjects>();
        stencilFeature.name = "NPC Stencil Mask";
        var settings1 = stencilFeature.settings;
        settings1.Event = RenderPassEvent.AfterRenderingOpaques;
        settings1.filterSettings.LayerMask = 1 << layerMask;
        settings1.filterSettings.RenderQueueType = RenderQueueType.Opaque;
        settings1.overrideMaterial = maskMat;
        settings1.overrideMaterialPassIndex = 0;
        settings1.overrideDepthState = true;
        settings1.depthCompareFunction = UnityEngine.Rendering.CompareFunction.LessEqual;
        
        settings1.stencilSettings.overrideStencilState = true;
        settings1.stencilSettings.stencilReference = 1;
        settings1.stencilSettings.stencilCompareFunction = UnityEngine.Rendering.CompareFunction.Always;
        settings1.stencilSettings.passOperation = UnityEngine.Rendering.StencilOp.Replace;
        stencilFeature.settings = settings1;

        // 2. X-Ray Pass (Occluded parts render IF Stencil != 1)
        RenderObjects xrayFeature = ScriptableObject.CreateInstance<RenderObjects>();
        xrayFeature.name = "NPC X-Ray";
        var settings2 = xrayFeature.settings;
        settings2.Event = RenderPassEvent.AfterRenderingOpaques;
        settings2.filterSettings.LayerMask = 1 << layerMask;
        settings2.filterSettings.RenderQueueType = RenderQueueType.Opaque;
        settings2.overrideMaterial = xrayMat;
        settings2.overrideMaterialPassIndex = 0;
        settings2.overrideDepthState = true;
        settings2.depthCompareFunction = UnityEngine.Rendering.CompareFunction.Greater;
        
        settings2.stencilSettings.overrideStencilState = true;
        settings2.stencilSettings.stencilReference = 1;
        settings2.stencilSettings.stencilCompareFunction = UnityEngine.Rendering.CompareFunction.NotEqual;
        xrayFeature.settings = settings2;

        AssetDatabase.AddObjectToAsset(stencilFeature, rendererData);
        AssetDatabase.AddObjectToAsset(xrayFeature, rendererData);
        rendererData.rendererFeatures.Add(stencilFeature);
        rendererData.rendererFeatures.Add(xrayFeature);
        rendererData.SetDirty();
        Debug.Log("Added NPC Stencil and X-Ray features to " + rendererData.name);
    }
}
