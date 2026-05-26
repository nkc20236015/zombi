using UnityEngine;

/// <summary>
/// マウスホバー時にオブジェクトをハイライト（アウトライン表示）するコンポーネント。
/// モードに応じて表示を変える:
/// - 伐採モード: 黄色の細いアウトライン
/// - その他: 薄い白のアウトライン（控えめ）
/// </summary>
public class HoverHighlight : MonoBehaviour
{
    [Header("Harvest Mode Highlight")]
    [SerializeField] private Color harvestHighlightColor = new Color(1f, 0.9f, 0.3f, 0.6f);
    [SerializeField] private float harvestOutlineWidth = 0.001f;

    [Header("Normal Mode Highlight")]
    [SerializeField] private Color normalHighlightColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private float normalOutlineWidth = 0.0005f;
    
    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private Material outlineMaterialHarvest;
    private Material outlineMaterialNormal;

    private bool isHovered = false;

    void Start()
    {
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length][];

        Shader outlineShader = Shader.Find("Custom/Outline");
        if (outlineShader != null)
        {
            // 伐採モード用 - 黄色の細いアウトライン
            outlineMaterialHarvest = new Material(outlineShader);
            outlineMaterialHarvest.SetColor("_OutlineColor", harvestHighlightColor);
            outlineMaterialHarvest.SetFloat("_OutlineWidth", harvestOutlineWidth);

            // 通常モード用 - 薄白の控えめなアウトライン
            outlineMaterialNormal = new Material(outlineShader);
            outlineMaterialNormal.SetColor("_OutlineColor", normalHighlightColor);
            outlineMaterialNormal.SetFloat("_OutlineWidth", normalOutlineWidth);
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].sharedMaterials;
        }
    }

    void OnMouseEnter()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Building)
            return;

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

        if (renderers == null) return;

        // モードに応じてマテリアルを選択
        Material outlineMat = null;
        if (GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Gathering)
        {
            outlineMat = outlineMaterialHarvest;
        }
        else
        {
            outlineMat = outlineMaterialNormal;
        }

        if (outlineMat == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] baseMats = originalMaterials[i];
            
            if (enable)
            {
                Material[] newMats = new Material[baseMats.Length + 1];
                for (int j = 0; j < baseMats.Length; j++) newMats[j] = baseMats[j];
                newMats[baseMats.Length] = outlineMat;
                renderers[i].materials = newMats;
            }
            else
            {
                renderers[i].materials = baseMats;
            }
        }
    }

    void OnDisable()
    {
        SetHighlight(false);
    }
}