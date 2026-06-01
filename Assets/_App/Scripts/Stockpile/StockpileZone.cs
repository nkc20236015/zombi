using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 1つの備蓄場エリアを管理するクラス。
/// マス目のリストを保持し、ロープの境界線を描画する。
/// </summary>
public class StockpileZone : MonoBehaviour
{
    /// <summary>この備蓄場が占有するグリッド座標のセット</summary>
    private HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
    public IReadOnlyCollection<Vector2Int> Cells => cells;

    /// <summary>各マスに置かれているアイテム数を追跡</summary>
    private Dictionary<Vector2Int, int> storedAmounts = new Dictionary<Vector2Int, int>();

    /// <summary>1マスあたりの最大保管量</summary>
    public int MaxPerCell => 200;

    // ロープ描画用
    private LineRenderer ropeRenderer;
    private static readonly Color ropeColor = new Color(0.55f, 0.35f, 0.15f, 1f); // 茶色（ロープ風）
    private static readonly float ropeWidth = 0.08f;

    void Awake()
    {
        // ロープ表現用のLineRendererを設定
        ropeRenderer = gameObject.AddComponent<LineRenderer>();
        ropeRenderer.useWorldSpace = true;
        ropeRenderer.loop = true;
        ropeRenderer.startWidth = ropeWidth;
        ropeRenderer.endWidth = ropeWidth;
        ropeRenderer.numCapVertices = 4;
        ropeRenderer.numCornerVertices = 4;
        ropeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ropeRenderer.receiveShadows = false;

        // マテリアル設定（テクスチャ未割当の場合はプレーン茶色）
        Material ropeMat = new Material(Shader.Find("Sprites/Default"));
        ropeMat.color = ropeColor;
        ropeRenderer.material = ropeMat;
        ropeRenderer.startColor = ropeColor;
        ropeRenderer.endColor = ropeColor;
    }

    /// <summary>
    /// グリッド座標の矩形範囲からゾーンを初期化する。
    /// </summary>
    public void Initialize(Vector2Int min, Vector2Int max)
    {
        cells.Clear();
        storedAmounts.Clear();

        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                Vector2Int pos = new Vector2Int(x, z);
                if (GridManager.Instance != null && GridManager.Instance.IsValidPosition(pos))
                {
                    cells.Add(pos);
                    storedAmounts[pos] = 0;
                }
            }
        }

        RebuildRopeBorder();
    }

    /// <summary>
    /// ゾーン内で空きのあるマスを返す。NPCが運搬先を探す時に使う。
    /// npcPositionに最も近い空きマスを優先。
    /// </summary>
    public bool TryGetAvailableCell(Vector3 npcPosition, out Vector2Int bestCell)
    {
        bestCell = Vector2Int.zero;
        float bestDist = float.MaxValue;
        bool found = false;

        foreach (var cell in cells)
        {
            int stored = storedAmounts.ContainsKey(cell) ? storedAmounts[cell] : 0;
            if (stored >= MaxPerCell) continue;

            Vector3 worldPos = GridManager.Instance.GridToWorld(cell);
            float dist = Vector3.Distance(npcPosition, worldPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCell = cell;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// 指定マスにアイテムを保管（数量を加算）
    /// </summary>
    public void StoreItem(Vector2Int cell, int amount)
    {
        if (!storedAmounts.ContainsKey(cell)) storedAmounts[cell] = 0;
        storedAmounts[cell] += amount;
    }

    /// <summary>
    /// このゾーンに空きがあるか（1マスでも保管可能なら true）
    /// </summary>
    public bool HasSpace()
    {
        foreach (var cell in cells)
        {
            int stored = storedAmounts.ContainsKey(cell) ? storedAmounts[cell] : 0;
            if (stored < MaxPerCell) return true;
        }
        return false;
    }

    /// <summary>
    /// セルの個数
    /// </summary>
    public int CellCount => cells.Count;

    // ==================== ロープ境界線の描画 ====================

    /// <summary>
    /// セルの外周（ペリメーター）を計算し、LineRendererでロープを描画する。
    /// マス目の境界線に沿って四角形の辺を繋ぐ。
    /// </summary>
    private void RebuildRopeBorder()
    {
        if (ropeRenderer == null || GridManager.Instance == null || cells.Count == 0) return;

        // 外周のエッジを収集する
        // 各セルの4辺のうち、隣にセルがない辺だけが外周
        HashSet<(Vector2Int, Vector2Int)> edges = new HashSet<(Vector2Int, Vector2Int)>();

        foreach (var cell in cells)
        {
            float cellSizeX = GridManager.Instance.CellSizeX;
            float cellSizeZ = GridManager.Instance.CellSizeZ;
            Vector3 origin = GridManager.Instance.GridOrigin;

            float left = cell.x * cellSizeX + origin.x;
            float right = (cell.x + 1) * cellSizeX + origin.x;
            float bottom = cell.y * cellSizeZ + origin.z;
            float top = (cell.y + 1) * cellSizeZ + origin.z;

            // 上辺 (cell.y+1方向に隣がなければ外周)
            if (!cells.Contains(new Vector2Int(cell.x, cell.y + 1)))
            {
                AddEdge(edges, 
                    new Vector2Int(Mathf.RoundToInt(left * 1000), Mathf.RoundToInt(top * 1000)),
                    new Vector2Int(Mathf.RoundToInt(right * 1000), Mathf.RoundToInt(top * 1000)));
            }
            // 下辺
            if (!cells.Contains(new Vector2Int(cell.x, cell.y - 1)))
            {
                AddEdge(edges,
                    new Vector2Int(Mathf.RoundToInt(right * 1000), Mathf.RoundToInt(bottom * 1000)),
                    new Vector2Int(Mathf.RoundToInt(left * 1000), Mathf.RoundToInt(bottom * 1000)));
            }
            // 右辺
            if (!cells.Contains(new Vector2Int(cell.x + 1, cell.y)))
            {
                AddEdge(edges,
                    new Vector2Int(Mathf.RoundToInt(right * 1000), Mathf.RoundToInt(top * 1000)),
                    new Vector2Int(Mathf.RoundToInt(right * 1000), Mathf.RoundToInt(bottom * 1000)));
            }
            // 左辺
            if (!cells.Contains(new Vector2Int(cell.x - 1, cell.y)))
            {
                AddEdge(edges,
                    new Vector2Int(Mathf.RoundToInt(left * 1000), Mathf.RoundToInt(bottom * 1000)),
                    new Vector2Int(Mathf.RoundToInt(left * 1000), Mathf.RoundToInt(top * 1000)));
            }
        }

        // エッジを繋いでポリゴンの頂点順序にする
        List<Vector3> orderedPoints = OrderEdgesIntoLoop(edges);

        if (orderedPoints.Count == 0)
        {
            ropeRenderer.positionCount = 0;
            return;
        }

        // Y座標をTerrainの高さに合わせる
        for (int i = 0; i < orderedPoints.Count; i++)
        {
            Vector3 p = orderedPoints[i];
            if (VoxelWorld.Instance != null)
            {
                p.y = VoxelWorld.Instance.GetSurfaceWorldY(p.x, p.z) + 0.15f; // 地面から少し浮かせる
            }
            else
            {
                p.y = 0.15f;
            }
            orderedPoints[i] = p;
        }

        ropeRenderer.positionCount = orderedPoints.Count;
        ropeRenderer.SetPositions(orderedPoints.ToArray());
    }

    private void AddEdge(HashSet<(Vector2Int, Vector2Int)> edges, Vector2Int a, Vector2Int b)
    {
        edges.Add((a, b));
    }

    /// <summary>
    /// エッジのリストを繋いで、連続した頂点ループに変換する。
    /// </summary>
    private List<Vector3> OrderEdgesIntoLoop(HashSet<(Vector2Int, Vector2Int)> edges)
    {
        if (edges.Count == 0) return new List<Vector3>();

        // 隣接リストを構築
        Dictionary<Vector2Int, List<Vector2Int>> adjacency = new Dictionary<Vector2Int, List<Vector2Int>>();
        foreach (var (a, b) in edges)
        {
            if (!adjacency.ContainsKey(a)) adjacency[a] = new List<Vector2Int>();
            adjacency[a].Add(b);
        }

        List<Vector3> result = new List<Vector3>();
        HashSet<(Vector2Int, Vector2Int)> visited = new HashSet<(Vector2Int, Vector2Int)>();

        // 最初のエッジから開始
        var firstEdge = default((Vector2Int, Vector2Int));
        foreach (var e in edges) { firstEdge = e; break; }

        Vector2Int current = firstEdge.Item1;
        Vector2Int start = current;

        result.Add(new Vector3(current.x / 1000f, 0, current.y / 1000f));

        int maxIterations = edges.Count + 10; // 安全弁
        int iterations = 0;

        while (iterations < maxIterations)
        {
            iterations++;
            bool found = false;

            if (adjacency.ContainsKey(current))
            {
                foreach (var next in adjacency[current])
                {
                    if (visited.Contains((current, next))) continue;

                    visited.Add((current, next));
                    current = next;

                    if (current == start && result.Count > 2)
                    {
                        // ループ完了
                        return result;
                    }

                    result.Add(new Vector3(current.x / 1000f, 0, current.y / 1000f));
                    found = true;
                    break;
                }
            }

            if (!found) break; // 行き止まり（L字型など複雑な形状の場合）
        }

        return result;
    }

    /// <summary>
    /// ロープのテクスチャを外部から設定する（将来用）
    /// </summary>
    public void SetRopeTexture(Texture2D ropeTexture)
    {
        if (ropeRenderer != null && ropeTexture != null)
        {
            ropeRenderer.material.mainTexture = ropeTexture;
        }
    }
}
