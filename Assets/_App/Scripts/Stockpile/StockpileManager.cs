using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ゲーム全体の備蓄場を管理するシングルトン。
/// 備蓄場の作成、一覧管理、NPCからの検索APIを提供する。
/// </summary>
public class StockpileManager : MonoBehaviour
{
    public static StockpileManager Instance { get; private set; }

    private List<StockpileZone> zones = new List<StockpileZone>();
    public IReadOnlyList<StockpileZone> Zones => zones;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// 新しい備蓄場を指定のグリッド矩形で作成する。
    /// </summary>
    public StockpileZone CreateZone(Vector2Int gridMin, Vector2Int gridMax)
    {
        if (GridManager.Instance == null) return null;

        // グリッド範囲のクランプ
        gridMin.x = Mathf.Max(0, gridMin.x);
        gridMin.y = Mathf.Max(0, gridMin.y);
        gridMax.x = Mathf.Min(GridManager.Instance.GridWidth - 1, gridMax.x);
        gridMax.y = Mathf.Min(GridManager.Instance.GridHeight - 1, gridMax.y);

        if (gridMin.x > gridMax.x || gridMin.y > gridMax.y) return null;

        // ゾーンのGameObjectを生成
        GameObject zoneObj = new GameObject($"Stockpile_{zones.Count}");
        zoneObj.transform.parent = transform;

        StockpileZone zone = zoneObj.AddComponent<StockpileZone>();
        zone.Initialize(gridMin, gridMax);

        zones.Add(zone);
        Debug.Log($"[StockpileManager] 備蓄場作成: ({gridMin.x},{gridMin.y}) - ({gridMax.x},{gridMax.y}), {zone.CellCount}マス");

        return zone;
    }

    /// <summary>
    /// NPCの現在位置から最も近い、空きのある備蓄場のマスを検索する。
    /// </summary>
    public bool TryGetNearestAvailableCell(Vector3 npcPosition, out StockpileZone bestZone, out Vector2Int bestCell)
    {
        bestZone = null;
        bestCell = Vector2Int.zero;
        float bestDist = float.MaxValue;

        foreach (var zone in zones)
        {
            if (zone == null || !zone.HasSpace()) continue;

            if (zone.TryGetAvailableCell(npcPosition, out Vector2Int cell))
            {
                Vector3 cellWorld = GridManager.Instance.GridToWorld(cell);
                float dist = Vector3.Distance(npcPosition, cellWorld);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestZone = zone;
                    bestCell = cell;
                }
            }
        }

        return bestZone != null;
    }

    /// <summary>
    /// 備蓄場が1つでも存在するか
    /// </summary>
    public bool HasAnyZone()
    {
        return zones.Count > 0;
    }

    /// <summary>
    /// 備蓄場に空きがあるか
    /// </summary>
    public bool HasAnyAvailableSpace()
    {
        foreach (var zone in zones)
        {
            if (zone != null && zone.HasSpace()) return true;
        }
        return false;
    }

    /// <summary>
    /// 指定のグリッド座標がいずれかの備蓄場ゾーンの中にあるか判定する。
    /// 備蓄場に保管されたアイテムをNPCが再度拾いに行くのを防ぐために使用。
    /// </summary>
    public bool IsInsideAnyZone(Vector2Int gridPos)
    {
        foreach (var zone in zones)
        {
            if (zone == null) continue;
            if (zone.ContainsCell(gridPos)) return true;
        }
        return false;
    }
}
