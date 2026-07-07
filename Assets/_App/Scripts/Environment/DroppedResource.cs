using UnityEngine;
using System.Collections.Generic;

public class DroppedResource : MonoBehaviour, ISelectable
{
    public ResourceType Type { get; private set; }
    public int Amount { get; private set; }
    public Vector2Int GridPosition { get; private set; }

    private GameObject currentVisual;

    public void Initialize(ResourceType type, int amount, Vector2Int gridPos)
    {
        Type = type;
        Amount = amount;
        GridPosition = gridPos;
        UpdateVisual();
    }

    public void AddAmount(int amountToAdd)
    {
        Amount += amountToAdd;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        GameObject prefabToUse = null;

        if (Type == ResourceType.Wood)
        {
            prefabToUse = ItemDropManager.Instance.GetWoodPrefabForAmount(Amount);
        }
        else if (Type == ResourceType.Food)
        {
            prefabToUse = ItemDropManager.Instance.foodPrefab;
        }

        if (prefabToUse != null)
        {
            // 既に同じプレハブのインスタンスがあるなら作り直さない
            if (currentVisual != null && currentVisual.name == prefabToUse.name)
            {
                return;
            }

            if (currentVisual != null)
            {
                Destroy(currentVisual);
            }

            currentVisual = Instantiate(prefabToUse, transform);
            currentVisual.name = prefabToUse.name; // 識別用

            // 1マスに収まるようにスケールを自動調整する
            FitToGridCell(currentVisual);
        }
    }

    private void FitToGridCell(GameObject visualObj)
    {
        if (GridManager.Instance == null) return;

        // メッシュのバウンディングボックスを取得
        Renderer[] renderers = visualObj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        // CellSizeX / CellSizeZ に収める
        float cellWidth = GridManager.Instance.CellSizeX;
        float cellDepth = GridManager.Instance.CellSizeZ;

        // マージンを少し持たせる
        float targetWidth = cellWidth * 0.9f;
        float targetDepth = cellDepth * 0.9f;

        float scaleX = targetWidth / bounds.size.x;
        float scaleZ = targetDepth / bounds.size.z;

        // X, Y, Z 同じ比率で縮小する（一番厳しい制限に合わせる）
        float scale = Mathf.Min(scaleX, scaleZ);

        // もともとセルより小さい場合は無理に拡大しない（スケール1のまま）
        if (scale < 1f)
        {
            visualObj.transform.localScale = new Vector3(scale, scale, scale);
        }

        UpdateCollider(bounds);
    }

    private void UpdateCollider(Bounds visualBounds)
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
        }
        // ローカル座標系でコライダーのサイズを合わせる
        col.center = currentVisual.transform.localPosition + new Vector3(0, visualBounds.size.y / 2f, 0);
        col.size = new Vector3(GridManager.Instance.CellSizeX * 0.8f, visualBounds.size.y, GridManager.Instance.CellSizeZ * 0.8f);
    }

    // ==================== ISelectable Implementation ====================

    public string GetSelectionName()
    {
        return $"アイテム ({GetResourceTypeJapanese()})";
    }

    public string GetSelectionDescription()
    {
        return "地面に落ちているアイテムです。NPCが備蓄場に運んでくれます。";
    }

    public Dictionary<string, string> GetSelectionStats()
    {
        var stats = new Dictionary<string, string>();
        stats.Add("種類", GetResourceTypeJapanese());
        stats.Add("数量", Amount.ToString());
        return stats;
    }

    private string GetResourceTypeJapanese()
    {
        switch (Type)
        {
            case ResourceType.Wood: return "木材";
            case ResourceType.Stone: return "石材";
            case ResourceType.Food: return "食料";
            default: return Type.ToString();
        }
    }
}
