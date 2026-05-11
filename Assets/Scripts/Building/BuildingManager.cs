using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    [Header("Building")]
    [SerializeField] private GameObject[] blockPrefabs;
    [SerializeField] private int selectedBlockIndex = 0;

    [Header("Preview")]
    [SerializeField] private Material previewValidMaterial;
    [SerializeField] private Material previewInvalidMaterial;

    private GridManager grid;
    private GameObject preview;
    private Vector2Int curGridPos;
    private bool canPlace;

    public bool IsBuilding => GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Building;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        grid = GridManager.Instance;
        if (GameManager.Instance != null) GameManager.Instance.OnPlayerModeChanged += OnModeChanged;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnPlayerModeChanged -= OnModeChanged;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab) && GameManager.Instance != null) GameManager.Instance.ToggleBuildMode();
        if (!IsBuilding) return;

        for (int i = 0; i < 9 && blockPrefabs != null && i < blockPrefabs.Length; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { selectedBlockIndex = i; DestroyPreview(); }

        UpdatePreview();
        if (Input.GetMouseButtonDown(0) && canPlace) PlaceBlock();
        if (Input.GetMouseButtonDown(1)) RemoveBlock();
    }

    void UpdatePreview()
    {
        if (grid == null || blockPrefabs == null || blockPrefabs.Length == 0) return;
        if (grid.TryGetGridPositionFromMouse(out Vector2Int gp))
        {
            curGridPos = gp;
            canPlace = grid.CanPlace(gp);
            if (preview == null)
            {
                preview = Instantiate(blockPrefabs[selectedBlockIndex]);
                foreach (var c in preview.GetComponentsInChildren<Collider>()) c.enabled = false;
            }

            // グリッド座標をワールド座標に変換（底面中央の座標が返る）
            Vector3 worldPos = grid.GridToWorld(gp);
            // ターゲットとなるセルの中央座標
            Vector3 targetCenter = new Vector3(worldPos.x, worldPos.y + VoxelData.BlockHeight * 0.5f, worldPos.z);

            AlignToCellCenter(preview, targetCenter);
            
            preview.SetActive(true);

            if (canPlace && previewValidMaterial != null)
                foreach (var r in preview.GetComponentsInChildren<Renderer>()) r.material = previewValidMaterial;
            else if (!canPlace && previewInvalidMaterial != null)
                foreach (var r in preview.GetComponentsInChildren<Renderer>()) r.material = previewInvalidMaterial;
        }
        else { canPlace = false; if (preview != null) preview.SetActive(false); }
    }

    void PlaceBlock()
    {
        Vector3 wp = grid.GridToWorld(curGridPos);
        Vector3 targetCenter = new Vector3(wp.x, wp.y + VoxelData.BlockHeight * 0.5f, wp.z);

        GameObject block = Instantiate(blockPrefabs[selectedBlockIndex], Vector3.zero, Quaternion.identity);

        // ブロックのスケールをVoxelWorldのブロックサイズに合わせる
        EnsureBlockScale(block);

        AlignToCellCenter(block, targetCenter);

        if (!grid.PlaceObject(curGridPos, block))
        {
            Destroy(block);
            return;
        }

        // NavMeshObstacleを追加してプレイヤーの通り抜けを防止
        if (block.GetComponent<UnityEngine.AI.NavMeshObstacle>() == null)
        {
            var obstacle = block.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            // ブロックのコライダーに合わせたサイズ
            var boxCol = block.GetComponent<BoxCollider>();
            if (boxCol != null)
            {
                obstacle.center = boxCol.center;
                obstacle.size = boxCol.size;
            }
            else
            {
                // VoxelWorldのブロックサイズに合わせたデフォルト
                obstacle.size = new Vector3(VoxelData.BlockWidth, VoxelData.BlockHeight, VoxelData.BlockDepth);
                obstacle.center = new Vector3(0, VoxelData.BlockHeight * 0.5f, 0);
            }
        }
    }

    /// <summary>
    /// 建築ブロックのスケールをVoxelWorldのブロックサイズに合わせる。
    /// </summary>
    void EnsureBlockScale(GameObject block)
    {
        Vector3 currentSize = Vector3.one;
        var col = block.GetComponentInChildren<BoxCollider>();
        if (col != null)
        {
            currentSize = Vector3.Scale(col.size, col.transform.lossyScale);
        }
        else
        {
            var renderers = block.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                currentSize = b.size;
            }
        }

        Vector3 targetSize = new Vector3(VoxelData.BlockWidth, VoxelData.BlockHeight, VoxelData.BlockDepth);
        if (currentSize.x > 0 && currentSize.y > 0 && currentSize.z > 0 && 
            (!Mathf.Approximately(currentSize.x, targetSize.x) ||
             !Mathf.Approximately(currentSize.y, targetSize.y) ||
             !Mathf.Approximately(currentSize.z, targetSize.z)))
        {
            Vector3 newScale = new Vector3(
                block.transform.localScale.x * (targetSize.x / currentSize.x),
                block.transform.localScale.y * (targetSize.y / currentSize.y),
                block.transform.localScale.z * (targetSize.z / currentSize.z)
            );
            block.transform.localScale = newScale;
        }
    }

    /// <summary>
    /// ブロックの見た目の中心を正確にターゲット座標に合わせる
    /// </summary>
    void AlignToCellCenter(GameObject block, Vector3 targetCenter)
    {
        var renderers = block.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            
            Vector3 offset = targetCenter - b.center;
            block.transform.position += offset;
        }
        else
        {
            block.transform.position = targetCenter;
        }
    }

    void RemoveBlock()
    {
        if (grid.TryGetGridPositionFromMouse(out Vector2Int gp))
        {
            var cell = grid.GetCell(gp);
            if (cell != null && cell.Occupant != null)
            {
                Destroy(cell.Occupant);
                grid.RemoveObject(gp);
            }
        }
    }

    void DestroyPreview() { if (preview != null) { Destroy(preview); preview = null; } }
    void OnModeChanged(PlayerMode m) { if (m != PlayerMode.Building) DestroyPreview(); }
}