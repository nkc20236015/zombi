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

    [Header("Zoning")]
    [SerializeField] private Color zoningPreviewColor = new Color(0.3f, 0.7f, 1f, 0.25f);
    [SerializeField] private Color zoningBorderColor = new Color(0.3f, 0.7f, 1f, 1f);

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
    private Material zoningMaterial;

    // Zoning用のドラッグ
    private Vector2Int zoningGridStart;
    private Vector2Int zoningGridCurrent;
    private bool isZoningDrag = false;
    private GameObject zoningPreviewObject;
    private MeshRenderer zoningPreviewRenderer;
    private LineRenderer zoningBorderRenderer;

    // Zoning用ホバープレビュー
    private GameObject zoningHoverObject;
    private MeshRenderer zoningHoverRenderer;
    private LineRenderer zoningHoverBorderRenderer;

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

        zoningMaterial = new Material(shader);
        zoningMaterial.color = zoningPreviewColor;

        // 塗りつぶし用（地面に張り付く板）
        selectionAreaObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        selectionAreaObject.name = "SelectionAreaPreview";
        selectionAreaObject.transform.parent = transform;
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

        // Zoning用のプレビューオブジェクト
        zoningPreviewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zoningPreviewObject.name = "ZoningPreview";
        zoningPreviewObject.transform.parent = transform;
        Destroy(zoningPreviewObject.GetComponent<Collider>());

        zoningPreviewRenderer = zoningPreviewObject.GetComponent<MeshRenderer>();
        zoningPreviewRenderer.material = zoningMaterial;
        zoningPreviewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        zoningPreviewRenderer.receiveShadows = false;

        zoningBorderRenderer = zoningPreviewObject.AddComponent<LineRenderer>();
        zoningBorderRenderer.useWorldSpace = true;
        zoningBorderRenderer.loop = true;
        zoningBorderRenderer.positionCount = 4;
        zoningBorderRenderer.startWidth = 0.06f;
        zoningBorderRenderer.endWidth = 0.06f;
        zoningBorderRenderer.material = new Material(shader);
        zoningBorderRenderer.startColor = zoningBorderColor;
        zoningBorderRenderer.endColor = zoningBorderColor;
        zoningBorderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        zoningBorderRenderer.receiveShadows = false;

        zoningPreviewObject.SetActive(false);

        // Zoning用ホバープレビューオブジェクト
        zoningHoverObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zoningHoverObject.name = "ZoningHoverPreview";
        zoningHoverObject.transform.parent = transform;
        Destroy(zoningHoverObject.GetComponent<Collider>());

        zoningHoverRenderer = zoningHoverObject.GetComponent<MeshRenderer>();
        zoningHoverRenderer.material = zoningMaterial;
        zoningHoverRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        zoningHoverRenderer.receiveShadows = false;

        zoningHoverBorderRenderer = zoningHoverObject.AddComponent<LineRenderer>();
        zoningHoverBorderRenderer.useWorldSpace = true;
        zoningHoverBorderRenderer.loop = true;
        zoningHoverBorderRenderer.positionCount = 4;
        zoningHoverBorderRenderer.startWidth = 0.04f;
        zoningHoverBorderRenderer.endWidth = 0.04f;
        zoningHoverBorderRenderer.material = new Material(shader);
        zoningHoverBorderRenderer.startColor = new Color(0.5f, 0.8f, 1f, 0.8f);
        zoningHoverBorderRenderer.endColor = new Color(0.5f, 0.8f, 1f, 0.8f);
        zoningHoverBorderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        zoningHoverBorderRenderer.receiveShadows = false;

        zoningHoverObject.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        PlayerMode mode = GameManager.Instance.CurrentPlayerMode;

        // 建築モード中は何もしない
        if (mode == PlayerMode.Building) return;

        // 右クリック: Gathering/Cancelling/StockpileZoning/Cutting/Picking モードを解除して Normal に戻す
        if (Input.GetMouseButtonDown(1))
        {
            if (mode == PlayerMode.Gathering || mode == PlayerMode.Cancelling || mode == PlayerMode.StockpileZoning || mode == PlayerMode.Cutting || mode == PlayerMode.Picking)
            {
                CancelDrag();
                CancelZoningDrag();
                GameManager.Instance.SetPlayerMode(PlayerMode.Normal);
                Debug.Log("[CommandManager] モード解除 → Normal");
                return;
            }
        }

        // StockpileZoning モードではグリッドスナップ選択を処理
        if (mode == PlayerMode.StockpileZoning)
        {
            HandleZoningInput();
            UpdateZoningHover();
            return;
        }
        else
        {
            if (zoningHoverObject != null && zoningHoverObject.activeSelf)
            {
                zoningHoverObject.SetActive(false);
            }
        }

        // Gathering, Cutting, Picking または Cancelling モードではドラッグ範囲選択を処理
        if (mode == PlayerMode.Gathering || mode == PlayerMode.Cutting || mode == PlayerMode.Picking || mode == PlayerMode.Cancelling)
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
                if (mode == PlayerMode.Gathering || mode == PlayerMode.Cutting || mode == PlayerMode.Picking) HandleGatheringDragSelect(mode);
                else if (mode == PlayerMode.Cancelling) HandleCancellingDragSelect();
            }
            else
            {
                // 単発クリックは従来のレイキャスト（クリックしたオブジェクトを直接判定）
                if (mode == PlayerMode.Gathering || mode == PlayerMode.Cutting || mode == PlayerMode.Picking) HandleGatheringClick(mode);
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
        bool isGatherType = (mode == PlayerMode.Gathering || mode == PlayerMode.Cutting || mode == PlayerMode.Picking);
        selectionRenderer.material = isGatherType ? gatherMaterial : cancelMaterial;
        
        Color borderColor = isGatherType ? new Color(0.3f, 1f, 0.3f, 1f) : new Color(1f, 0.3f, 0.3f, 1f);
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

    // ==================== Stockpile Zoning Input ====================

    /// <summary>
    /// ホバー時の1マスプレビュー表示
    /// </summary>
    private void UpdateZoningHover()
    {
        // UI上またはドラッグ中は非表示
        if ((UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) || isZoningDrag)
        {
            zoningHoverObject.SetActive(false);
            return;
        }

        if (GridManager.Instance == null) return;

        if (GridManager.Instance.TryGetGridPositionFromMouse(out Vector2Int gridPos))
        {
            zoningHoverObject.SetActive(true);

            float cellSizeX = GridManager.Instance.CellSizeX;
            float cellSizeZ = GridManager.Instance.CellSizeZ;
            Vector3 origin = GridManager.Instance.GridOrigin;

            float worldMinX = gridPos.x * cellSizeX + origin.x;
            float worldMaxX = (gridPos.x + 1) * cellSizeX + origin.x;
            float worldMinZ = gridPos.y * cellSizeZ + origin.z;
            float worldMaxZ = (gridPos.y + 1) * cellSizeZ + origin.z;

            float sizeX = worldMaxX - worldMinX;
            float sizeZ = worldMaxZ - worldMinZ;
            float centerX = worldMinX + sizeX / 2f;
            float centerZ = worldMinZ + sizeZ / 2f;

            float yPos = 0.05f;
            if (VoxelWorld.Instance != null)
            {
                yPos = VoxelWorld.Instance.GetSurfaceWorldY(centerX, centerZ) + 0.05f;
            }

            zoningHoverObject.transform.position = new Vector3(centerX, yPos, centerZ);
            zoningHoverObject.transform.localScale = new Vector3(sizeX, 0.01f, sizeZ);

            float lineY = yPos + 0.02f;
            zoningHoverBorderRenderer.SetPosition(0, new Vector3(worldMinX, lineY, worldMinZ));
            zoningHoverBorderRenderer.SetPosition(1, new Vector3(worldMinX, lineY, worldMaxZ));
            zoningHoverBorderRenderer.SetPosition(2, new Vector3(worldMaxX, lineY, worldMaxZ));
            zoningHoverBorderRenderer.SetPosition(3, new Vector3(worldMaxX, lineY, worldMinZ));
        }
        else
        {
            zoningHoverObject.SetActive(false);
        }
    }

    /// <summary>
    /// 備蓄場作成モードのドラッグ入力処理。マス目にスナップする。
    /// </summary>
    private void HandleZoningInput()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            if (!isZoningDrag) return;
        }

        if (GridManager.Instance == null) return;

        // マウスダウン: グリッド開始位置を記録
        if (Input.GetMouseButtonDown(0))
        {
            if (GridManager.Instance.TryGetGridPositionFromMouse(out Vector2Int gridPos))
            {
                isZoningDrag = true;
                zoningGridStart = gridPos;
                zoningGridCurrent = gridPos;
                zoningPreviewObject.SetActive(true);
                UpdateZoningPreview();
            }
        }

        // マウスドラッグ中: グリッド現在位置を更新
        if (isZoningDrag && Input.GetMouseButton(0))
        {
            if (GridManager.Instance.TryGetGridPositionFromMouse(out Vector2Int gridPos))
            {
                zoningGridCurrent = gridPos;
                UpdateZoningPreview();
            }
        }

        // マウスアップ: 備蓄場を作成
        if (isZoningDrag && Input.GetMouseButtonUp(0))
        {
            CreateStockpileFromDrag();
            CancelZoningDrag();
        }
    }

    /// <summary>
    /// ドラッグ中の備蓄場プレビューを更新（マス目にスナップ）
    /// </summary>
    private void UpdateZoningPreview()
    {
        if (GridManager.Instance == null) return;

        int minX = Mathf.Min(zoningGridStart.x, zoningGridCurrent.x);
        int maxX = Mathf.Max(zoningGridStart.x, zoningGridCurrent.x);
        int minZ = Mathf.Min(zoningGridStart.y, zoningGridCurrent.y);
        int maxZ = Mathf.Max(zoningGridStart.y, zoningGridCurrent.y);

        float cellSizeX = GridManager.Instance.CellSizeX;
        float cellSizeZ = GridManager.Instance.CellSizeZ;
        Vector3 origin = GridManager.Instance.GridOrigin;

        // エリア内に建築物や木がないかチェック
        bool isValid = true;
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector2Int pos = new Vector2Int(x, z);
                GridCell cell = GridManager.Instance.GetCell(pos);
                if (cell != null && cell.State != CellState.Empty) isValid = false;
                if (VoxelWorld.Instance != null && VoxelWorld.Instance.HasTreeAt(pos)) isValid = false;
            }
        }

        // ワールド座標に変換（グリッドの辺にスナップ）
        float worldMinX = minX * cellSizeX + origin.x;
        float worldMaxX = (maxX + 1) * cellSizeX + origin.x;
        float worldMinZ = minZ * cellSizeZ + origin.z;
        float worldMaxZ = (maxZ + 1) * cellSizeZ + origin.z;

        float sizeX = worldMaxX - worldMinX;
        float sizeZ = worldMaxZ - worldMinZ;
        float centerX = worldMinX + sizeX / 2f;
        float centerZ = worldMinZ + sizeZ / 2f;

        float yPos = 0.06f;
        if (VoxelWorld.Instance != null)
        {
            yPos = VoxelWorld.Instance.GetSurfaceWorldY(centerX, centerZ) + 0.06f;
        }

        // 資源（キノコなど）が範囲内にないかチェック
        Vector3 checkCenter = new Vector3(centerX, yPos, centerZ);
        Vector3 checkExtents = new Vector3(sizeX / 2f, 5f, sizeZ / 2f); // 高さの余裕を持たせる
        if (Physics.CheckBox(checkCenter, checkExtents, Quaternion.identity, resourceLayer))
        {
            isValid = false;
        }

        zoningPreviewObject.transform.position = new Vector3(centerX, yPos, centerZ);
        zoningPreviewObject.transform.localScale = new Vector3(sizeX, 0.01f, sizeZ);

        // 枠線（ロープのプレビュー）
        float lineY = yPos + 0.02f;
        Color borderColor = isValid ? new Color(1f, 0.8f, 0.2f, 0.8f) : new Color(1f, 0.3f, 0.3f, 0.8f);
        zoningBorderRenderer.startColor = borderColor;
        zoningBorderRenderer.endColor = borderColor;
        
        zoningBorderRenderer.SetPosition(0, new Vector3(worldMinX, lineY, worldMinZ));
        zoningBorderRenderer.SetPosition(1, new Vector3(worldMinX, lineY, worldMaxZ));
        zoningBorderRenderer.SetPosition(2, new Vector3(worldMaxX, lineY, worldMaxZ));
        zoningBorderRenderer.SetPosition(3, new Vector3(worldMaxX, lineY, worldMinZ));
    }

    /// <summary>
    /// ドラッグ範囲から備蓄場を作成する
    /// </summary>
    private void CreateStockpileFromDrag()
    {
        if (StockpileManager.Instance == null) return;

        int minX = Mathf.Min(zoningGridStart.x, zoningGridCurrent.x);
        int maxX = Mathf.Max(zoningGridStart.x, zoningGridCurrent.x);
        int minZ = Mathf.Min(zoningGridStart.y, zoningGridCurrent.y);
        int maxZ = Mathf.Max(zoningGridStart.y, zoningGridCurrent.y);

        // エリア内に建築物や木がないか最終チェック
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector2Int pos = new Vector2Int(x, z);
                GridCell cell = GridManager.Instance.GetCell(pos);
                if (cell != null && cell.State != CellState.Empty) 
                {
                    Debug.LogWarning("[CommandManager] 建築物があるため備蓄場を作成できません");
                    return;
                }
                if (VoxelWorld.Instance != null && VoxelWorld.Instance.HasTreeAt(pos))
                {
                    Debug.LogWarning("[CommandManager] 木があるため備蓄場を作成できません");
                    return;
                }
            }
        }

        // 資源（キノコなど）の最終チェック
        float cellSizeX = GridManager.Instance.CellSizeX;
        float cellSizeZ = GridManager.Instance.CellSizeZ;
        Vector3 origin = GridManager.Instance.GridOrigin;

        float worldMinX = minX * cellSizeX + origin.x;
        float worldMaxX = (maxX + 1) * cellSizeX + origin.x;
        float worldMinZ = minZ * cellSizeZ + origin.z;
        float worldMaxZ = (maxZ + 1) * cellSizeZ + origin.z;

        float sizeX = worldMaxX - worldMinX;
        float sizeZ = worldMaxZ - worldMinZ;
        float centerX = worldMinX + sizeX / 2f;
        float centerZ = worldMinZ + sizeZ / 2f;

        float yPos = 0.06f;
        if (VoxelWorld.Instance != null)
        {
            yPos = VoxelWorld.Instance.GetSurfaceWorldY(centerX, centerZ) + 0.06f;
        }

        Vector3 checkCenter = new Vector3(centerX, yPos, centerZ);
        Vector3 checkExtents = new Vector3(sizeX / 2f, 5f, sizeZ / 2f);
        if (Physics.CheckBox(checkCenter, checkExtents, Quaternion.identity, resourceLayer))
        {
            Debug.LogWarning("[CommandManager] キノコや資源があるため備蓄場を作成できません");
            return;
        }

        Vector2Int gridMin = new Vector2Int(minX, minZ);
        Vector2Int gridMax = new Vector2Int(maxX, maxZ);

        StockpileZone zone = StockpileManager.Instance.CreateZone(gridMin, gridMax);
        if (zone != null)
        {
            Debug.Log($"[CommandManager] 備蓄場を作成しました: {zone.CellCount}マス");
        }
    }

    private void CancelZoningDrag()
    {
        isZoningDrag = false;
        if (zoningPreviewObject != null)
        {
            zoningPreviewObject.SetActive(false);
        }
    }

    // ==================== Action Executions ====================

    /// <summary>
    /// ドラッグ範囲（ワールドX/Z）内の木・資源をまとめてタスク登録する。
    /// </summary>
    private void HandleGatheringDragSelect(PlayerMode mode)
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
            if (node == null || !node.HasResources) continue;

            // モードによる対象のフィルタリング
            bool isTarget = false;
            if (mode == PlayerMode.Gathering && node.Type == ResourceType.Wood) isTarget = true;
            else if (mode == PlayerMode.Cutting && node.Type == ResourceType.Food && !node.IsRipe) isTarget = true;
            else if (mode == PlayerMode.Picking && node.Type == ResourceType.Food && node.IsRipe) isTarget = true;

            if (!isTarget) continue;

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
            Debug.Log($"[CommandManager] ワールドドラッグ範囲選択({mode}): {registeredCount}個の資源をタスク登録");
        }
        else
        {
            Debug.Log($"[CommandManager] ワールドドラッグ範囲選択({mode}): 範囲内に有効な資源がありません");
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
    /// 伐採・採取モード: クリックした資源をタスクに登録する
    /// </summary>
    private void HandleGatheringClick(PlayerMode mode)
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
                    if (tNode != null && tNode.HasResources)
                    {
                        bool isTarget = false;
                        if (mode == PlayerMode.Gathering && tNode.Type == ResourceType.Wood) isTarget = true;
                        else if (mode == PlayerMode.Cutting && tNode.Type == ResourceType.Food && !tNode.IsRipe) isTarget = true;
                        else if (mode == PlayerMode.Picking && tNode.Type == ResourceType.Food && tNode.IsRipe) isTarget = true;

                        if (isTarget)
                        {
                            TaskManager.Instance?.RegisterGatherTask(tNode);
                            return;
                        }
                    }
                }
            }
        }

        // 2. 既存の ResourceNode の判定
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, resourceLayer))
        {
            ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
            if (node != null && node.HasResources)
            {
                bool isTarget = false;
                if (mode == PlayerMode.Gathering && node.Type == ResourceType.Wood) isTarget = true;
                else if (mode == PlayerMode.Cutting && node.Type == ResourceType.Food && !node.IsRipe) isTarget = true;
                else if (mode == PlayerMode.Picking && node.Type == ResourceType.Food && node.IsRipe) isTarget = true;

                if (isTarget)
                {
                    TaskManager.Instance?.RegisterGatherTask(node);
                    return;
                }
            }
        }
        Debug.Log($"[CommandManager] モード({mode}): 有効な対象が見つかりません");
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