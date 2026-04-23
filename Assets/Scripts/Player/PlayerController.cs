using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerController : MonoBehaviour
{
    [Header("Click Movement")]
    [SerializeField] private float clickMoveSpeed = 5f;
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("WASD Movement")]
    [SerializeField] private float wasdMoveSpeed = 6f;
    [SerializeField] private float rotSpeed = 10f;

    [Header("Build Mode Visual")]
    [SerializeField] private float buildModeAlpha = 0.3f;

    private NavMeshAgent agent;
    private Camera mainCam;
    private PlayerMode curMode = PlayerMode.Normal;

    // 建築モード半透明化用
    private Renderer[] modelRenderers;
    private Material[][] originalMaterials;
    private Material[][] ghostMaterials;

    void Awake() { agent = GetComponent<NavMeshAgent>(); agent.speed = clickMoveSpeed; }

    void Start()
    {
        mainCam = Camera.main;
        CacheRenderers();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerModeChanged += OnModeChanged;
            curMode = GameManager.Instance.CurrentPlayerMode;
        }
        SyncMode();
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnPlayerModeChanged -= OnModeChanged;
    }

    void Update()
    {
        switch (curMode)
        {
            case PlayerMode.Normal:
                DoClickMove();
                break;
            case PlayerMode.Combat:
                DoCombatMove();
                break;
            case PlayerMode.Building:
                // 建築モード中はプレイヤー操作を完全に無効化
                break;
        }
    }

    void DoClickMove()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
            agent.SetDestination(hit.point);
    }

    void DoCombatMove()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 camFwd = Vector3.ProjectOnPlane(mainCam.transform.forward, Vector3.up).normalized;
        Vector3 camRt = Vector3.ProjectOnPlane(mainCam.transform.right, Vector3.up).normalized;
        Vector3 dir = (camFwd * v + camRt * h).normalized;
        if (dir.sqrMagnitude > 0.01f)
        {
            transform.position += dir * wasdMoveSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), rotSpeed * Time.deltaTime);
        }
    }

    void OnModeChanged(PlayerMode m) { curMode = m; SyncMode(); }

    void SyncMode()
    {
        switch (curMode)
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
}