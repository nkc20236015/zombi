using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 50;
    [SerializeField] private int gridHeight = 50;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector3 gridOrigin = Vector3.zero;

    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public float CellSize => cellSize;
    public Vector3 GridOrigin => gridOrigin;

    private GridCell[,] cells;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        cells = new GridCell[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridHeight; z++)
                cells[x, z] = new GridCell(new Vector2Int(x, z));
    }

    public Vector2Int WorldToGrid(Vector3 wp)
    {
        int x = Mathf.FloorToInt((wp.x - gridOrigin.x) / cellSize);
        int z = Mathf.FloorToInt((wp.z - gridOrigin.z) / cellSize);
        return new Vector2Int(x, z);
    }

    public Vector3 GridToWorld(Vector2Int gp)
    {
        float x = gp.x * cellSize + gridOrigin.x + cellSize * 0.5f;
        float z = gp.y * cellSize + gridOrigin.z + cellSize * 0.5f;
        return new Vector3(x, 0f, z);
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