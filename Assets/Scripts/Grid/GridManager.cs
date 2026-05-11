using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Settings")]
    [Tooltip("VoxelWorldが存在する場合、自動的にワールドサイズに合わせます")]
    [SerializeField] private int gridWidth = 50;
    [SerializeField] private int gridHeight = 50;
    [SerializeField] private Vector3 gridOrigin = Vector3.zero;

    // セルサイズはVoxelDataのブロックサイズに固定
    public float CellSizeX => VoxelData.BlockWidth;
    public float CellSizeZ => VoxelData.BlockDepth;

    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public Vector3 GridOrigin => gridOrigin;

    // 後方互換: 旧CellSizeプロパティ（X方向のサイズを返す）
    public float CellSize => CellSizeX;

    private GridCell[,] cells;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        InitializeGrid();
    }

    /// <summary>
    /// VoxelWorldのサイズに合わせてグリッドを初期化する。
    /// VoxelWorldが存在しない場合はインスペクターの値を使用。
    /// </summary>
    public void InitializeGrid()
    {
        if (VoxelWorld.Instance != null)
        {
            gridWidth = VoxelWorld.Instance.WorldWidth;
            gridHeight = VoxelWorld.Instance.WorldDepth;
            // VoxelWorldの原点とグリッドの原点を強制的に同期させる
            gridOrigin = VoxelWorld.Instance.transform.position;
            Debug.Log($"[GridManager] VoxelWorldに合わせてグリッドを初期化: {gridWidth}x{gridHeight}, 原点={gridOrigin}");
        }

        cells = new GridCell[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridHeight; z++)
                cells[x, z] = new GridCell(new Vector2Int(x, z));
    }

    /// <summary>
    /// ワールド座標からグリッド座標(XZ)に変換。
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 wp)
    {
        int x = Mathf.FloorToInt((wp.x - gridOrigin.x) / CellSizeX);
        int z = Mathf.FloorToInt((wp.z - gridOrigin.z) / CellSizeZ);
        return new Vector2Int(x, z);
    }

    /// <summary>
    /// グリッド座標からワールド座標に変換。
    /// Y座標はVoxelWorldの地表面に合わせる。
    /// </summary>
    public Vector3 GridToWorld(Vector2Int gp)
    {
        float x = gp.x * CellSizeX + gridOrigin.x + CellSizeX * 0.5f;
        float z = gp.y * CellSizeZ + gridOrigin.z + CellSizeZ * 0.5f;

        // VoxelWorldが存在する場合、地表面の高さを使用
        float y = 0f;
        if (VoxelWorld.Instance != null)
        {
            y = VoxelWorld.Instance.GetSurfaceWorldY(x, z);
        }

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// グリッド座標からワールド座標に変換（ブロックの中心に配置、高さ指定可能）。
    /// stackHeight: 地上から何ブロック目に積むか（0=地上直置き）
    /// </summary>
    public Vector3 GridToWorldForBuilding(Vector2Int gp, int stackHeight = 0)
    {
        float x = gp.x * CellSizeX + gridOrigin.x + CellSizeX * 0.5f;
        float z = gp.y * CellSizeZ + gridOrigin.z + CellSizeZ * 0.5f;

        float y = 0f;
        if (VoxelWorld.Instance != null)
        {
            y = VoxelWorld.Instance.GetSurfaceWorldY(x, z);
        }
        y += stackHeight * VoxelData.BlockHeight;

        return new Vector3(x, y, z);
    }

    public bool IsValidPosition(Vector2Int gp)
    {
        return gp.x >= 0 && gp.x < gridWidth && gp.y >= 0 && gp.y < gridHeight;
    }

    public bool CanPlace(Vector2Int gp)
    {
        if (!IsValidPosition(gp)) return false;
        if (cells[gp.x, gp.y].State != CellState.Empty) return false;

        // 全てのNPCが立っているセルには設置不可
        if (GameManager.Instance != null && GameManager.Instance.NPCs != null)
        {
            foreach (var npc in GameManager.Instance.NPCs)
            {
                if (npc == null) continue;
                Vector2Int npcGridPos = WorldToGrid(npc.transform.position);
                if (npcGridPos == gp) return false;
            }
        }

        return true;
    }

    public bool PlaceObject(Vector2Int gp, GameObject obj, bool blockWalking = true)
    {
        if (!CanPlace(gp)) return false;
        var cell = cells[gp.x, gp.y];
        cell.State = CellState.Occupied;
        cell.Occupant = obj;
        cell.IsWalkable = !blockWalking;
        return true;
    }

    public bool RemoveObject(Vector2Int gp)
    {
        if (!IsValidPosition(gp)) return false;
        var cell = cells[gp.x, gp.y];
        if (cell.State != CellState.Occupied) return false;
        cell.State = CellState.Empty;
        cell.Occupant = null;
        cell.IsWalkable = true;
        return true;
    }

    public GridCell GetCell(Vector2Int gp)
    {
        if (!IsValidPosition(gp)) return null;
        return cells[gp.x, gp.y];
    }

    public bool TryGetGridPositionFromMouse(out Vector2Int gridPos)
    {
        gridPos = Vector2Int.zero;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            gridPos = WorldToGrid(hit.point);
            return IsValidPosition(gridPos);
        }
        return false;
    }
}