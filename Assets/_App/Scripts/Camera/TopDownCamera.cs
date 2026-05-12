using UnityEngine;

public class TopDownCamera : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float fastMoveMultiplier = 3f;
    [SerializeField] private float panSpeed = 0.05f;
    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 3f;
    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float minHeight = 3f;
    [SerializeField] private float maxHeight = 80f;

    [Header("Smoothing")]
    [SerializeField] private float moveSmoothTime = 0.1f;
    [Header("Bounds")]
    [SerializeField] private float boundsMargin = 20f;
    private Vector2 boundsX;
    private Vector2 boundsZ;
    [Header("Initial")]
    [SerializeField] private float initialPitch = 50f;
    [SerializeField] private float initialHeight = 10f;

    private Vector3 targetPos;
    private Vector3 vel;
    private float rotX, rotY;
    private bool rmb, mmb;
    private Vector3 lastMouse;
    private Vector3 lastMouseRight;

    void Start()
    {
        if (VoxelWorld.Instance != null)
        {
            float w = VoxelWorld.Instance.WorldWidth * VoxelData.BlockWidth;
            float d = VoxelWorld.Instance.WorldDepth * VoxelData.BlockDepth;
            boundsX = new Vector2(-boundsMargin, w + boundsMargin);
            // Z方向のマイナスマージンは、斜め下を向くカメラの性質上多めに取る
            boundsZ = new Vector2(-boundsMargin * 2f, d + boundsMargin);
            
            // マップ中央付近を初期位置にする
            float startX = w * 0.5f;
            float startZ = d * 0.5f - (initialHeight / Mathf.Tan(initialPitch * Mathf.Deg2Rad));
            transform.position = new Vector3(startX, initialHeight, startZ);
        }
        else
        {
            boundsX = new Vector2(-30f, 80f);
            boundsZ = new Vector2(-30f, 80f);
            transform.position = new Vector3(25f, initialHeight, 10f);
        }

        transform.rotation = Quaternion.Euler(initialPitch, 0f, 0f);
        targetPos = transform.position;
        rotX = initialPitch;
    }

    void Update()
    {
        // ---- 中ホイール回転（追従中も有効） ----
        if (Input.GetMouseButtonDown(2)) { mmb = true; lastMouse = Input.mousePosition; }
        if (Input.GetMouseButtonUp(2)) mmb = false;

        if (mmb)
        {
            Vector3 d = Input.mousePosition - lastMouse;
            lastMouse = Input.mousePosition;
            rotY += d.x * rotationSpeed * 0.1f;
            rotX -= d.y * rotationSpeed * 0.1f;
            rotX = Mathf.Clamp(rotX, 30f, 89f);
            transform.rotation = Quaternion.Euler(rotX, rotY, 0f);
        }

        // ---- 右クリックドラッグ移動（パン） ----
        if (Input.GetMouseButtonDown(1)) { rmb = true; lastMouseRight = Input.mousePosition; }
        if (Input.GetMouseButtonUp(1)) rmb = false;

        if (rmb)
        {
            Vector3 d = Input.mousePosition - lastMouseRight;
            lastMouseRight = Input.mousePosition;
            
            float hf = Mathf.Max(1f, transform.position.y * 0.05f);
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 rt = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            
            // ドラッグした方向と逆に進む (d.x, d.y分マイナスする)
            targetPos -= (rt * d.x + fwd * d.y) * panSpeed * hf;
        }

        // ---- 通常カメラ操作 ----

        // WASD movement
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


        // Scroll zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (!Mathf.Approximately(scroll, 0f))
        {
            float hf = Mathf.Max(1f, transform.position.y * 0.1f);
            targetPos += transform.forward * scroll * zoomSpeed * hf;
        }

        // Clamp and apply
        float surfaceY = 0f;
        if (VoxelWorld.Instance != null)
        {
            surfaceY = VoxelWorld.Instance.GetSurfaceWorldY(targetPos.x, targetPos.z);
        }
        // 絶対的なminHeightと、地面+1.5fの高さの大きい方を下限とする
        float dynamicMinHeight = Mathf.Max(minHeight, surfaceY + 1.5f);

        targetPos.y = Mathf.Clamp(targetPos.y, dynamicMinHeight, maxHeight);
        targetPos.x = Mathf.Clamp(targetPos.x, boundsX.x, boundsX.y);
        targetPos.z = Mathf.Clamp(targetPos.z, boundsZ.x, boundsZ.y);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref vel, moveSmoothTime);
    }

    // ==================== Public API ====================

    public void FocusOnPosition(Vector3 worldPosition)
    {
        float h = transform.position.y;
        float off = h / Mathf.Tan(rotX * Mathf.Deg2Rad);
        targetPos = new Vector3(worldPosition.x, h, worldPosition.z - off);
    }
}