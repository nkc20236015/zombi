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
            preview.transform.position = grid.GridToWorld(gp);
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
        GameObject block = Instantiate(blockPrefabs[selectedBlockIndex], wp, Quaternion.identity);
        if (!grid.PlaceObject(curGridPos, block)) Destroy(block);
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