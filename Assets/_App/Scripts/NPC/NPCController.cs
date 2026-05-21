using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCController : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float buildModeAlpha = 0.3f;
    [SerializeField] private Color selectionColor = new Color(0.5f, 1f, 0.5f, 1f);

    [Header("Gathering Settings")]
    [SerializeField] private float gatherInterval = 2.0f; // 採取アニメーション間隔（秒）

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
        }
    }

    // ==================== State Updates ====================

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
                // 実際の目的地に到達しているか（壁で止まった場合のチェック）
                float distanceToDest = Vector3.Distance(transform.position, agent.destination);
                if (distanceToDest > agent.stoppingDistance + 1.0f)
                {
                    Debug.Log($"[NPCController] 到達不能: 経路が塞がれています。");
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
            if (harvested > 0 && ResourceManager.Instance != null)
            {
                ResourceManager.Instance.AddResource(targetNode.Type, harvested);
            }

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
                toolHolder.ShowTool(targetNode.Type);
            }
        }

        gatherTimer = 1.5f; // TakeItemアニメーション(1秒)を待ってから最初の採取
        PlayGatherAnimation();
    }

    private void PlayGatherAnimation()
    {
        if (animController == null) return;

        // ResourceType に応じてアクションタイプを切り替え
        // 0 = Chop（伐採）, 1 = Mine（採掘）
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
            // バッファなし → Idle
            pendingMoveDestination = null;
            pendingGatherNode = null;
            HideMarker();
        }
    }

    // ==================== State & Marker Helpers ====================

    private void SetState(NPCState newState)
    {
        CurrentState = newState;
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
}
