using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(GridManager))]
public class GridVisualizer : MonoBehaviour
{
    [Header("Grid Line Settings")]
    [SerializeField] private Color gridLineColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color gridBorderColor = new Color(1f, 0.8f, 0f, 0.6f);
    [SerializeField] private float lineHeightOffset = 0.05f;

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

    void OnEnable()
    {
        // URPではOnRenderObjectが呼ばれないため、endCameraRenderingを使用
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
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

    /// <summary>
    /// URP対応: endCameraRenderingコールバックでGL描画
    /// </summary>
    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        // GameViewのメインカメラのみ描画
        if (camera != Camera.main) return;
        DrawGrid();
    }

    /// <summary>
    /// Built-in RP用フォールバック
    /// </summary>
    void OnRenderObject()
    {
        // URPではendCameraRenderingが呼ばれるので、BuiltIn RP時のフォールバック
        if (GraphicsSettings.currentRenderPipeline != null) return;
        DrawGrid();
    }

    /// <summary>
    /// 指定XZグリッド座標の地表面Y座標を取得。
    /// </summary>
    float GetSurfaceY(int gridX, int gridZ)
    {
        float csX = gridManager.CellSizeX;
        float csZ = gridManager.CellSizeZ;
        Vector3 o = gridManager.GridOrigin;
        float worldX = o.x + gridX * csX + csX * 0.5f;
        float worldZ = o.z + gridZ * csZ + csZ * 0.5f;

        if (VoxelWorld.Instance != null)
        {
            return VoxelWorld.Instance.GetSurfaceWorldY(worldX, worldZ) + lineHeightOffset;
        }
        return o.y + lineHeightOffset;
    }

    void DrawGrid()
    {
        if (!gridVisible || lineMaterial == null || gridManager == null) return;
        lineMaterial.SetPass(0);
        int w = gridManager.GridWidth;
        int h = gridManager.GridHeight;
        float csX = gridManager.CellSizeX;
        float csZ = gridManager.CellSizeZ;
        Vector3 o = gridManager.GridOrigin;

        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);

        // グリッド線（地形の起伏に沿って描画）
        GL.Begin(GL.LINES);
        GL.Color(gridLineColor);

        // X方向の線（Z軸に沿って引く、各セグメントごとにY座標を変える）
        for (int x = 0; x <= w; x++)
        {
            float xp = o.x + x * csX;
            for (int z = 0; z < h; z++)
            {
                float z0 = o.z + z * csZ;
                float z1 = o.z + (z + 1) * csZ;
                // セグメント両端のY座標を周辺セルから取得
                float y0 = GetSurfaceYForVertex(x, z);
                float y1 = GetSurfaceYForVertex(x, z + 1);
                GL.Vertex3(xp, y0, z0);
                GL.Vertex3(xp, y1, z1);
            }
        }

        // Z方向の線（X軸に沿って引く）
        for (int z = 0; z <= h; z++)
        {
            float zp = o.z + z * csZ;
            for (int x = 0; x < w; x++)
            {
                float x0 = o.x + x * csX;
                float x1 = o.x + (x + 1) * csX;
                float y0 = GetSurfaceYForVertex(x, z);
                float y1 = GetSurfaceYForVertex(x + 1, z);
                GL.Vertex3(x0, y0, zp);
                GL.Vertex3(x1, y1, zp);
            }
        }
        GL.End();

        // 外枠（太線）
        GL.Begin(GL.LINES);
        GL.Color(gridBorderColor);
        // 外枠は四隅の代表Y座標で描画
        float borderY = GetSurfaceY(w / 2, h / 2);
        float bx0 = o.x, bx1 = o.x + w * csX, bz0 = o.z, bz1 = o.z + h * csZ;
        GL.Vertex3(bx0, borderY, bz0); GL.Vertex3(bx1, borderY, bz0);
        GL.Vertex3(bx0, borderY, bz1); GL.Vertex3(bx1, borderY, bz1);
        GL.Vertex3(bx0, borderY, bz0); GL.Vertex3(bx0, borderY, bz1);
        GL.Vertex3(bx1, borderY, bz0); GL.Vertex3(bx1, borderY, bz1);
        GL.End();

        // ホバーセル（地形に合わせたY座標）
        if (hoveredCell.x >= 0 && hoveredCell.y >= 0)
        {
            float hy = GetSurfaceY(hoveredCell.x, hoveredCell.y) + 0.01f;
            DrawCellQuad(hoveredCell, hoveredCellValid ? hoverValidColor : hoverInvalidColor, hy);
        }

        // 占有セル
        if (showOccupiedCells)
        {
            for (int x = 0; x < w; x++)
                for (int z = 0; z < h; z++)
                {
                    var cell = gridManager.GetCell(new Vector2Int(x, z));
                    if (cell != null && cell.State == CellState.Occupied)
                    {
                        float oy = GetSurfaceY(x, z) + 0.005f;
                        DrawCellQuad(new Vector2Int(x, z), occupiedColor, oy);
                    }
                }
        }
        GL.PopMatrix();
    }

    /// <summary>
    /// グリッドの頂点（交点）でのY座標を取得。
    /// 隣接する最大4セルの平均を使用して滑らかな線にする。
    /// </summary>
    float GetSurfaceYForVertex(int vertX, int vertZ)
    {
        float csX = gridManager.CellSizeX;
        float csZ = gridManager.CellSizeZ;
        Vector3 o = gridManager.GridOrigin;
        float worldX = o.x + vertX * csX;
        float worldZ = o.z + vertZ * csZ;

        if (VoxelWorld.Instance != null)
        {
            return VoxelWorld.Instance.GetSurfaceWorldY(worldX, worldZ) + lineHeightOffset;
        }
        return o.y + lineHeightOffset;
    }

    void DrawCellQuad(Vector2Int gp, Color color, float y)
    {
        float csX = gridManager.CellSizeX;
        float csZ = gridManager.CellSizeZ;
        Vector3 o = gridManager.GridOrigin;
        float x0 = o.x + gp.x * csX, z0 = o.z + gp.y * csZ;
        float x1 = x0 + csX, z1 = z0 + csZ;
        GL.Begin(GL.QUADS);
        GL.Color(color);
        GL.Vertex3(x0, y, z0); GL.Vertex3(x0, y, z1); GL.Vertex3(x1, y, z1); GL.Vertex3(x1, y, z0);
        GL.End();
    }

    public void ToggleGrid() { gridVisible = !gridVisible; }
    public void SetGridVisible(bool v) { gridVisible = v; }
}