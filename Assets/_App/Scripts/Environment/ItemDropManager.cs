using UnityEngine;
using System.Collections.Generic;

public class ItemDropManager : MonoBehaviour
{
    public static ItemDropManager Instance { get; private set; }

    [Header("Wood Stack Settings")]
    public int maxWoodPerStack = 200;

    [Header("Wood Prefabs (Assigned automatically if empty)")]
    public GameObject woodPrefab1; // 1-49
    public GameObject woodPrefab2; // 50-99
    public GameObject woodPrefab3; // 100-149
    public GameObject woodPrefab4; // 150-200

    // グリッド上のドロップアイテムを管理する辞書
    private Dictionary<Vector2Int, DroppedResource> gridItems = new Dictionary<Vector2Int, DroppedResource>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

#if UNITY_EDITOR
        // エディタ上で指定のプレハブが未割り当ての場合、自動で読み込む
        if (woodPrefab1 == null) woodPrefab1 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Static Soul Studio/Wood Pack/Built-in/Prefabs/Small Log_1.prefab");
        if (woodPrefab2 == null) woodPrefab2 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Static Soul Studio/Wood Pack/Built-in/Prefabs/Small Log_2.prefab");
        if (woodPrefab3 == null) woodPrefab3 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Static Soul Studio/Wood Pack/Built-in/Prefabs/Small Log_3.prefab");
        if (woodPrefab4 == null) woodPrefab4 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Static Soul Studio/Wood Pack/Built-in/Prefabs/Small Log_4.prefab");
#endif
    }

    public GameObject GetWoodPrefabForAmount(int amount)
    {
        if (amount >= 150) return woodPrefab4;
        if (amount >= 100) return woodPrefab3;
        if (amount >= 50) return woodPrefab2;
        return woodPrefab1;
    }

    public bool HasItemAt(Vector2Int gridPos)
    {
        return gridItems.ContainsKey(gridPos);
    }

    /// <summary>
    /// 木材を指定のグリッド位置にドロップする。
    /// 既にスタックがある場合は統合し、最大数を超える場合は隣接マスに溢れさせる。
    /// </summary>
    public void DropWood(Vector2Int gridPos, int amount)
    {
        if (amount <= 0 || GridManager.Instance == null) return;

        int remainingAmount = amount;
        Vector2Int currentTargetPos = gridPos;

        // 近くの空きマス、または追加可能なマスを探す（簡単なBFS）
        Queue<Vector2Int> searchQueue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        searchQueue.Enqueue(currentTargetPos);
        visited.Add(currentTargetPos);

        while (remainingAmount > 0 && searchQueue.Count > 0)
        {
            Vector2Int pos = searchQueue.Dequeue();

            if (!GridManager.Instance.IsValidPosition(pos)) continue;

            // すでにアイテムがあるか確認
            if (gridItems.TryGetValue(pos, out DroppedResource existingResource))
            {
                if (existingResource.Type == ResourceType.Wood)
                {
                    int spaceLeft = maxWoodPerStack - existingResource.Amount;
                    if (spaceLeft > 0)
                    {
                        int addAmount = Mathf.Min(spaceLeft, remainingAmount);
                        existingResource.AddAmount(addAmount);
                        remainingAmount -= addAmount;
                    }
                }
            }
            else
            {
                // アイテムがない場合は新規作成
                // セルの中心に配置する
                Vector3 worldPos = GridManager.Instance.GridToWorld(pos);
                
                GameObject dropObj = new GameObject($"DroppedWood_{pos.x}_{pos.y}");
                dropObj.transform.position = worldPos;

                DroppedResource newResource = dropObj.AddComponent<DroppedResource>();
                
                int addAmount = Mathf.Min(maxWoodPerStack, remainingAmount);
                newResource.Initialize(ResourceType.Wood, addAmount, pos);
                
                gridItems[pos] = newResource;
                remainingAmount -= addAmount;
            }

            // まだ残っていれば、隣接マスを探索キューに追加
            if (remainingAmount > 0)
            {
                Vector2Int[] neighbors = {
                    new Vector2Int(pos.x + 1, pos.y),
                    new Vector2Int(pos.x - 1, pos.y),
                    new Vector2Int(pos.x, pos.y + 1),
                    new Vector2Int(pos.x, pos.y - 1)
                };

                foreach (var neighbor in neighbors)
                {
                    if (GridManager.Instance.IsValidPosition(neighbor) && !visited.Contains(neighbor))
                    {
                        // 障害物がないか（NPCや建物など）をチェックするべきだが、
                        // アイテムなのでとりあえず置けるマスならOKとする
                        visited.Add(neighbor);
                        searchQueue.Enqueue(neighbor);
                    }
                }
            }
        }
    }
}
