using UnityEngine;

/// <summary>
/// マウスホバー時にオブジェクトをハイライトするコンポーネント。
/// アウトラインではなく、マテリアルの色を薄く変えることで控えめに選択表示する。
/// - 通常モード: 薄い白に変化（控えめ）
/// - 伐採モード: 薄い黄色に変化
/// </summary>
public class HoverHighlight : MonoBehaviour
{
    [Header("Normal Mode")]
    [SerializeField] private Color normalTintColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private float normalTintStrength = 0.35f;

    [Header("Gathering Mode")]
    [SerializeField] private Color gatheringTintColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private float gatheringTintStrength = 0.45f;

    [Header("Cancel Mode")]
    [SerializeField] private Color cancelTintColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float cancelTintStrength = 0.45f;

    private Renderer[] renderers;
    private Material[][] cachedMaterials;
    private Color[][] originalColors;
    private bool isHovered = false;

    private PlayerMode lastMode;

    void Start()
    {
        CacheRenderers();
    }

    void Update()
    {
        if (isHovered && GameManager.Instance != null)
        {
            if (lastMode != GameManager.Instance.CurrentPlayerMode)
            {
                // Force update color if mode changed while hovering
                SetHighlight(true, true);
            }
        }
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>();
        cachedMaterials = new Material[renderers.Length][];
        originalColors = new Color[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            // 最初に一度だけマテリアルをインスタンス化してキャッシュする
            cachedMaterials[i] = renderers[i].materials;
            originalColors[i] = new Color[cachedMaterials[i].Length];
            
            for (int j = 0; j < cachedMaterials[i].Length; j++)
            {
                if (cachedMaterials[i][j] != null)
                {
                    if (cachedMaterials[i][j].HasProperty("_BaseColor"))
                        originalColors[i][j] = cachedMaterials[i][j].GetColor("_BaseColor");
                    else if (cachedMaterials[i][j].HasProperty("_Color"))
                        originalColors[i][j] = cachedMaterials[i][j].GetColor("_Color");
                    else
                        originalColors[i][j] = Color.white;
                }
            }
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

    private void SetHighlight(bool enable, bool forceUpdate = false)
    {
        if (!forceUpdate && isHovered == enable) return;
        isHovered = enable;
        if (renderers == null || cachedMaterials == null) return;

        // モードに応じた色と強さを決定
        PlayerMode currentMode = PlayerMode.Normal;
        if (GameManager.Instance != null)
        {
            currentMode = GameManager.Instance.CurrentPlayerMode;
            lastMode = currentMode;
        }

        Color tint = normalTintColor;
        float strength = normalTintStrength;

        if (currentMode == PlayerMode.Gathering)
        {
            tint = gatheringTintColor;
            strength = gatheringTintStrength;
        }
        else if (currentMode == PlayerMode.Cancelling)
        {
            tint = cancelTintColor;
            strength = cancelTintStrength;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            for (int j = 0; j < cachedMaterials[i].Length; j++)
            {
                if (cachedMaterials[i][j] == null) continue;

                Color target;
                if (enable)
                {
                    target = Color.Lerp(originalColors[i][j], tint, strength);
                }
                else
                {
                    target = originalColors[i][j];
                }

                if (cachedMaterials[i][j].HasProperty("_BaseColor"))
                    cachedMaterials[i][j].SetColor("_BaseColor", target);
                else if (cachedMaterials[i][j].HasProperty("_Color"))
                    cachedMaterials[i][j].SetColor("_Color", target);
            }
        }
    }

    void OnDisable()
    {
        SetHighlight(false);
    }
}