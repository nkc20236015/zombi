using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// カメラと選択中のNPC（または注視点）の間にあるオブジェクトを半透明にするコンポーネント。
/// メインカメラにアタッチして使用する。
/// </summary>
public class CameraOccluder : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float transparentAlpha = 0.25f;     // 半透明時の透過度
    [SerializeField] private float fadeSpeed = 8f;               // フェード速度
    [SerializeField] private LayerMask occludableLayers = ~0;    // 半透明化の対象レイヤー
    [SerializeField] private string[] ignoreTags = { "NPC" };    // 無視するタグ

    [Header("Camera Proximity (カメラ自体が近づいた時)")]
    [SerializeField] private float proximityRadius = 5f;         // カメラ周辺の検出範囲
    [SerializeField] private LayerMask proximityLayers = ~0;     // 近接検出の対象レイヤー（Resourceレイヤーなど）

    // 各Rendererの元のマテリアル状態を保持する構造体
    private class OccludedInfo
    {
        public Renderer renderer;
        public Material[] originalMaterials;     // 元のマテリアルの参照
        public Color[] originalColors;           // 元のBaseColor
        public float[] originalSurface;          // 元の_Surface値
        public int[] originalRenderQueue;        // 元のRenderQueue
        public float currentAlpha;               // 現在のアルファ値 (1.0 = 不透明)
        public bool isOccluding;                 // 今フレームで遮蔽しているか
    }

    private Dictionary<Renderer, OccludedInfo> occludedObjects = new Dictionary<Renderer, OccludedInfo>();
    private List<Renderer> toRemove = new List<Renderer>();

    void LateUpdate()
    {
        // 全ての遮蔽フラグをリセット
        foreach (var kvp in occludedObjects)
        {
            kvp.Value.isOccluding = false;
        }

        // 選択中のNPCがいればそこへRayを飛ばす
        if (SelectionManager.Instance != null && SelectionManager.Instance.SelectedNPCs.Count > 0)
        {
            foreach (var npc in SelectionManager.Instance.SelectedNPCs)
            {
                if (npc != null)
                {
                    CheckOcclusion(npc.transform.position + Vector3.up * 1.0f);
                }
            }
        }

        // カメラ自体が木などに近づいた場合も半透明化
        CheckCameraProximity();

        // アルファ値を更新
        UpdateOccludedObjects();
    }

    private void CheckOcclusion(Vector3 targetPosition)
    {
        Vector3 cameraPos = transform.position;
        Vector3 direction = targetPosition - cameraPos;
        float distance = direction.magnitude;

        // カメラからNPCに向けてRayを飛ばし、途中の障害物を取得
        RaycastHit[] hits = Physics.RaycastAll(cameraPos, direction.normalized, distance, occludableLayers);

        foreach (var hit in hits)
        {
            // NPC自身は無視
            if (ShouldIgnore(hit.collider.gameObject)) continue;

            // 対象のRendererを取得
            Renderer[] renderers = hit.collider.GetComponentsInChildren<Renderer>();
            // 親にもRendererがある場合（MeshRendererが親にある木など）
            Renderer parentRenderer = hit.collider.GetComponentInParent<Renderer>();
            
            // まずはヒットしたオブジェクト自体のRendererを処理
            foreach (var rend in renderers)
            {
                MarkAsOccluding(rend);
            }
            
            // 親のRendererも処理（木の幹と葉が別オブジェクトの場合）
            if (parentRenderer != null)
            {
                MarkAsOccluding(parentRenderer);
                // 親の兄弟のRendererも取得（同じ木の他のパーツ）
                Renderer[] siblingRenderers = parentRenderer.GetComponentsInChildren<Renderer>();
                foreach (var rend in siblingRenderers)
                {
                    MarkAsOccluding(rend);
                }
            }
        }
    }

    private bool ShouldIgnore(GameObject obj)
    {
        foreach (string tag in ignoreTags)
        {
            if (obj.CompareTag(tag)) return true;
        }
        // NPCController がついていたら無視
        if (obj.GetComponentInParent<NPCController>() != null) return true;
        return false;
    }

    /// <summary>
    /// カメラの近くにあるオブジェクト（木など）を検出して半透明にする。
    /// BoxColliderは不要で、OverlapSphereで周囲を検出する。
    /// </summary>
    private void CheckCameraProximity()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, proximityRadius, proximityLayers);

        foreach (var col in nearby)
        {
            if (ShouldIgnore(col.gameObject)) continue;

            // 木のルートオブジェクトのRendererをまとめて取得
            Transform root = col.transform.root;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

            foreach (var rend in renderers)
            {
                MarkAsOccluding(rend);
            }
        }
    }

    private void MarkAsOccluding(Renderer rend)
    {
        if (rend == null) return;

        if (!occludedObjects.ContainsKey(rend))
        {
            // 新規登録
            var info = new OccludedInfo
            {
                renderer = rend,
                originalMaterials = rend.sharedMaterials,
                currentAlpha = 1f,
                isOccluding = true
            };

            // 各マテリアルの元の情報を保存
            Material[] mats = rend.materials; // インスタンス化されたマテリアル
            info.originalColors = new Color[mats.Length];
            info.originalSurface = new float[mats.Length];
            info.originalRenderQueue = new int[mats.Length];

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].HasProperty("_BaseColor"))
                    info.originalColors[i] = mats[i].GetColor("_BaseColor");
                else if (mats[i].HasProperty("_Color"))
                    info.originalColors[i] = mats[i].GetColor("_Color");
                else
                    info.originalColors[i] = Color.white;

                info.originalSurface[i] = mats[i].HasProperty("_Surface") ? mats[i].GetFloat("_Surface") : 0f;
                info.originalRenderQueue[i] = mats[i].renderQueue;
            }

            occludedObjects[rend] = info;
        }
        else
        {
            occludedObjects[rend].isOccluding = true;
        }
    }

    private void UpdateOccludedObjects()
    {
        toRemove.Clear();

        foreach (var kvp in occludedObjects)
        {
            var info = kvp.Value;
            if (info.renderer == null)
            {
                toRemove.Add(kvp.Key);
                continue;
            }

            float targetAlpha = info.isOccluding ? transparentAlpha : 1f;
            info.currentAlpha = Mathf.MoveTowards(info.currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

            Material[] mats = info.renderer.materials;
            int count = Mathf.Min(mats.Length, info.originalColors.Length);
            for (int i = 0; i < count; i++)
            {
                if (info.currentAlpha < 0.99f)
                {
                    // 半透明に設定
                    SetMaterialTransparent(mats[i]);
                    Color col = info.originalColors[i];
                    col.a = info.currentAlpha;

                    if (mats[i].HasProperty("_BaseColor"))
                        mats[i].SetColor("_BaseColor", col);
                    else if (mats[i].HasProperty("_Color"))
                        mats[i].SetColor("_Color", col);
                }
                else
                {
                    // 完全に不透明に戻す
                    RestoreMaterialOpaque(mats[i], info.originalSurface[i], info.originalRenderQueue[i]);
                    Color col = info.originalColors[i];
                    col.a = 1f;

                    if (mats[i].HasProperty("_BaseColor"))
                        mats[i].SetColor("_BaseColor", col);
                    else if (mats[i].HasProperty("_Color"))
                        mats[i].SetColor("_Color", col);
                }
            }
            info.renderer.materials = mats;

            // 完全に不透明に戻ったら辞書から削除
            if (!info.isOccluding && info.currentAlpha >= 0.99f)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var key in toRemove)
        {
            occludedObjects.Remove(key);
        }
    }

    /// <summary>
    /// URP Litマテリアルを半透明モードに設定する。
    /// </summary>
    private void SetMaterialTransparent(Material mat)
    {
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // 1 = Transparent
            mat.SetFloat("_Blend", 0f);   // 0 = Alpha
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
    }

    /// <summary>
    /// URP Litマテリアルを不透明モードに戻す。
    /// </summary>
    private void RestoreMaterialOpaque(Material mat, float originalSurface, int originalRenderQueue)
    {
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", originalSurface);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)BlendMode.One);
            mat.SetInt("_DstBlend", (int)BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = originalRenderQueue;
        }
    }
}
