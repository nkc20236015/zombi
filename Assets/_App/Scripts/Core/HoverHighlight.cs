using UnityEngine;

/// <summary>
/// マウスホバー時にオブジェクトをハイライト（アウトライン表示など）するコンポーネント。
/// Collider がアタッチされているオブジェクトで動作します。
/// </summary>
public class HoverHighlight : MonoBehaviour
{
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.5f, 0.5f);
    [SerializeField] private float outlineWidth = 0.0015f;
    
    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private Material outlineMaterial;

    private bool isHovered = false;

    void Start()
    {
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length][];

        // カスタムのアウトラインシェーダーを取得
        Shader outlineShader = Shader.Find("Custom/Outline");
        if (outlineShader != null)
        {
            outlineMaterial = new Material(outlineShader);
            outlineMaterial.SetColor("_OutlineColor", highlightColor);
            outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].sharedMaterials;
        }
    }

    void OnMouseEnter()
    {
        // 建築モードなどの特別なモードではハイライトしないなどの制御が必要であれば追加可能
        if (GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Building)
            return;

        // UIの上をクリック・ホバーしている場合は無視
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        SetHighlight(true);
    }

    void OnMouseExit()
    {
        SetHighlight(false);
    }

    private void SetHighlight(bool enable)
    {
        if (isHovered == enable) return;
        isHovered = enable;

        if (renderers == null || outlineMaterial == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] baseMats = originalMaterials[i];
            
            if (enable)
            {
                // オリジナルマテリアル + アウトラインマテリアルを結合
                Material[] newMats = new Material[baseMats.Length + 1];
                for (int j = 0; j < baseMats.Length; j++) newMats[j] = baseMats[j];
                newMats[baseMats.Length] = outlineMaterial;
                renderers[i].materials = newMats;
            }
            else
            {
                // 元に戻す
                renderers[i].materials = baseMats;
            }
        }
    }

    void OnDisable()
    {
        SetHighlight(false);
    }
}