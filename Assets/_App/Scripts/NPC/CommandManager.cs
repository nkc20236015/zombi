using UnityEngine;

/// <summary>
/// プレイヤーの入力を処理し、現在のPlayerModeに応じてタスク登録やキャンセルを行う。
/// - Gathering モード: 左クリックで木を選択 → TaskManager にタスク登録
/// - Cancelling モード: 左クリックで対象を選択 → TaskManager でキャンセル
/// - 右クリック（どのモードでも）: モードを Normal に戻す
/// </summary>
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
        if (GameManager.Instance == null) return;

        PlayerMode mode = GameManager.Instance.CurrentPlayerMode;

        // 建築モード中は何もしない
        if (mode == PlayerMode.Building) return;

        // 右クリック: Gathering/Cancelling モードを解除して Normal に戻す
        if (Input.GetMouseButtonDown(1))
        {
            if (mode == PlayerMode.Gathering || mode == PlayerMode.Cancelling)
            {
                GameManager.Instance.SetPlayerMode(PlayerMode.Normal);
                Debug.Log("[CommandManager] モード解除 → Normal");
                return;
            }
        }

        // 左クリック: モードに応じた処理
        if (Input.GetMouseButtonDown(0))
        {
            // UIの上をクリックした場合は無視
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            switch (mode)
            {
                case PlayerMode.Gathering:
                    HandleGatheringClick();
                    break;
                case PlayerMode.Cancelling:
                    HandleCancellingClick();
                    break;
                // Normal モードでは何もしない（SelectionManagerがNPC選択を処理）
            }
        }
    }

    /// <summary>
    /// 伐採モード: クリックした木をタスクに登録する
    /// </summary>
    private void HandleGatheringClick()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, resourceLayer))
        {
            ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
            if (node != null && node.HasResources && node.Type == ResourceType.Wood)
            {
                if (TaskManager.Instance != null)
                {
                    TaskManager.Instance.RegisterGatherTask(node);
                }
                return;
            }
        }

        Debug.Log("[CommandManager] 伐採モード: 有効な木が見つかりません");
    }

    /// <summary>
    /// キャンセルモード: クリックした対象のタスクをキャンセルする
    /// </summary>
    private void HandleCancellingClick()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, resourceLayer))
        {
            ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                if (TaskManager.Instance != null)
                {
                    TaskManager.Instance.CancelGatherTask(node);
                }
                return;
            }
        }

        // 何もない場所をクリックした場合 → モード解除
        GameManager.Instance.SetPlayerMode(PlayerMode.Normal);
        Debug.Log("[CommandManager] キャンセルモード: 何もない場所 → Normal に戻る");
    }
}