using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCController : MonoBehaviour, ISelectable
{
    [Header("Visual Settings")]
    [SerializeField] private float buildModeAlpha = 0.3f;
    [SerializeField] private Color selectionColor = new Color(0.5f, 1f, 0.5f, 1f);

    [Header("Gathering Settings")]
    [SerializeField] private float gatherInterval = 2.0f; // 採取アニメーション間隔（秒）

    [Header("Hauling Settings")]
    [SerializeField] private int maxCarryAmount = 200; // 1回に運べる最大量
    [SerializeField] private float haulingCheckInterval = 3.0f; // 運搬可否チェック間隔

    [Header("Wander Settings")]
    [SerializeField] private float wanderRadius = 8.0f;
    [SerializeField] private float minWanderInterval = 10.0f;
    [SerializeField] private float maxWanderInterval = 20.0f;
    [SerializeField] private float wanderSpeedMultiplier = 0.4f; // うろちょろ時は歩くように遅くする
    private float wanderTimer;
    private float defaultMoveSpeed;
    private Vector3 homePosition; // NPCが初期化された場所、または最後に指示された場所をホームとするか等。今回は現在地ベースでランダム

    public NPCState CurrentState { get; private set; } = NPCState.Idle;
    public bool IsSelected { get; private set; }

    private NavMeshAgent agent;
    private Renderer[] modelRenderers;
    private Material[][] originalMaterials;
    private Material[][] ghostMaterials;
    private GameObject selectionRing;
    private NPCAnimationController animController;
    private NPCToolHolder toolHolder;

    [Header("Movement Marker")]
    [SerializeField] private GameObject targetMarkerPrefab;
    private GameObject targetMarkerInstance;

    // 採取関連
    private ResourceNode targetNode;
    private float gatherTimer;

    // ツール収納 → 移動のバッファ
    private Vector3? pendingMoveDestination;     // 収納後に移動する先
    private ResourceNode pendingGatherNode;      // 収納後に採取する先
    private float putAwayTimer;

    // 運搬関連
    private DroppedResource haulTarget;           // 拾いに行くアイテム
    private StockpileZone carryTargetZone;        // 運ぶ先の備蓄場
    private Vector2Int carryTargetCell;            // 運ぶ先のマス
    private ResourceType carryingResourceType;    // 運んでいる資源の種類
    private int carryingAmount;                   // 運んでいる量
    private float haulingCheckTimer;              // 運搬チェックタイマー

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animController = GetComponent<NPCAnimationController>();
        toolHolder = GetComponent<NPCToolHolder>();

#if UNITY_EDITOR
        if (targetMarkerPrefab == null)
        {
            targetMarkerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Map track markers VFX/Prefabs/Marker 6 Arrows Loop.prefab");
        }
#endif
    }

    void Start()
    {
        CacheRenderers();
        CreateSelectionRing();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerModeChanged += OnPlayerModeChanged;
            GameManager.Instance.RegisterNPC(this);
            SyncMode(GameManager.Instance.CurrentPlayerMode);
        }

        defaultMoveSpeed = agent.speed;

        // 初期ステートの設定
        SetState(NPCState.Idle);
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerModeChanged -= OnPlayerModeChanged;
            GameManager.Instance.UnregisterNPC(this);
        }
    }

    void Update()
    {
        switch (CurrentState)
        {
            case NPCState.Idle:
                UpdateIdleState();
                break;
            case NPCState.Wandering:
                UpdateWanderingState();
                break;
            case NPCState.Moving:
                UpdateMovingState();
                break;
            case NPCState.MovingToResource:
                UpdateMovingToResourceState();
                break;
            case NPCState.Gathering:
                UpdateGatheringState();
                break;
            case NPCState.PuttingAway:
                UpdatePuttingAwayState();
                break;
            case NPCState.Hauling:
                UpdateHaulingState();
                break;
            case NPCState.Carrying:
                UpdateCarryingState();
                break;
        }
    }

    // ==================== State Updates ====================

    private void UpdateIdleState()
    {
        if (!agent.isOnNavMesh) return;

        // 暇そうにするアニメーションが再生中なら、終わるまでうろちょろタイマーを進めない
        if (animController != null && animController.IsPlayingBoredIdle()) return;

        // 運搬チェック（Idle中に一定間隔で落ちているアイテムを探す）
        haulingCheckTimer -= Time.deltaTime;
        if (haulingCheckTimer <= 0f)
        {
            haulingCheckTimer = haulingCheckInterval;
            if (TryStartHauling()) return; // 運搬開始できたらIdleから抜ける
        }

        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0f)
        {
            // 徘徊先を決定
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection += transform.position;
            
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, 1))
            {
                agent.isStopped = false;
                agent.speed = defaultMoveSpeed * wanderSpeedMultiplier; // 歩行スピードにする
                agent.SetDestination(hit.position);
                SetState(NPCState.Wandering);
            }
            else
            {
                // NavMeshが見つからなかった場合は再度タイマーリセット
                wanderTimer = Random.Range(minWanderInterval, maxWanderInterval);
            }
        }
    }

    private void UpdateWanderingState()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                // 到着したらIdleに戻る
                SetState(NPCState.Idle);
            }
        }
    }

    private void UpdateMovingState()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                SetState(NPCState.Idle);
                HideMarker();
            }
        }
    }

    private void UpdateMovingToResourceState()
    {
        if (targetNode == null || !targetNode.HasResources)
        {
            // ターゲットが消失・枯渇した場合
            StopGathering();
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                // 実際の目的オブジェクトに十分に近づけたかをチェック（壁や障害物で遠くで止まった場合を防ぐ）
                float actualDistToTarget = Vector3.Distance(transform.position, targetNode.transform.position);
                if (actualDistToTarget > targetNode.InteractionRange + 1.5f)
                {
                    Debug.Log($"[NPCController] 到達不能: 目標に十分に近づけませんでした。（実際の距離: {actualDistToTarget}）");
                    StopGathering();
                    return;
                }

                // 壁越しにアクセスしていないかレイキャストでチェック（Line of Sight）
                Vector3 rayStart = transform.position + Vector3.up * 1.0f; // NPCの胸の高さ
                Vector3 targetPos = targetNode.transform.position + Vector3.up * 1.0f;
                Vector3 dir = targetPos - rayStart;
                float dist = dir.magnitude;

                // NPC自身のコライダーや他のトリガーなどを無視するため、全てのレイヤーを対象としつつチェック
                // （もし壁のみのレイヤーがある場合は LayerMask を設定するのが理想です）
                if (Physics.Raycast(rayStart, dir.normalized, out RaycastHit hit, dist - 0.1f)) // distより少し手前まで
                {
                    // 当たったものがターゲット自身やその子オブジェクト、NPC以外であれば障害物とみなす
                    if (hit.collider.transform != targetNode.transform && 
                        !hit.collider.transform.IsChildOf(targetNode.transform) &&
                        !hit.collider.CompareTag("NPC"))
                    {
                        Debug.Log($"[NPCController] 障害物に遮られてアクセスできません。 Hit: {hit.collider.gameObject.name}");
                        StopGathering();
                        return;
                    }
                }

                // 資源ノードに到着 → 採取開始
                StartGatheringAction();
            }
        }
    }

    private void UpdateGatheringState()
    {
        if (targetNode == null || !targetNode.HasResources)
        {
            // 資源が枯渇した
            StopGathering();
            return;
        }

        gatherTimer -= Time.deltaTime;
        if (gatherTimer <= 0f)
        {
            // 採取を1回実行
            int harvested = targetNode.Harvest();
            // 直接の加算処理を削除し、全て枯渇時のドロップ（ResourceNode.HarvestNonTree）に任せ、NPCが運搬するフローに一本化。

            // 再度タイマーリセットしてアニメーション再生
            if (targetNode != null && targetNode.HasResources)
            {
                gatherTimer = gatherInterval;
                PlayGatherAnimation();
            }
            else
            {
                StopGathering();
            }
        }
    }

    // ==================== Public Commands ====================

    public void MoveTo(Vector3 destination)
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        // BoredIdle中に指示が来た場合は即座に中断
        if (CurrentState == NPCState.Idle && animController != null && animController.IsPlayingBoredIdle())
        {
            animController.PlayIdle();
        }

        // 採取中の場合 → ツールをしまってから移動する
        if (CurrentState == NPCState.Gathering)
        {
            BeginPutAway(destination, null);
            return;
        }

        // PuttingAway中に新しい移動指示が来た場合 → 目的地だけ上書き
        if (CurrentState == NPCState.PuttingAway)
        {
            pendingMoveDestination = destination;
            pendingGatherNode = null;
            ShowMarker(destination);
            return;
        }

        // 通常の移動（MovingToResource中からの切替含む）
        if (CurrentState == NPCState.MovingToResource)
        {
            targetNode = null;
        }

        agent.isStopped = false;
        agent.speed = defaultMoveSpeed; // 通常スピードに戻す
        agent.SetDestination(destination);
        SetState(NPCState.Moving);
        ShowMarker(destination);
    }

    /// <summary>
    /// 指定した ResourceNode に向かって移動し、到着後に採取を開始する。
    /// </summary>
    public void GatherResource(ResourceNode node)
    {
        if (node == null || !node.HasResources) return;
        if (!agent.enabled || !agent.isOnNavMesh) return;

        // BoredIdle中に指示が来た場合は即座に中断
        if (CurrentState == NPCState.Idle && animController != null && animController.IsPlayingBoredIdle())
        {
            animController.PlayIdle();
        }

        // 採取中の場合 → ツールをしまってから次の採取対象へ移動
        if (CurrentState == NPCState.Gathering)
        {
            BeginPutAway(null, node);
            return;
        }

        // PuttingAway中に新しい採取指示が来た場合 → 対象を上書き
        if (CurrentState == NPCState.PuttingAway)
        {
            pendingMoveDestination = null;
            pendingGatherNode = node;
            return;
        }

        // 通常
        targetNode = node;
        Vector3 harvestPos = node.GetHarvestPosition(transform.position);
        agent.isStopped = false;
        agent.speed = defaultMoveSpeed; // 通常スピードに戻す
        agent.SetDestination(harvestPos);
        SetState(NPCState.MovingToResource);
        ShowMarker(harvestPos);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        UpdateVisuals();
    }

    // ==================== Gathering Helpers ====================

    private void StartGatheringAction()
    {
        agent.isStopped = true;
        HideMarker();
        SetState(NPCState.Gathering);

        // ノードの方を向く
        if (targetNode != null)
        {
            Vector3 lookDir = (targetNode.transform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }

            // ツールを手に表示
            if (toolHolder != null)
            {
                bool useTool = true;
                if (targetNode.Type == ResourceType.Food && targetNode.IsRipe)
                {
                    useTool = false; // 完熟時の採取は素手
                }
                toolHolder.ShowTool(targetNode.Type, useTool);
            }
        }

        gatherTimer = 1.5f; // TakeItemアニメーション(1秒)を待ってから最初の採取
        PlayGatherAnimation();
    }

    private void PlayGatherAnimation()
    {
        if (animController == null) return;

        int actionType = 0;
        if (targetNode != null)
        {
            switch (targetNode.Type)
            {
                case ResourceType.Wood:
                    actionType = 0; // Chop
                    break;
                case ResourceType.Stone:
                    actionType = 1; // Mine
                    break;
                case ResourceType.Food:
                    if (targetNode.IsRipe)
                    {
                        animController.PlayGather();
                    }
                    else
                    {
                        animController.PlaySickle(); // カマを使うアニメーション
                    }
                    return; // 専用トリガーを使ったのでここで終了
                default:
                    actionType = 0;
                    break;
            }
        }
        animController.PlayAction(actionType);
    }

    /// <summary>
    /// アニメーションのStrikeイベント（斧が当たった瞬間）を受けて、
    /// 採取中の木を揺らす。NPCAnimationControllerから呼ばれる。
    /// </summary>
    public void OnStrikeHit()
    {
        if (CurrentState == NPCState.Gathering && targetNode != null)
        {
            targetNode.OnStrikeHit();
        }
    }

    private void StopGathering()
    {
        targetNode = null;
        agent.isStopped = true;
        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
        if (animController != null) animController.PlayPutAway();

        // ツールをしまう時間を待つため、ステートをPuttingAwayにする
        SetState(NPCState.PuttingAway);
        putAwayTimer = 2.0f;

        HideMarker();

        // TaskManagerに完了報告（次のタスクの自動割り当てをトリガー）
        if (TaskManager.Instance != null)
        {
            TaskManager.Instance.ReportTaskComplete(this);
        }
    }

/// <summary>
    /// TaskManagerからのキャンセル指示。採取を中断してIdleに戻る。
    /// StopGathering とは異なり、TaskManagerへの報告は行わない（呼び出し元がTaskManagerのため）。
    /// ただし次のタスクへの自動割り当ては行う。
    /// </summary>
    public void CancelGathering()
    {
        targetNode = null;
        agent.isStopped = true;
        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
        if (animController != null) animController.PlayPutAway();
        
        SetState(NPCState.PuttingAway);
        putAwayTimer = 2.0f;
        
        HideMarker();
        pendingMoveDestination = null;
        pendingGatherNode = null;
    }


    private void StopGatheringImmediate()
    {
        if (CurrentState == NPCState.Gathering || CurrentState == NPCState.MovingToResource)
        {
            targetNode = null;
            if (animController != null) animController.StopAction();
            if (toolHolder != null) toolHolder.HideTool();
        }
    }

    // ==================== PuttingAway (ツール収納) ====================

    /// <summary>
    /// 採取を中断し、ツールをしまうアニメーションを再生してから次の行動へ移る。
    /// moveDestination と gatherNode のどちらか一方を指定する。
    /// </summary>
    private void BeginPutAway(Vector3? moveDestination, ResourceNode gatherNode)
    {
        // 採取アニメーションを停止し、ツールをしまうアニメーションを再生
        if (animController != null) animController.PlayPutAway();

        targetNode = null;
        agent.isStopped = true;
        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        // 次の行動をバッファ
        pendingMoveDestination = moveDestination;
        pendingGatherNode = gatherNode;

        if (moveDestination.HasValue)
            ShowMarker(moveDestination.Value);

        // PuttingAway ステートへ遷移（しまう時間を待つ）
        putAwayTimer = 2.0f; // しまうアニメーションの長さに合わせる
        SetState(NPCState.PuttingAway);
    }

    /// <summary>
    /// PuttingAway ステートの更新。タイマーが切れたらバッファした行動を実行する。
    /// </summary>
    private void UpdatePuttingAwayState()
    {
        putAwayTimer -= Time.deltaTime;
        if (putAwayTimer > 0f) return;

        // タイマー完了 → ツールをしまってからバッファした行動を実行
        if (toolHolder != null) toolHolder.HideTool();
        SetState(NPCState.Idle); // 一旦Idleに戻す（再帰呼び出し対策）

        if (pendingGatherNode != null && pendingGatherNode.HasResources)
        {
            ResourceNode node = pendingGatherNode;
            pendingGatherNode = null;
            pendingMoveDestination = null;
            GatherResource(node);
        }
        else if (pendingMoveDestination.HasValue)
        {
            Vector3 dest = pendingMoveDestination.Value;
            pendingMoveDestination = null;
            pendingGatherNode = null;
            MoveTo(dest);
        }
        else
        {
            // バッファなし → 運搬があるかチェックしてからIdleへ
            pendingMoveDestination = null;
            pendingGatherNode = null;
            HideMarker();
            
            // ツール収納後、すぐに運搬を試みる
            if (!TryStartHauling())
            {
                // 運搬がなければ通常Idle
            }
        }
    }

    // ==================== Hauling (item pickup) ====================

    /// <summary>
    /// 落ちているアイテムと備蓄場がある場合、運搬を開始する。成功したらtrue。
    /// </summary>
    private bool TryStartHauling()
    {
        // 伐採タスクが残っている場合は運搬しない（タスク優先）
        if (TaskManager.Instance != null && TaskManager.Instance.GatherTasks.Count > 0) return false;

        if (ItemDropManager.Instance == null || StockpileManager.Instance == null) return false;
        if (!ItemDropManager.Instance.HasAnyDroppedItems()) return false;
        if (!StockpileManager.Instance.HasAnyAvailableSpace()) return false;

        // 最寄りの落ちアイテムを探す
        DroppedResource nearest = ItemDropManager.Instance.FindNearestDroppedResource(transform.position);
        if (nearest == null) return false;

        // 備蓄場に空きがあるか確認
        if (!StockpileManager.Instance.TryGetNearestAvailableCell(transform.position, out StockpileZone zone, out Vector2Int cell))
            return false;

        // 運搬開始
        haulTarget = nearest;
        carryTargetZone = zone;
        carryTargetCell = cell;

        agent.isStopped = false;
        agent.speed = defaultMoveSpeed; // 通常スピードで拾いに行く
        agent.SetDestination(nearest.transform.position);
        SetState(NPCState.Hauling);

        Debug.Log($"[NPCController] {gameObject.name}: 運搬開始 → アイテムを拾いに行く");
        return true;
    }

    /// <summary>
    /// Haulingステート: アイテムの場所へ向かっている
    /// </summary>
    private void UpdateHaulingState()
    {
        // アイテムが消えてしまった（他のNPCが先に拾った等）
        if (haulTarget == null)
        {
            StopHauling();
            return;
        }

        // 到着チェック
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                PickUpItem();
            }
        }
    }

    /// <summary>
    /// アイテムを拾う。まだ持てる量に余裕があり、近くに同じアイテムが落ちていれば拾いに行く。
    /// 満杯になるか、もう落ちていなければCarryingステートへ移行。
    /// </summary>
    private void PickUpItem()
    {
        if (haulTarget == null || ItemDropManager.Instance == null)
        {
            if (carryingAmount > 0)
            {
                GoToStockpile();
            }
            else
            {
                StopHauling();
            }
            return;
        }

        // 初回拾いかどうかのチェック
        if (carryingAmount == 0)
        {
            carryingResourceType = haulTarget.Type;
        }

        // アイテムを回収 (持てる空き容量分だけ拾う)
        int spaceLeft = maxCarryAmount - carryingAmount;
        int taken = ItemDropManager.Instance.PickUpResource(haulTarget, spaceLeft);
        carryingAmount += taken;
        haulTarget = null;

        Debug.Log($"[NPCController] {gameObject.name}: {carryingResourceType} を {taken} 個拾った (現在 {carryingAmount}/{maxCarryAmount})");

        // まだ持てる余裕があるか
        if (carryingAmount < maxCarryAmount)
        {
            // 同じ種類のアイテムが近くに落ちているか探す
            DroppedResource nextTarget = ItemDropManager.Instance.FindNearestDroppedResource(transform.position, carryingResourceType);
            if (nextTarget != null)
            {
                // 見つかった場合は次のアイテムへ向かう（Hauling継続）
                haulTarget = nextTarget;
                agent.isStopped = false;
                agent.speed = defaultMoveSpeed;
                agent.SetDestination(haulTarget.transform.position);
                Debug.Log($"[NPCController] {gameObject.name}: まだ持てるので次の {carryingResourceType} を拾いに行く");
                return; // ここで終了。UpdateHaulingStateで到着を待つ
            }
        }

        // 満杯になった、または近くにもうアイテムがない場合は備蓄場へ向かう
        GoToStockpile();
    }

    private void GoToStockpile()
    {
        Debug.Log($"[NPCController] {gameObject.name}: {carryingResourceType} x{carryingAmount} を備蓄場へ運ぶ");

        if (StockpileManager.Instance != null &&
            StockpileManager.Instance.TryGetNearestAvailableCell(transform.position, out StockpileZone zone, out Vector2Int cell))
        {
            carryTargetZone = zone;
            carryTargetCell = cell;

            Vector3 targetWorldPos = GridManager.Instance.GridToWorld(carryTargetCell);
            agent.isStopped = false;
            agent.speed = defaultMoveSpeed * wanderSpeedMultiplier; // 運搬中は少しゆっくり歩く
            agent.SetDestination(targetWorldPos);
            SetState(NPCState.Carrying);
        }
        else
        {
            // 備蓄場がいっぱいになった、または削除された！その場にドロップし直す
            Debug.LogWarning($"[NPCController] {gameObject.name}: 備蓄場の空きがないためその場に落とします。");
            DropCarriedItems();
            StopHauling();
        }
    }

    /// <summary>
    /// Carryingステート: アイテムを抱えて備蓄場へ向かっている
    /// </summary>
    private void UpdateCarryingState()
    {
        // 到着チェック
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                DeliverItem();
            }
        }
    }

    /// <summary>
    /// 備蓄場にアイテムを置く。
    /// 備蓄場内に配置できた分のみResourceManagerに加算する。
    /// </summary>
    private void DeliverItem()
    {
        if (carryingAmount <= 0)
        {
            StopHauling();
            return;
        }

        int actuallyStored = 0;

        if (ItemDropManager.Instance != null && GridManager.Instance != null && carryTargetZone != null)
        {
            // 備蓄場ゾーン内にのみ配置（ゾーン外には溢れない）
            actuallyStored = ItemDropManager.Instance.DropItemInZone(
                carryTargetZone, carryTargetCell, carryingAmount, carryingResourceType);

            // 指定セルの保管量を更新
            if (actuallyStored > 0)
            {
                carryTargetZone.StoreItem(carryTargetCell, actuallyStored);
            }
        }

        // 実際に備蓄場に置けた分だけリソースマネージャーに加算
        if (actuallyStored > 0 && ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource(carryingResourceType, actuallyStored);
            Debug.Log($"[NPCController] {gameObject.name}: {carryingResourceType} x{actuallyStored} を備蓄場に納品！");
        }

        // 置ききれなかった分はその場にドロップ（ResourceManagerには加算しない）
        int leftover = carryingAmount - actuallyStored;
        if (leftover > 0)
        {
            Debug.LogWarning($"[NPCController] {gameObject.name}: 備蓄場に入りきらなかった {carryingResourceType} x{leftover} をその場に落とします");
            if (ItemDropManager.Instance != null && GridManager.Instance != null)
            {
                Vector2Int dropPos = GridManager.Instance.WorldToGrid(transform.position);
                ItemDropManager.Instance.DropItem(dropPos, leftover, carryingResourceType);
            }
        }

        carryingAmount = 0;
        StopHauling();
    }

    /// <summary>
    /// 運搬を中断/完了してIdleに戻る
    /// </summary>
    private void StopHauling()
    {
        haulTarget = null;
        carryTargetZone = null;
        carryingAmount = 0;
        agent.speed = defaultMoveSpeed;
        SetState(NPCState.Idle);
    }

    /// <summary>
    /// 運搬中のアイテムをその場にドロップする（運搬中断時）
    /// </summary>
    private void DropCarriedItems()
    {
        if (carryingAmount <= 0) return;
        if (ItemDropManager.Instance == null || GridManager.Instance == null) return;

        Vector2Int dropPos = GridManager.Instance.WorldToGrid(transform.position);
        ItemDropManager.Instance.DropItem(dropPos, carryingAmount, carryingResourceType);
        carryingAmount = 0;
    }

    // ==================== State & Marker Helpers ====================

    private void SetState(NPCState newState)
    {
        CurrentState = newState;

        if (newState == NPCState.Idle)
        {
            // Idleに入った時に徘徊タイマーをリセット
            wanderTimer = Random.Range(minWanderInterval, maxWanderInterval);

            if (animController != null)
            {
                // BoredIdleはプレイヤーが完全に何も指示していない場合のみ発動
                // タスクが残っている場合や運搬すべきアイテムがある場合は絶対に発動しない
                bool hasPendingTasks = TaskManager.Instance != null && TaskManager.Instance.GatherTasks.Count > 0;
                bool hasItemsToHaul = ItemDropManager.Instance != null 
                    && StockpileManager.Instance != null 
                    && ItemDropManager.Instance.HasAnyDroppedItems() 
                    && StockpileManager.Instance.HasAnyAvailableSpace();

                if (!hasPendingTasks && !hasItemsToHaul && Random.value < 0.3f)
                {
                    animController.PlayBoredIdle();
                }
                else
                {
                    animController.PlayIdle();
                }
            }
        }
    }

    private void ShowMarker(Vector3 position)
    {
        if (targetMarkerPrefab != null)
        {
            if (targetMarkerInstance == null)
            {
                targetMarkerInstance = Instantiate(targetMarkerPrefab);
            }
            targetMarkerInstance.transform.position = position;
            targetMarkerInstance.SetActive(true);
        }
    }

    private void HideMarker()
    {
        if (targetMarkerInstance != null)
        {
            targetMarkerInstance.SetActive(false);
        }
    }

    // ==================== Mode & Visuals ====================

    private void OnPlayerModeChanged(PlayerMode mode)
    {
        UpdateVisuals();
    }

    private void SyncMode(PlayerMode mode)
    {
        UpdateVisuals();
    }

    private Material outlineMaterial;

    private void UpdateVisuals()
    {
        if (modelRenderers == null) return;
        
        bool isGhost = GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Building;
        bool shouldOutline = IsSelected && !isGhost;
        
        for (int i = 0; i < modelRenderers.Length; i++)
        {
            Material[] baseMats = isGhost ? ghostMaterials[i] : originalMaterials[i];
            
            if (shouldOutline && outlineMaterial != null)
            {
                Material[] newMats = new Material[baseMats.Length + 1];
                for (int j = 0; j < baseMats.Length; j++) newMats[j] = baseMats[j];
                newMats[baseMats.Length] = outlineMaterial;
                modelRenderers[i].materials = newMats;
            }
            else
            {
                modelRenderers[i].materials = baseMats;
            }
        }

        if (selectionRing != null)
        {
            selectionRing.SetActive(IsSelected && !isGhost);
        }
    }

    private void CacheRenderers()
    {
        modelRenderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[modelRenderers.Length][];
        ghostMaterials = new Material[modelRenderers.Length][];

        Shader outlineShader = Shader.Find("Custom/Outline");
        if (outlineShader != null)
        {
            outlineMaterial = new Material(outlineShader);
            outlineMaterial.SetColor("_OutlineColor", selectionColor);
            outlineMaterial.SetFloat("_OutlineWidth", 0.015f);
        }

        for (int i = 0; i < modelRenderers.Length; i++)
        {
            Material[] origMats = modelRenderers[i].sharedMaterials;
            originalMaterials[i] = origMats;

            Material[] ghosts = new Material[origMats.Length];
            
            for (int j = 0; j < origMats.Length; j++)
            {
                // ゴーストマテリアル
                Material ghost = new Material(origMats[j]);
                ghost.SetFloat("_Surface", 1); // Transparent
                ghost.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghost.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghost.SetInt("_ZWrite", 0);
                ghost.renderQueue = 3000;
                ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                
                if (ghost.HasProperty("_BaseColor"))
                {
                    Color c = ghost.GetColor("_BaseColor");
                    c.a = buildModeAlpha;
                    ghost.SetColor("_BaseColor", c);
                }
                else if (ghost.HasProperty("_Color"))
                {
                    Color c = ghost.color;
                    c.a = buildModeAlpha;
                    ghost.color = c;
                }
                ghosts[j] = ghost;
            }
            ghostMaterials[i] = ghosts;
        }
    }

    private void CreateSelectionRing()
    {
        selectionRing = new GameObject("SelectionRing");
        selectionRing.transform.SetParent(transform);
        selectionRing.transform.localPosition = new Vector3(0, 0.05f, 0); // 地面から少しだけ浮かす
        selectionRing.transform.localRotation = Quaternion.Euler(90, 0, 0);
        
        var line = selectionRing.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.positionCount = 31;
        line.loop = true;
        
        // スプライト用のデフォルトマテリアルを利用して緑の円を描く
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = selectionColor;
        line.endColor = selectionColor;

        float radius = 0.6f;
        for (int i = 0; i <= 30; i++)
        {
            float angle = i * Mathf.PI * 2 / 30f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0));
        }

        selectionRing.SetActive(false);
    }

    // ==================== ISelectable Implementation ====================

    public string GetSelectionName()
    {
        return $"生存者 ({gameObject.name})";
    }

    public string GetSelectionDescription()
    {
        return "指示を出して資源採取や建築、運搬を行ってくれる心強い生存者です。";
    }

    public Dictionary<string, string> GetSelectionStats()
    {
        var stats = new Dictionary<string, string>();
        stats.Add("現在の行動", GetStateNameJapanese());
        stats.Add("運搬アイテム", carryingAmount > 0 ? $"{GetResourceTypeJapanese(carryingResourceType)} × {carryingAmount}" : "なし");
        stats.Add("最大可搬量", maxCarryAmount.ToString());
        return stats;
    }

    private string GetStateNameJapanese()
    {
        switch (CurrentState)
        {
            case NPCState.Idle: return "待機中";
            case NPCState.Wander: return "うろうろ";
            case NPCState.Moving: return "移動中";
            case NPCState.Gathering: return "資源採取中";
            case NPCState.Hauling: return "アイテム運搬中";
            case NPCState.PutAway: return "道具の片づけ中";
            default: return CurrentState.ToString();
        }
    }

    private string GetResourceTypeJapanese(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return "木材";
            case ResourceType.Stone: return "石材";
            case ResourceType.Food: return "食料";
            default: return type.ToString();
        }
    }
}
