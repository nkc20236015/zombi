using UnityEngine;

/// <summary>
/// マップ上の採取可能なオブジェクト（木・岩など）にアタッチするスクリプト。
/// 資源の種類・残量を管理し、NPCがアクセスして採取できるようにする。
/// </summary>
public class ResourceNode : MonoBehaviour
{
    [Header("Resource Settings")]
    [SerializeField] private ResourceType resourceType = ResourceType.Wood;
    [SerializeField] private int maxAmount = 50;
    [SerializeField] private int harvestAmountPerAction = 5; // 1回の採取で得られる量

    [Header("Visual")]
    [SerializeField] private float depletedScale = 0.3f; // 枯渇時の縮小率

    /// <summary>このノードの資源タイプ</summary>
    public ResourceType Type => resourceType;

    /// <summary>残りの資源量</summary>
    public int CurrentAmount { get; private set; }

    /// <summary>資源が残っているか</summary>
    public bool HasResources => CurrentAmount > 0;

    /// <summary>1回の採取で得られる量</summary>
    public int HarvestAmount => harvestAmountPerAction;

    /// <summary>NPCが停止して採取を開始する距離</summary>
    public float InteractionRange => 2.0f;

    private Vector3 originalScale;

    void Awake()
    {
        CurrentAmount = maxAmount;
        originalScale = transform.localScale;
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

        // 残量に応じてスケールを変化（視覚フィードバック）
        float ratio = (float)CurrentAmount / maxAmount;
        float scaleFactor = Mathf.Lerp(depletedScale, 1f, ratio);
        transform.localScale = originalScale * scaleFactor;

        // 枯渇したらオブジェクトを非アクティブにする
        if (CurrentAmount <= 0)
        {
            Debug.Log($"[ResourceNode] {gameObject.name} has been depleted!");
            gameObject.SetActive(false);
        }

        return harvested;
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
