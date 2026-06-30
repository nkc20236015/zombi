using UnityEngine;

/// <summary>
/// タスクマーカー（アイコン）の表示とカメラ追従を管理する。
/// 親オブジェクト（木など）が動的に拡縮しても、アイコン自体の見た目のサイズを一定に保つ。
/// </summary>
public class TaskMarker : MonoBehaviour
{
    private Camera mainCamera;
    
    [SerializeField] private float bobbingSpeed = 2f;
    [SerializeField] private float bobbingAmount = 0.2f;
    
    private Vector3 initialLocalPosition;
    private Transform parentTransform;
    private float targetWorldSize = 1.5f; // アイコンの目標ワールドサイズ
    private GameObject customIconObj;     // ユーザー指定のAxeIconなどの実体
    private bool hasCanvas = false;       // Canvas追加済みフラグ

    public void Initialize(Transform parent, float localY, float worldSize, GameObject customPrefab = null)
    {
        parentTransform = parent;
        targetWorldSize = worldSize;
        initialLocalPosition = new Vector3(0, localY, 0);
        transform.localPosition = initialLocalPosition;

        if (customPrefab != null)
        {
            SetupIconFromPrefab(customPrefab);
        }
    }

    /// <summary>
    /// アイコンプレハブを差し替える。既存のアイコンを破棄して新しいものを生成する。
    /// タスク登録時に呼ばれ、モード（採取/切る）に応じた正しいアイコンを表示する。
    /// </summary>
    public void SwapIcon(GameObject newPrefab)
    {
        if (newPrefab == null) return;

        // 既存のアイコンオブジェクトを破棄
        if (customIconObj != null)
        {
            Destroy(customIconObj);
            customIconObj = null;
        }

        SetupIconFromPrefab(newPrefab);
    }

    /// <summary>
    /// プレハブからアイコンオブジェクトを生成し、Canvas等の設定を行う共通処理。
    /// </summary>
    private void SetupIconFromPrefab(GameObject prefab)
    {
        customIconObj = Instantiate(prefab, transform);
        
        RectTransform rt = customIconObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            // UI(Image等)の場合、3D空間で表示するためにWorldSpace Canvasを追加（初回のみ）
            if (!hasCanvas)
            {
                Canvas canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 10;
                hasCanvas = true;
            }
            
            // ピクセルサイズが大きすぎるのを防ぐため、基本サイズ(100)を基準に1x1サイズに縮小
            float maxDim = Mathf.Max(rt.rect.width, rt.rect.height);
            if (maxDim <= 0.01f) maxDim = 100f;
            
            rt.localScale = new Vector3(1f / maxDim, 1f / maxDim, 1f / maxDim);
            rt.localPosition = Vector3.zero;
            // UIはそのままビルボードさせると左右反転することがあるため補正
            rt.localRotation = Quaternion.Euler(0, 180f, 0); 
        }
        else
        {
            customIconObj.transform.localPosition = Vector3.zero;
            customIconObj.transform.localRotation = Quaternion.identity;
        }
        
        // 既存のSpriteRendererがあれば消す（フォールバック用）
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) Destroy(sr);
    }

    void Awake()
    {
        mainCamera = Camera.main;
        // fallback in case Initialize is called late
        if (initialLocalPosition == Vector3.zero) 
            initialLocalPosition = transform.localPosition;
    }

    void Update()
    {
        if (mainCamera == null) return;

        // ビルボード（常にカメラを向く）
        transform.forward = mainCamera.transform.forward;

        if (parentTransform != null)
        {
            // 親（木）のスケールが変化しても、アイコン自身のワールドサイズは一定に保つ
            // これにより、木が縮んでもアイコンは小さくならない。
            // さらに親の子要素であるため、木が縮むとアイコンのワールド高さは自動的に木の上端に追従する。
            transform.localScale = new Vector3(
                targetWorldSize / parentTransform.lossyScale.x,
                targetWorldSize / parentTransform.lossyScale.y,
                targetWorldSize / parentTransform.lossyScale.z
            );
        }

        // 上下のフワフワアニメーション
        float newY = initialLocalPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;
        transform.localPosition = new Vector3(initialLocalPosition.x, newY, initialLocalPosition.z);
    }

    /// <summary>
    /// マーカーの表示ON/OFFを設定
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = isVisible;

        if (customIconObj != null)
        {
            customIconObj.SetActive(isVisible);
        }
        else
        {
            // fallback: direct children with renderers
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                r.enabled = isVisible;
            }
        }
    }
}