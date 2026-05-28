using UnityEngine;
using DG.Tweening;

/// <summary>
/// マップ上の採取可能なオブジェクト（木・岩など）にアタッチするスクリプト。
/// 資源の種類・残量を管理し、NPCがアクセスして採取できるようにする。
/// </summary>
public class ResourceNode : MonoBehaviour
{
    [Header("Resource Settings")]
    [SerializeField] private ResourceType resourceType = ResourceType.Wood;
    [SerializeField] private int minYield = 40;
    [SerializeField] private int maxYield = 60;
    [SerializeField] private int harvestAmountPerAction = 5; // 1回の採取で得られる量

    [Header("Visual - 岩など（Wood以外）")]
    [SerializeField] private float depletedScale = 0.3f; // 枯渇時の縮小率

    [Header("Visual - 木の伐採演出")]
    [SerializeField] private float shakeStrength = 2f;     // 採取時の揺れの強さ（角度）
    [SerializeField] private float shakeDuration = 0.4f;   // 揺れの持続時間
    [SerializeField] private float fallDuration = 1.5f;    // 倒れるまでの時間
    [SerializeField] private float fallAngle = 85f;        // 倒れる角度
    [SerializeField] private float disappearDelay = 1.0f;  // 倒れた後に消えるまでの待機時間

    /// <summary>このノードの資源タイプ</summary>
    public ResourceType Type => resourceType;

    /// <summary>残りの資源量</summary>
    public int CurrentAmount { get; private set; }

    /// <summary>資源が残っているか</summary>
    public bool HasResources => CurrentAmount > 0;

    /// <summary>このノードが生成する総資源量</summary>
    public int ActualYield { get; private set; }

    /// <summary>1回の採取で得られる量</summary>
    public int HarvestAmount => harvestAmountPerAction;

    /// <summary>NPCが停止して採取を開始する距離</summary>
    public float InteractionRange => 1.0f;

    private Vector3 originalScale;
    private Quaternion originalRotation;
    private bool isFalling = false; // 倒木中フラグ（二重実行防止）
    private Vector2Int gridPos;
    private bool hasGridPos = false;

    public void SetGridPosition(Vector2Int gp)
    {
        gridPos = gp;
        hasGridPos = true;
    }

    void Awake()
    {
        ActualYield = Random.Range(minYield, maxYield + 1);
        CurrentAmount = ActualYield;
        originalScale = transform.localScale;
        originalRotation = transform.localRotation;
        
        // タスクマーカーの初期化 (Instantiate直後に呼ばれるSetTaskMarkerに対応するためAwakeで実行)
        InitializeTaskMarker();
    }

    void Start()
    {
        // TaskMarkerはAwakeで初期化済み

        // HoverHighlight を動的にアタッチ (もし付いていなければ)
        if (GetComponent<HoverHighlight>() == null)
        {
            gameObject.AddComponent<HoverHighlight>();
        }
    }

    private TaskMarker taskMarker;

    private void InitializeTaskMarker()
    {
        taskMarker = GetComponentInChildren<TaskMarker>();
        if (taskMarker == null)
        {
            GameObject markerObj = new GameObject("TaskMarker");
            markerObj.transform.SetParent(transform);

            // オブジェクトのメッシュの最も高い位置を計算
            float maxLocalY = 2.0f;
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh != null)
                {
                    float topY = mf.transform.localPosition.y + mf.sharedMesh.bounds.max.y * mf.transform.localScale.y;
                    if (topY > maxLocalY) maxLocalY = topY;
                }
            }
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.sharedMesh != null)
                {
                    float topY = smr.transform.localPosition.y + smr.sharedMesh.bounds.max.y * smr.transform.localScale.y;
                    if (topY > maxLocalY) maxLocalY = topY;
                }
            }

            // 最上部から少し浮かせた位置に配置（0.5m上。スケール影響を考慮）
            float scaleY = transform.lossyScale.y;
            float localOffset = (scaleY > 0.01f) ? (0.5f / scaleY) : 0.5f;
            
            markerObj.transform.localPosition = new Vector3(0, maxLocalY + localOffset, 0);

            // スケールを均一に保つ（木が巨大・矮小でもアイコンの大きさは一定にする）
            markerObj.transform.localScale = new Vector3(
                1.0f / transform.lossyScale.x,
                1.0f / transform.lossyScale.y,
                1.0f / transform.lossyScale.z
            ) * 1.5f; // 見やすいように少し大きめに設定

            var sr = markerObj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            // アイコンの設定
#if UNITY_EDITOR
            // UIicon フォルダから斧アイコンを検索
            string[] axeGuids = UnityEditor.AssetDatabase.FindAssets("Axe t:Texture2D", new[] { "Assets/UIicon" });
            if (axeGuids.Length == 0)
                axeGuids = UnityEditor.AssetDatabase.FindAssets("Axe t:Texture2D");
            if (axeGuids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(axeGuids[0]);
                Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 32f);
                }
            }
#endif

            taskMarker = markerObj.AddComponent<TaskMarker>();
        }

        // 初期状態は非表示
        taskMarker.SetVisible(false);
    }

    public void SetTaskMarker(bool active)
    {
        if (taskMarker != null)
        {
            taskMarker.SetVisible(active);
        }
    }


    /// <summary>
    /// NPCが資源を1回分採取する。
    /// 採取できた量を返す。枯渇していた場合は0を返す。
    /// </summary>
    public int Harvest()
    {
        if (CurrentAmount <= 0) return 0;

        int harvested = Mathf.Min(harvestAmountPerAction, CurrentAmount);
        CurrentAmount -= harvested;

        if (resourceType == ResourceType.Wood)
        {
            HarvestTree();
        }
        else
        {
            HarvestNonTree();
        }

        return harvested;
    }

    /// <summary>
    /// 木の採取処理: 揺れ → 枯渇時に倒木
    /// </summary>
    private void HarvestTree()
    {
        if (isFalling) return;

        if (CurrentAmount > 0)
        {
            // 揺れはStrikeイベント経由でOnStrikeHit()から呼ばれる
        }
        else
        {
            // 枯渇した → 倒木アニメーション
            FallTree();
        }
    }

    /// <summary>
    /// 木以外の資源の採取処理: 従来通りスケール縮小 → 非アクティブ化
    /// </summary>
    private void HarvestNonTree()
    {
        // 残量に応じてスケールを変化（視覚フィードバック）
        float ratio = (float)CurrentAmount / ActualYield;
        float scaleFactor = Mathf.Lerp(depletedScale, 1f, ratio);
        transform.localScale = originalScale * scaleFactor;

        // 枯渇したらオブジェクトを非アクティブにする
        if (CurrentAmount <= 0)
        {
            Debug.Log($"[ResourceNode] {gameObject.name} has been depleted!");
            UnregisterFromGrid();
            gameObject.SetActive(false);
        }
    }

    private void UnregisterFromGrid()
    {
        if (hasGridPos && GridManager.Instance != null)
        {
            GridManager.Instance.RemoveObject(gridPos);
        }
    }

    /// <summary>
    /// 斧が当たった瞬間に呼ばれる。木をわずかに揺らす演出。
    /// NPCAnimationController の Strike イベント経由で外部から呼ばれる。
    /// </summary>
    public void OnStrikeHit()
    {
        if (isFalling || resourceType != ResourceType.Wood) return;

        // 既存の揺れTweenを止めてから新しい揺れを開始（重複防止）
        transform.DOKill(complete: false);

        // ローカルのZ軸周りに小さく揺らす（左右にブルブル揺れる感じ）
        Vector3 punchRotation = new Vector3(0f, 0f, shakeStrength);
        transform.DOPunchRotation(punchRotation, shakeDuration, vibrato: 8, elasticity: 0.3f);
    }

    /// <summary>
    /// 木を倒すアニメーション。ローカルX軸方向に倒れる。
    /// </summary>
    private void FallTree()
    {
        isFalling = true;

        // 進行中のTweenをすべてキャンセル
        transform.DOKill(complete: false);

        // コライダーを無効化（倒れている途中にNPCが引っかからないように）
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Debug.Log($"[ResourceNode] {gameObject.name} is falling!");

        // ローカルX軸方向に倒れる（前方に倒れる演出）
        Vector3 fallRotation = transform.localRotation.eulerAngles + new Vector3(fallAngle, 0f, 0f);

        transform.DOLocalRotate(fallRotation, fallDuration, RotateMode.Fast)
            .SetEase(Ease.InQuad) // 加速しながら倒れる（重力感）
            .OnComplete(() =>
            {
                Debug.Log($"[ResourceNode] {gameObject.name} has been depleted!");
                
                // 木が倒れたタイミングで、地面に木材をドロップする
                if (resourceType == ResourceType.Wood && ItemDropManager.Instance != null && GridManager.Instance != null)
                {
                    Vector2Int dropPos = hasGridPos ? gridPos : GridManager.Instance.WorldToGrid(transform.position);
                    ItemDropManager.Instance.DropWood(dropPos, ActualYield);
                }

                // 倒れた後、少し待ってから非アクティブにする
                DOVirtual.DelayedCall(disappearDelay, () =>
                {
                    UnregisterFromGrid();
                    gameObject.SetActive(false);
                });
            });
    }

    /// <summary>
    /// NPCが採取のために近づく位置を返す。
    /// ノードの中心からInteractionRange離れた最寄りの位置。
    /// </summary>
    public Vector3 GetHarvestPosition(Vector3 npcPosition)
    {
        Vector3 direction = (npcPosition - transform.position).normalized;
        return transform.position + direction * InteractionRange;
    }
}
