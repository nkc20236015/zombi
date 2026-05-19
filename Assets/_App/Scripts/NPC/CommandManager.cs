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

    [Header("Selection Box")]
    [SerializeField] private Color selectionBoxColor = new Color(0.5f, 1f, 0.5f, 0.2f);
    [SerializeField] private Color selectionBoxBorderColor = new Color(0.5f, 1f, 0.5f, 1f);
    private Vector2 startMousePosition;
    private bool isDragging = false;
    private Texture2D whiteTexture;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        mainCamera = Camera.main;

        whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
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

            if (mode == PlayerMode.Gathering || mode == PlayerMode.Cancelling)
            {
                startMousePosition = Input.mousePosition;
                isDragging = true;
            }
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            Vector2 endMousePosition = Input.mousePosition;

            if (Vector2.Distance(startMousePosition, endMousePosition) < 10f)
            {
                // 単体クリック
                if (mode == PlayerMode.Gathering) HandleGatheringClick();
                else if (mode == PlayerMode.Cancelling) HandleCancellingClick();
            }
            else
            {
                // ドラッグ（範囲選択）
                if (mode == PlayerMode.Gathering) HandleGatheringDrag(startMousePosition, endMousePosition);
                else if (mode == PlayerMode.Cancelling) HandleCancellingDrag(startMousePosition, endMousePosition);
            }
        }
    }

    void OnGUI()
    {
        if (isDragging && (GameManager.Instance.CurrentPlayerMode == PlayerMode.Gathering || GameManager.Instance.CurrentPlayerMode == PlayerMode.Cancelling))
        {
            // GUIのY座標は上が0になるため反転
            var rect = GetScreenRect(startMousePosition, Input.mousePosition);
            DrawScreenRect(rect, selectionBoxColor);
            DrawScreenRectBorder(rect, 2, selectionBoxBorderColor);
        }
    }

    private Rect GetScreenRect(Vector2 screenPosition1, Vector2 screenPosition2)
    {
        // GUI座標系に変換
        screenPosition1.y = Screen.height - screenPosition1.y;
        screenPosition2.y = Screen.height - screenPosition2.y;

        var topLeft = Vector2.Min(screenPosition1, screenPosition2);
        var bottomRight = Vector2.Max(screenPosition1, screenPosition2);

        return Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
    }

    private void DrawScreenRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawScreenRectBorder(Rect rect, float thickness, Color color)
    {
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        DrawScreenRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        DrawScreenRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
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

    private void HandleGatheringDrag(Vector2 start, Vector2 end)
    {
        // 画面座標系 (左下原点) の矩形を作成
        Rect selectionRect = GetScreenRectStandard(start, end);

        ResourceNode[] allNodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        int addedCount = 0;

        foreach (var node in allNodes)
        {
            if (node.Type == ResourceType.Wood && node.HasResources)
            {
                Vector3 pivotScreenPos = mainCamera.WorldToScreenPoint(node.transform.position);
                
                Vector3 centerPos = node.transform.position;
                Collider col = node.GetComponentInChildren<Collider>();
                if (col != null) centerPos = col.bounds.center;
                Vector3 centerScreenPos = mainCamera.WorldToScreenPoint(centerPos);

                // z > 0 はカメラの前方にあることを意味する
                bool isInside = (pivotScreenPos.z > 0 && selectionRect.Contains(new Vector2(pivotScreenPos.x, pivotScreenPos.y))) ||
                                (centerScreenPos.z > 0 && selectionRect.Contains(new Vector2(centerScreenPos.x, centerScreenPos.y)));

                if (isInside)
                {
                    if (TaskManager.Instance != null)
                    {
                        TaskManager.Instance.RegisterGatherTask(node);
                        addedCount++;
                    }
                }
            }
        }

        Debug.Log($"[CommandManager] 伐採ドラッグ: {addedCount}件のタスクを追加");
    }

    private void HandleCancellingDrag(Vector2 start, Vector2 end)
    {
        Rect selectionRect = GetScreenRectStandard(start, end);

        ResourceNode[] allNodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        int canceledCount = 0;

        foreach (var node in allNodes)
        {
            Vector3 pivotScreenPos = mainCamera.WorldToScreenPoint(node.transform.position);
            
            Vector3 centerPos = node.transform.position;
            Collider col = node.GetComponentInChildren<Collider>();
            if (col != null) centerPos = col.bounds.center;
            Vector3 centerScreenPos = mainCamera.WorldToScreenPoint(centerPos);

            bool isInside = (pivotScreenPos.z > 0 && selectionRect.Contains(new Vector2(pivotScreenPos.x, pivotScreenPos.y))) ||
                            (centerScreenPos.z > 0 && selectionRect.Contains(new Vector2(centerScreenPos.x, centerScreenPos.y)));

            if (isInside)
            {
                if (TaskManager.Instance != null)
                {
                    // 内部でタスクが登録されているかチェックしてキャンセルする
                    TaskManager.Instance.CancelGatherTask(node);
                    canceledCount++;
                }
            }
        }

        Debug.Log($"[CommandManager] キャンセルドラッグ: {canceledCount}件のタスクをキャンセル");
    }

    private Rect GetScreenRectStandard(Vector2 p1, Vector2 p2)
    {
        // 左下原点の標準的なスクリーン座標Rect
        var topLeft = Vector2.Min(p1, p2);
        var bottomRight = Vector2.Max(p1, p2);
        return Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
    }
}
