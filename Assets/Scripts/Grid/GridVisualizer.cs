using UnityEngine;

[RequireComponent(typeof(GridManager))]
public class GridVisualizer : MonoBehaviour
{
    [Header("Grid Line Settings")]
    [SerializeField] private Color gridLineColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color gridBorderColor = new Color(1f, 0.8f, 0f, 0.6f);
    [SerializeField] private float lineHeight = 0.02f;

    [Header("Hover Highlight")]
    [SerializeField] private Color hoverValidColor = new Color(0f, 1f, 0f, 0.4f);
    [SerializeField] private Color hoverInvalidColor = new Color(1f, 0f, 0f, 0.4f);
    [SerializeField] private Color occupiedColor = new Color(0.5f, 0.5f, 1f, 0.3f);

    [Header("Display")]
    [SerializeField] private bool alwaysShowGrid = false;
    [SerializeField] private bool showOccupiedCells = true;

    private GridManager gridManager;
    private Material lineMaterial;
    private bool gridVisible;
    private Vector2Int hoveredCell = new Vector2Int(-1, -1);
    private bool hoveredCellValid;

    void Awake()
    {
        gridManager = GetComponent<GridManager>();
        CreateLineMaterial();
    }

    void Start()
    {
        gridVisible = alwaysShowGrid;
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerModeChanged += OnModeChanged;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerModeChanged -= OnModeChanged;
    }

    void Update()
    {
        if (gridVisible && gridManager.TryGetGridPositionFromMouse(out Vector2Int gp))
        {
            hoveredCell = gp;
            hoveredCellValid = gridManager.CanPlace(gp);
        }
        else
        {
            hoveredCell = new Vector2Int(-1, -1);
        }
    }

    void OnModeChanged(PlayerMode mode) { gridVisible = alwaysShowGrid || mode == PlayerMode.Building; }

    void CreateLineMaterial()
    {
        var shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        lineMaterial = new Material(shader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    void OnRenderObject()
    {
        if (!gridVisible || lineMaterial == null || gridManager == null) return;
        lineMaterial.SetPass(0);
        int w = gridManager.GridWidth;
        int h = gridManager.GridHeight;
        float cs = gridManager.CellSize;
        Vector3 o = gridManager.GridOrigin;
        float y = o.y + lineHeight;

        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);

        GL.Begin(GL.LINES);
        GL.Color(gridLineColor);
        for (int x = 0; x <= w; x++) { float xp = o.x + x * cs; GL.Vertex3(xp, y, o.z); GL.Vertex3(xp, y, o.z + h * cs); }
        for (int z = 0; z <= h; z++) { float zp = o.z + z * cs; GL.Vertex3(o.x, y, zp); GL.Vertex3(o.x + w * cs, y, zp); }
        GL.End();

        GL.Begin(GL.LINES);
        GL.Color(gridBorderColor);
        float x0 = o.x, x1 = o.x + w * cs, z0 = o.z, z1 = o.z + h * cs;
        GL.Vertex3(x0, y, z0); GL.Vertex3(x1, y, z0);
        GL.Vertex3(x0, y, z1); GL.Vertex3(x1, y, z1);
        GL.Vertex3(x0, y, z0); GL.Vertex3(x0, y, z1);
        GL.Vertex3(x1, y, z0); GL.Vertex3(x1, y, z1);
        GL.End();

        if (hoveredCell.x >= 0 && hoveredCell.y >= 0)
            DrawCellQuad(hoveredCell, hoveredCellValid ? hoverValidColor : hoverInvalidColor, y + 0.01f);

        if (showOccupiedCells)
        {
            for (int x = 0; x < w; x++)
                for (int z = 0; z < h; z++)
                {
                    var cell = gridManager.GetCell(new Vector2Int(x, z));
                    if (cell != null && cell.State == CellState.Occupied)
                        DrawCellQuad(new Vector2Int(x, z), occupiedColor, y + 0.005f);
                }
        }
        GL.PopMatrix();
    }

    void DrawCellQuad(Vector2Int gp, Color color, float y)
    {
        float cs = gridManager.CellSize;
        Vector3 o = gridManager.GridOrigin;
        float x0 = o.x + gp.x * cs, z0 = o.z + gp.y * cs;
        float x1 = x0 + cs, z1 = z0 + cs;
        GL.Begin(GL.QUADS);
        GL.Color(color);
        GL.Vertex3(x0, y, z0); GL.Vertex3(x0, y, z1); GL.Vertex3(x1, y, z1); GL.Vertex3(x1, y, z0);
        GL.End();
    }

    public void ToggleGrid() { gridVisible = !gridVisible; }
    public void SetGridVisible(bool v) { gridVisible = v; }
}