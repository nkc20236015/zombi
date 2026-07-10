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
    private Material[][] originalSharedMats; // 復元用のオリジナルsharedMaterials
    private Color[][] originalColors;
    private bool isHovered = false;
    private bool isCached = false;

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
        // アクティブなRendererのみ取得（LOD1+は非アクティブ化済み）
        renderers = GetComponentsInChildren<Renderer>(false);
        originalSharedMats = new Material[renderers.Length][];
        originalColors = new Color[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            // sharedMaterialsを保存（復元用・色の読み取り用）
            originalSharedMats[i] = renderers[i].sharedMaterials;
            originalColors[i] = new Color[originalSharedMats[i].Length];

            for (int j = 0; j < originalSharedMats[i].Length; j++)
            {
                if (originalSharedMats[i][j] != null)
                {
                    if (originalSharedMats[i][j].HasProperty("_BaseColor"))
                        originalColors[i][j] = originalSharedMats[i][j].GetColor("_BaseColor");
                    else if (originalSharedMats[i][j].HasProperty("_Color"))
                        originalColors[i][j] = originalSharedMats[i][j].GetColor("_Color");
                    else
                        originalColors[i][j] = Color.white;
                }
            }
        }
        isCached = true;
    }

    void OnMouseEnter()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Building)
            return;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        // まだキャッシュされていない場合（動的に追加された場合など）
        if (!isCached) CacheRenderers();

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
        if (renderers == null || originalSharedMats == null) return;

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
            if (renderers[i] == null) continue;

            if (enable)
            {
                // renderer.materials でインスタンス化されたコピーを取得・適用し、色を変更
                Material[] mats = renderers[i].materials;
                for (int j = 0; j < mats.Length; j++)
                {
                    if (mats[j] == null) continue;
                    Color target = Color.Lerp(originalColors[i][j], tint, strength);

                    if (mats[j].HasProperty("_BaseColor"))
                        mats[j].SetColor("_BaseColor", target);
                    else if (mats[j].HasProperty("_Color"))
                        mats[j].SetColor("_Color", target);
                }
            }
            else
            {
                // オリジナルのsharedMaterialsに戻す（インスタンスを破棄）
                renderers[i].sharedMaterials = originalSharedMats[i];
            }
        }
    }

    void OnDisable()
    {
        SetHighlight(false);
    }
}