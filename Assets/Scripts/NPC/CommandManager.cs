using UnityEngine;

public class CommandManager : MonoBehaviour
{
    public static CommandManager Instance { get; private set; }

    [SerializeField] private LayerMask groundLayer;
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
        
        // とりあえず地面への移動指示のみ（Phase 2）
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
        {
            IssueMoveCommand(hit.point);
            
            // 指示が出たことを示す視覚効果（後で追加）
            Debug.Log($"Issued Move Command to {SelectionManager.Instance.SelectedNPCs.Count} NPCs at {hit.point}");
        }
    }

    private void IssueMoveCommand(Vector3 targetPosition)
    {
        // 複数人の場合、少しばらけさせるなどのフォーメーション処理を追加するとより良くなります
        // とりあえず全員同じ場所を目指す
        foreach (var npc in SelectionManager.Instance.SelectedNPCs)
        {
            if (npc != null)
            {
                npc.MoveTo(targetPosition);
            }
        }
    }
}
