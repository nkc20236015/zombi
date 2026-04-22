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

    private NavMeshAgent agent;
    private Camera mainCam;
    private PlayerMode curMode = PlayerMode.Normal;

    void Awake() { agent = GetComponent<NavMeshAgent>(); agent.speed = clickMoveSpeed; }

    void Start()
    {
        mainCam = Camera.main;
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
        if (curMode == PlayerMode.Combat) DoCombatMove();
        else DoClickMove();
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
        if (curMode == PlayerMode.Combat) { agent.ResetPath(); agent.enabled = false; }
        else { agent.enabled = true; agent.speed = clickMoveSpeed; }
    }
}