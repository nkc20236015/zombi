using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// プレイヤーの移動制御。
/// - 昼間（Normalモード）: マウスクリックで移動先を指示（NavMeshAgent使用）
/// - 夜間（Combatモード）: WASD直接移動
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PlayerController : MonoBehaviour
{
    [Header("Click Movement (Daytime)")]
    [SerializeField] private float clickMoveSpeed = 5f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private GameObject moveIndicatorPrefab;

    [Header("WASD Movement (Nighttime)")]
    [SerializeField] private float wasdMoveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Visual")]
    [SerializeField] private float modelRotationSmooth = 10f;
    [SerializeField] private float buildModeAlpha = 0.3f;

    private NavMeshAgent agent;
    private Camera mainCamera;
    private PlayerMode currentMode = PlayerMode.Normal;
    private Vector3 wasdMoveDirection;
    private GameObject moveIndicator;

    // 建築モード半透明化用
    private Renderer[] modelRenderers;
    private Material[][] originalMaterials;
    private Material[][] ghostMaterials;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = clickMoveSpeed;
    }

    private void Start()
    {
        mainCamera = Camera.main;

        // モデルのRendererをキャッシュし、半透明マテリアルを準備
        CacheRenderers();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerModeChanged += OnPlayerModeChanged;
            currentMode = GameManager.Instance.CurrentPlayerMode;
        }

        UpdateMovementMode();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerModeChanged -= OnPlayerModeChanged;
        }
    }

    private void Update()
    {
        switch (currentMode)
        {
            case PlayerMode.Normal:
                HandleClickMovement();
                break;
            case PlayerMode.Combat:
                HandleWASDMovement();
                break;
            case PlayerMode.Building:
                // 建築モード中はプレイヤー操作を無効化
                break;
        }
    }

    /// <summary>
    /// マウスクリックで移動先を指示（昼間モード）
    /// </summary>
    private void HandleClickMovement()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // UI上をクリックした場合は無視
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
            {
                agent.SetDestination(hit.point);

                // 移動インジケータを表示
                ShowMoveIndicator(hit.point);
            }
        }
    }

    /// <summary>
    /// WASD直接移動（夜間戦闘モード）
    /// </summary>
    private void HandleWASDMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // カメラの向きを基準にした移動方向を計算
        Vector3 camForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;

        wasdMoveDirection = (camForward * v + camRight * h).normalized;

        if (wasdMoveDirection.sqrMagnitude > 0.01f)
        {
            // 移動
            Vector3 move = wasdMoveDirection * wasdMoveSpeed * Time.deltaTime;
            transform.position += move;

            // モデルの向きを移動方向に回転
            Quaternion targetRotation = Quaternion.LookRotation(wasdMoveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, modelRotationSmooth * Time.deltaTime);
        }
    }

    /// <summary>
    /// プレイヤーモードが変更されたとき
    /// </summary>
    private void OnPlayerModeChanged(PlayerMode newMode)
    {
        currentMode = newMode;
        UpdateMovementMode();
    }

    /// <summary>
    /// 現在のモードに応じてNavMeshAgentの有効/無効を切り替え
    /// </summary>
    private void UpdateMovementMode()
    {
        switch (currentMode)
        {
            case PlayerMode.Normal:
                agent.enabled = true;
                agent.speed = clickMoveSpeed;
                SetGhostMode(false);
                break;
            case PlayerMode.Building:
                // 建築モード: 移動停止 + 半透明化
                if (agent.isOnNavMesh) agent.ResetPath();
                agent.enabled = false;
                SetGhostMode(true);
                break;
            case PlayerMode.Combat:
                if (agent.isOnNavMesh) agent.ResetPath();
                agent.enabled = false;
                SetGhostMode(false);
                break;
        }
    }

    /// <summary>
    /// モデルのRendererをキャッシュし、半透明用マテリアルを事前生成する
    /// </summary>
    private void CacheRenderers()
    {
        modelRenderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[modelRenderers.Length][];
        ghostMaterials = new Material[modelRenderers.Length][];

        for (int i = 0; i < modelRenderers.Length; i++)
        {
            Material[] origMats = modelRenderers[i].sharedMaterials;
            originalMaterials[i] = origMats;

            Material[] ghosts = new Material[origMats.Length];
            for (int j = 0; j < origMats.Length; j++)
            {
                Material ghost = new Material(origMats[j]);
                // URP透過設定
                ghost.SetFloat("_Surface", 1); // Transparent
                ghost.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghost.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghost.SetInt("_ZWrite", 0);
                ghost.renderQueue = 3000;
                ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                Color c = ghost.color;
                c.a = buildModeAlpha;
                ghost.color = c;
                ghosts[j] = ghost;
            }
            ghostMaterials[i] = ghosts;
        }
    }

    /// <summary>
    /// プレイヤーモデルの半透明化を切り替える
    /// </summary>
    private void SetGhostMode(bool ghost)
    {
        if (modelRenderers == null) return;

        for (int i = 0; i < modelRenderers.Length; i++)
        {
            modelRenderers[i].materials = ghost ? ghostMaterials[i] : originalMaterials[i];
        }
    }

    /// <summary>
    /// 移動先インジケータを表示する
    /// </summary>
    private void ShowMoveIndicator(Vector3 position)
    {
        if (moveIndicatorPrefab == null) return;

        if (moveIndicator == null)
        {
            moveIndicator = Instantiate(moveIndicatorPrefab);
        }

        moveIndicator.transform.position = position + Vector3.up * 0.05f;
        moveIndicator.SetActive(true);

        CancelInvoke(nameof(HideMoveIndicator));
        Invoke(nameof(HideMoveIndicator), 1f);
    }

    private void HideMoveIndicator()
    {
        if (moveIndicator != null)
        {
            moveIndicator.SetActive(false);
        }
    }
}
