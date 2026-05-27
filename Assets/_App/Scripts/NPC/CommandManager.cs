using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// プレイヤーの入力を処理し、現在のPlayerModeに応じてタスク登録やキャンセルを行う。
/// - Gathering モード: 左クリックで木を選択 → TaskManager にタスク登録
///                     左ドラッグで範囲選択（地面に投影） → 範囲内の木をまとめてタスク登録
/// - Cancelling モード: 左クリックで対象を選択 → TaskManager でキャンセル
///                      左ドラッグで範囲選択（地面に投影） → 範囲内の対象をまとめてキャンセル
/// - 右クリック（どのモードでも）: モードを Normal に戻す
/// </summary>
public class CommandManager : MonoBehaviour
{
    public static CommandManager Instance { get; private set; }

    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask resourceLayer; // ResourceNode用のレイヤー

    [Header("Drag Selection")]
    [SerializeField] private float dragThreshold = 0.5f; // ワールド座標でのドラッグ判定距離

    private Camera mainCamera;

    // ドラッグ範囲選択用
    private bool isMouseDown = false;
    private bool isDragging = false;
    private Vector3 dragStartWorldPos;
    private Vector3 dragCurrentWorldPos;

    // ワールド空間描画用のオブジェクト
    private GameObject selectionAreaObject;
    private MeshRenderer selectionRenderer;
    private LineRenderer borderRenderer;

    private Material gatherMaterial;
    private Material cancelMaterial;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        CreateSelectionVisuals();
    }

    void Start()
    {
        mainCamera = Camera.main;
    }

    private void CreateSelectionVisuals()
    {
        // Unlit/Transparent をサポートする標準シェーダー
        Shader shader = Shader.Find("Sprites/Default");

        gatherMaterial = new Material(shader);
        gatherMaterial.color = new Color(0.2f, 0.8f, 0.2f, 0.3f); // 半透明の緑

        cancelMaterial = new Material(shader);
        cancelMaterial.color = new Color(1f, 0.2f, 0.2f, 0.3f); // 半透明の赤

        // 塗りつぶし用（地面に張り付く板）
        selectionAreaObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        selectionAreaObject.name = "SelectionAreaProjector";
        Destroy(selectionAreaObject.GetComponent<Collider>()); // 衝突判定は不要

        selectionRenderer = selectionAreaObject.GetComponent<MeshRenderer>();
        selectionRenderer.material = gatherMaterial;
        // シャドウなどを無効化
        selectionRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        selectionRenderer.receiveShadows = false;

        // 枠線用（LineRenderer）
        borderRenderer = selectionAreaObject.AddComponent<LineRenderer>();
        borderRenderer.useWorldSpace = true;
        borderRenderer.loop = true;
        borderRenderer.positionCount = 4;
        borderRenderer.startWidth = 0.05f;
        borderRenderer.endWidth = 0.05f;
        borderRenderer.material = new Material(shader); // Unlit
        // 影の影響を受けないように
        borderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        borderRenderer.receiveShadows = false;

        selectionAreaObject.SetActive(false);
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
                CancelDrag();
                GameManager.Instance.SetPlayerMode(PlayerMode.Normal);
                Debug.Log("[CommandManager] モード解除 → Normal");
                return;
            }
        }

        // Gathering または Cancelling モードではドラッグ範囲選択を処理
        if (mode == PlayerMode.Gathering || mode == PlayerMode.Cancelling)
        {
            HandleDragSelectionInput(mode);
            return;
        }

        // 左クリック: その他のモード
        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            // Normal モードでは何もしない（SelectionManagerがNPC選択を処理）
        }
    }

    // ==================== Mode Input (Click + Drag) ====================

    private void HandleDragSelectionInput(PlayerMode mode)
    {
        // UI上なら無視
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            // すでにドラッグ中の場合は、途中でUIの上に行ってもドラッグ継続
            if (!isMouseDown) return;
        }

        // マウスダウン: 地面へのレイキャストで開始位置を記録
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
            {
                isMouseDown = true;
                isDragging = false;
                dragStartWorldPos = hit.point;
                dragCurrentWorldPos = dragStartWorldPos;
            }
        }

        // マウスドラッグ中: 地面へのレイキャストで現在位置を更新し描画
        if (isMouseDown && Input.GetMouseButton(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
            {
                dragCurrentWorldPos = hit.point;

                // ワールド空間の距離でドラッグ開始を判定
                if (!isDragging && Vector3.Distance(dragStartWorldPos, dragCurrentWorldPos) > dragThreshold)
                {
                    isDragging = true;
                    selectionAreaObject.SetActive(true);
                }

                if (isDragging)
                {
                    UpdateSelectionVisuals(mode);
                }
            }
        }

        // マウスアップ: アクション実行
        if (isMouseDown && Input.GetMouseButtonUp(0))
        {
            if (isDragging)
            {
                if (mode == PlayerMode.Gathering) HandleGatheringDragSelect();
                else if (mode == PlayerMode.Cancelling) HandleCancellingDragSelect();
            }
            else
            {
                // 単発クリックは従来のレイキャスト（クリックしたオブジェクトを直接判定）
                if (mode == PlayerMode.Gathering) HandleGatheringClick();
                else if (mode == PlayerMode.Cancelling) HandleCancellingClick();
            }

            CancelDrag();
        }
    }

    /// <summary>
    /// 地面上の3Dドラッグ矩形の見た目と位置を更新する
    /// </summary>
    private void UpdateSelectionVisuals(PlayerMode mode)
    {
        float minX = Mathf.Min(dragStartWorldPos.x, dragCurrentWorldPos.x);
        float maxX = Mathf.Max(dragStartWorldPos.x, dragCurrentWorldPos.x);
        float minZ = Mathf.Min(dragStartWorldPos.z, dragCurrentWorldPos.z);
        float maxZ = Mathf.Max(dragStartWorldPos.z, dragCurrentWorldPos.z);

        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;
        
        float centerX = minX + sizeX / 2f;
        float centerZ = minZ + sizeZ / 2f;

        // Cubeを描画（厚みを薄くして地面の少し上に配置）
        float yPos = dragStartWorldPos.y + 0.05f; // 起伏の少ない地形を想定
        selectionAreaObject.transform.position = new Vector3(centerX, yPos, centerZ);
        selectionAreaObject.transform.localScale = new Vector3(sizeX, 0.01f, sizeZ);

        // マテリアルと色の切り替え
        selectionRenderer.material = (mode == PlayerMode.Gathering) ? gatherMaterial : cancelMaterial;
        
        Color borderColor = (mode == PlayerMode.Gathering) ? new Color(0.3f, 1f, 0.3f, 1f) : new Color(1f, 0.3f, 0.3f, 1f);
        borderRenderer.startColor = borderColor;
        borderRenderer.endColor = borderColor;

        // LineRendererで外枠を描画（塗りつぶしよりさらに少し上に浮かせる）
        float lineY = yPos + 0.01f;
        borderRenderer.SetPosition(0, new Vector3(minX, lineY, minZ));
        borderRenderer.SetPosition(1, new Vector3(minX, lineY, maxZ));
        borderRenderer.SetPosition(2, new Vector3(maxX, lineY, maxZ));
        borderRenderer.SetPosition(3, new Vector3(maxX, lineY, minZ));
    }

    private void CancelDrag()
    {
        isMouseDown = false;
        isDragging = false;
        if (selectionAreaObject != null)
        {
            selectionAreaObject.SetActive(false);
        }
    }

    // ==================== Action Executions ====================

    /// <summary>
    /// ドラッグ範囲（ワールドX/Z）内の木をまとめてタスク登録する。
    /// </summary>
    private void HandleGatheringDragSelect()
    {
        if (TaskManager.Instance == null) return;

        float minX = Mathf.Min(dragStartWorldPos.x, dragCurrentWorldPos.x);
        float maxX = Mathf.Max(dragStartWorldPos.x, dragCurrentWorldPos.x);
        float minZ = Mathf.Min(dragStartWorldPos.z, dragCurrentWorldPos.z);
        float maxZ = Mathf.Max(dragStartWorldPos.z, dragCurrentWorldPos.z);

        ResourceNode[] allNodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        int registeredCount = 0;

        foreach (ResourceNode node in allNodes)
        {
            if (node == null || !node.HasResources || node.Type != ResourceType.Wood) continue;

            Vector3 pos = node.transform.position;
            // ワールドのX, Z座標で範囲内か判定
            if (pos.x >= minX && pos.x <= maxX && pos.z >= minZ && pos.z <= maxZ)
            {
                if (TaskManager.Instance.RegisterGatherTask(node))
                {
                    registeredCount++;
                }
            }
        }

        // Terrain Trees
        if (TerrainTreeInteractManager.Instance != null && VoxelWorld.Instance != null)
        {
            List<int> treeIndices = TerrainTreeInteractManager.Instance.GetTreesInRect(minX, maxX, minZ, maxZ);
            // 配列の要素削除によるズレを防ぐため、降順にソートして処理する
            treeIndices.Sort((a, b) => b.CompareTo(a));
            foreach (int index in treeIndices)
            {
                GameObject go = VoxelWorld.Instance.ConvertTerrainTreeToGameObject(index);
                if (go != null)
                {
                    ResourceNode node = go.GetComponent<ResourceNode>();
                    if (node != null && TaskManager.Instance.RegisterGatherTask(node))
                    {
                        registeredCount++;
                    }
                }
            }
        }

        if (registeredCount > 0)
        {
            Debug.Log($"[CommandManager] ワールドドラッグ範囲選択: {registeredCount}本の木をタスク登録");
        }
        else
        {
            Debug.Log("[CommandManager] ワールドドラッグ範囲選択: 範囲内に有効な木がありません");
        }
    }

    /// <summary>
    /// ドラッグ範囲（ワールドX/Z）内のタスクをまとめてキャンセルする。
    /// </summary>
    private void HandleCancellingDragSelect()
    {
        if (TaskManager.Instance == null) return;

        float minX = Mathf.Min(dragStartWorldPos.x, dragCurrentWorldPos.x);
        float maxX = Mathf.Max(dragStartWorldPos.x, dragCurrentWorldPos.x);
        float minZ = Mathf.Min(dragStartWorldPos.z, dragCurrentWorldPos.z);
        float maxZ = Mathf.Max(dragStartWorldPos.z, dragCurrentWorldPos.z);

        ResourceNode[] allNodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        int cancelledCount = 0;

        foreach (ResourceNode node in allNodes)
        {
            if (node == null) continue;

            Vector3 pos = node.transform.position;
            if (pos.x >= minX && pos.x <= maxX && pos.z >= minZ && pos.z <= maxZ)
            {
                if (TaskManager.Instance.CancelGatherTask(node))
                {
                    cancelledCount++;
                }
            }
        }

        if (cancelledCount > 0)
        {
            Debug.Log($"[CommandManager] ワールドドラッグキャンセル: {cancelledCount}個のタスクをキャンセル");
        }
        else
        {
            Debug.Log("[CommandManager] ワールドドラッグキャンセル: 範囲内に対象がありません");
        }
    }

    /// <summary>
    /// 伐採モード: クリックした木をタスクに登録する
    /// </summary>
    private void HandleGatheringClick()
    {
        // 1. Terrain Tree の判定
        if (TerrainTreeInteractManager.Instance != null && VoxelWorld.Instance != null)
        {
            int treeIndex = TerrainTreeInteractManager.Instance.GetHoveredTreeIndex();
            if (treeIndex != -1)
            {
                GameObject go = VoxelWorld.Instance.ConvertTerrainTreeToGameObject(treeIndex);
                if (go != null)
                {
                    ResourceNode tNode = go.GetComponent<ResourceNode>();
                    if (tNode != null && tNode.HasResources && tNode.Type == ResourceType.Wood)
                    {
                        TaskManager.Instance?.RegisterGatherTask(tNode);
                        return;
                    }
                }
            }
        }

        // 2. 既存の ResourceNode の判定
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, resourceLayer))
        {
            ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
            if (node != null && node.HasResources && node.Type == ResourceType.Wood)
            {
                TaskManager.Instance?.RegisterGatherTask(node);
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
                TaskManager.Instance?.CancelGatherTask(node);
                return;
            }
        }
        
        GameManager.Instance.SetPlayerMode(PlayerMode.Normal);
        Debug.Log("[CommandManager] キャンセルモード: 何もない場所 → Normal に戻る");
    }
}