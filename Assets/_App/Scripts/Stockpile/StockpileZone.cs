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
    private List<LineRenderer> cellRenderers = new List<LineRenderer>();
    private static readonly Color ropeColor = new Color(0.55f, 0.35f, 0.15f, 1f); // 茶色（ロープ風）
    private static readonly float ropeWidth = 0.08f;

    void Awake()
    {
        // 以前の共有LineRendererのセットアップは削除。各セルごとに生成する。
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

                    // 雑草（草テクスチャ）を消して土にする
                    if (VoxelWorld.Instance != null)
                    {
                        VoxelWorld.Instance.ClearGrassAtGrid(pos);
                    }
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
    /// 指定のグリッド座標がこのゾーン内に含まれているか
    /// </summary>
    public bool ContainsCell(Vector2Int gridPos)
    {
        return cells.Contains(gridPos);
    }

    /// <summary>
    /// セルの個数
    /// </summary>
    public int CellCount => cells.Count;

    // ==================== ロープ境界線の描画 ====================

    /// <summary>
    /// 各セルごとに独立した四角形のロープを描画する。
    /// 一マスで作った時と同じ見た目を維持するため、外周の結合は行わない。
    /// </summary>
    private void RebuildRopeBorder()
    {
        if (GridManager.Instance == null) return;

        // 既存のレンダラーをクリア
        foreach (var lr in cellRenderers)
        {
            if (lr != null) Destroy(lr.gameObject);
        }
        cellRenderers.Clear();

        Material ropeMat = new Material(Shader.Find("Sprites/Default"));
        ropeMat.color = ropeColor;

        float cellSizeX = GridManager.Instance.CellSizeX;
        float cellSizeZ = GridManager.Instance.CellSizeZ;
        Vector3 origin = GridManager.Instance.GridOrigin;

        // 各セルごとにLineRendererを生成
        foreach (var cell in cells)
        {
            GameObject cellObj = new GameObject($"CellRope_{cell.x}_{cell.y}");
            cellObj.transform.parent = transform;

            LineRenderer lr = cellObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.startWidth = ropeWidth;
            lr.endWidth = ropeWidth;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = ropeMat;
            lr.startColor = ropeColor;
            lr.endColor = ropeColor;

            float left = cell.x * cellSizeX + origin.x;
            float right = (cell.x + 1) * cellSizeX + origin.x;
            float bottom = cell.y * cellSizeZ + origin.z;
            float top = (cell.y + 1) * cellSizeZ + origin.z;

            // 各角のY座標を取得
            float GetY(float x, float z)
            {
                if (VoxelWorld.Instance != null)
                    return VoxelWorld.Instance.GetSurfaceWorldY(x, z) + 0.15f;
                return 0.15f;
            }

            Vector3[] points = new Vector3[4];
            points[0] = new Vector3(left, GetY(left, bottom), bottom);
            points[1] = new Vector3(left, GetY(left, top), top);
            points[2] = new Vector3(right, GetY(right, top), top);
            points[3] = new Vector3(right, GetY(right, bottom), bottom);

            lr.positionCount = 4;
            lr.SetPositions(points);

            cellRenderers.Add(lr);
        }
    }

    /// <summary>
    /// ロープのテクスチャを外部から設定する（将来用）
    /// </summary>
    public void SetRopeTexture(Texture2D ropeTexture)
    {
        if (ropeTexture != null)
        {
            foreach (var lr in cellRenderers)
            {
                if (lr != null && lr.material != null)
                {
                    lr.material.mainTexture = ropeTexture;
                }
            }
        }
    }
}
