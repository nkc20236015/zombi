using UnityEngine;

public class CommandManager : MonoBehaviour
{
    public static CommandManager Instance { get; private set; }

    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask resourceLayer; // ResourceNode用のレイヤー
    private Camera mainCamera;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Building)
            return; // 建築モード中は指示無効

        if (Input.GetMouseButtonDown(1)) // 右クリック
        {
            if (SelectionManager.Instance == null || SelectionManager.Instance.SelectedNPCs.Count == 0)
                return;

            HandleCommand();
        }
    }

    private void HandleCommand()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // まず ResourceNode を優先チェック
        if (Physics.Raycast(ray, out RaycastHit resourceHit, 200f, resourceLayer))
        {
            ResourceNode node = resourceHit.collider.GetComponentInParent<ResourceNode>();
            if (node != null && node.HasResources)
            {
                IssueGatherCommand(node);
                Debug.Log($"Issued Gather Command to {SelectionManager.Instance.SelectedNPCs.Count} NPCs → {node.gameObject.name} ({node.Type})");
                return;
            }
        }
        
        // 地面への移動指示
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
        {
            IssueMoveCommand(hit.point);
            Debug.Log($"Issued Move Command to {SelectionManager.Instance.SelectedNPCs.Count} NPCs at {hit.point}");
        }
    }

    private void IssueMoveCommand(Vector3 targetPosition)
    {
        foreach (var npc in SelectionManager.Instance.SelectedNPCs)
        {
            if (npc != null)
            {
                npc.MoveTo(targetPosition);
            }
        }
    }

    private void IssueGatherCommand(ResourceNode node)
    {
        foreach (var npc in SelectionManager.Instance.SelectedNPCs)
        {
            if (npc != null)
            {
                npc.GatherResource(node);
            }
        }
    }
}
