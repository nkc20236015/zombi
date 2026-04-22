using UnityEngine;

public class TopDownCamera : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float fastMoveMultiplier = 3f;
    [SerializeField] private float panSpeed = 0.5f;
    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 3f;
    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float minHeight = 3f;
    [SerializeField] private float maxHeight = 80f;
    [Header("Vertical")]
    [SerializeField] private float verticalSpeed = 15f;
    [Header("Smoothing")]
    [SerializeField] private float moveSmoothTime = 0.1f;
    [Header("Bounds")]
    [SerializeField] private Vector2 boundsX = new Vector2(-30f, 80f);
    [SerializeField] private Vector2 boundsZ = new Vector2(-30f, 80f);
    [Header("Initial")]
    [SerializeField] private float initialPitch = 50f;
    [SerializeField] private float initialHeight = 30f;

    private Vector3 targetPos;
    private Vector3 vel;
    private float rotX, rotY;
    private bool rmb, mmb;
    private Vector3 lastMouse;

    void Start()
    {
        transform.position = new Vector3(25f, initialHeight, 10f);
        transform.rotation = Quaternion.Euler(initialPitch, 0f, 0f);
        targetPos = transform.position;
        rotX = initialPitch;
    }

    void Update()
    {
        // Mouse buttons
        if (Input.GetMouseButtonDown(1)) { rmb = true; lastMouse = Input.mousePosition; }
        if (Input.GetMouseButtonUp(1)) rmb = false;
        if (Input.GetMouseButtonDown(2)) { mmb = true; lastMouse = Input.mousePosition; }
        if (Input.GetMouseButtonUp(2)) mmb = false;

        // Right-click rotate
        if (rmb)
        {
            Vector3 d = Input.mousePosition - lastMouse;
            lastMouse = Input.mousePosition;
            rotY += d.x * rotationSpeed * 0.1f;
            rotX -= d.y * rotationSpeed * 0.1f;
            rotX = Mathf.Clamp(rotX, 10f, 89f);
            transform.rotation = Quaternion.Euler(rotX, rotY, 0f);
        }

        // Middle-click pan
        if (mmb)
        {
            Vector3 d = Input.mousePosition - lastMouse;
            lastMouse = Input.mousePosition;
            float hf = Mathf.Max(1f, transform.position.y * 0.05f);
            targetPos -= (transform.right * d.x + transform.up * d.y) * panSpeed * hf * Time.deltaTime;
        }

        // WASD movement (disabled in combat mode)
        bool combatMode = GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Combat;
        if (!combatMode)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (!Mathf.Approximately(h, 0f) || !Mathf.Approximately(v, 0f))
            {
                float spd = moveSpeed;
                if (Input.GetKey(KeyCode.LeftShift)) spd *= fastMoveMultiplier;
                float hf = Mathf.Max(1f, transform.position.y * 0.05f);
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                Vector3 rt = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                targetPos += (fwd * v + rt * h).normalized * spd * hf * Time.deltaTime;
            }
        }

        // Q/E vertical
        float vert = 0f;
        if (Input.GetKey(KeyCode.E)) vert = 1f;
        else if (Input.GetKey(KeyCode.Q)) vert = -1f;
        if (!Mathf.Approximately(vert, 0f))
        {
            float vs = verticalSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) vs *= fastMoveMultiplier;
            targetPos.y += vert * vs * Time.deltaTime;
        }

        // Scroll zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (!Mathf.Approximately(scroll, 0f))
        {
            float hf = Mathf.Max(1f, transform.position.y * 0.1f);
            targetPos += transform.forward * scroll * zoomSpeed * hf;
        }

        // Clamp and apply
        targetPos.y = Mathf.Clamp(targetPos.y, minHeight, maxHeight);
        targetPos.x = Mathf.Clamp(targetPos.x, boundsX.x, boundsX.y);
        targetPos.z = Mathf.Clamp(targetPos.z, boundsZ.x, boundsZ.y);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref vel, moveSmoothTime);
    }

    public void FocusOnPosition(Vector3 worldPosition)
    {
        float h = transform.position.y;
        float off = h / Mathf.Tan(rotX * Mathf.Deg2Rad);
        targetPos = new Vector3(worldPosition.x, h, worldPosition.z - off);
    }
}