using UnityEngine;

public class TopDownCamera : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float fastMoveMultiplier = 3f;
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

    // ==================== Follow Mode ====================
    private Transform followTarget;       // 追従対象（単体の場合）
    private NPCController[] followGroup;  // 追従対象（複数の場合）
    public bool IsFollowing => followTarget != null || (followGroup != null && followGroup.Length > 0);

    void Start()
    {
        transform.position = new Vector3(25f, initialHeight, 10f);
        transform.rotation = Quaternion.Euler(initialPitch, 0f, 0f);
        targetPos = transform.position;
        rotX = initialPitch;
    }

    void Update()
    {
        // ---- 追従解除の判定（中ホイール以外の操作） ----
        if (IsFollowing)
        {
            bool cancelFollow = false;

            // WASD
            float hInput = Input.GetAxisRaw("Horizontal");
            float vInput = Input.GetAxisRaw("Vertical");
            if (!Mathf.Approximately(hInput, 0f) || !Mathf.Approximately(vInput, 0f))
                cancelFollow = true;

            // Q/E
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E))
                cancelFollow = true;

            // スクロールズーム
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            if (!Mathf.Approximately(scrollInput, 0f))
                cancelFollow = true;

            // Escキー
            if (Input.GetKeyDown(KeyCode.Escape))
                cancelFollow = true;

            if (cancelFollow)
            {
                StopFollowing();
            }
        }

        // ---- 中ホイール回転（追従中も有効） ----
        if (Input.GetMouseButtonDown(2)) { mmb = true; lastMouse = Input.mousePosition; }
        if (Input.GetMouseButtonUp(2)) mmb = false;

        if (mmb)
        {
            Vector3 d = Input.mousePosition - lastMouse;
            lastMouse = Input.mousePosition;
            rotY += d.x * rotationSpeed * 0.1f;
            rotX -= d.y * rotationSpeed * 0.1f;
            rotX = Mathf.Clamp(rotX, 10f, 89f);
            transform.rotation = Quaternion.Euler(rotX, rotY, 0f);
        }

        // ---- 追従中の処理 ----
        if (IsFollowing)
        {
            Vector3 followPos = GetFollowCenter();
            FocusOnPosition(followPos);
        }
        else
        {
            // ---- 通常カメラ操作（追従していないとき） ----

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
        }

        // ---- F キーで追従開始 ----
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (SelectionManager.Instance != null && SelectionManager.Instance.SelectedNPCs.Count > 0)
            {
                StartFollowing();
            }
        }

        // Clamp and apply
        targetPos.y = Mathf.Clamp(targetPos.y, minHeight, maxHeight);
        targetPos.x = Mathf.Clamp(targetPos.x, boundsX.x, boundsX.y);
        targetPos.z = Mathf.Clamp(targetPos.z, boundsZ.x, boundsZ.y);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref vel, moveSmoothTime);
    }

    // ==================== Follow Methods ====================

    /// <summary>
    /// 選択中のNPCの追従を開始する。
    /// 1人なら直接Transformを追う、複数なら毎フレーム中心点を計算する。
    /// </summary>
    private void StartFollowing()
    {
        var selected = SelectionManager.Instance.SelectedNPCs;
        if (selected.Count == 1 && selected[0] != null)
        {
            followTarget = selected[0].transform;
            followGroup = null;
        }
        else
        {
            // 複数選択の場合はグループとして追従
            followTarget = null;
            followGroup = new NPCController[selected.Count];
            for (int i = 0; i < selected.Count; i++)
                followGroup[i] = selected[i];
        }

        // 最初のフォーカスを即座に適用
        FocusOnPosition(GetFollowCenter());
    }

    /// <summary>
    /// 追従を停止する。
    /// </summary>
    private void StopFollowing()
    {
        followTarget = null;
        followGroup = null;
    }

    /// <summary>
    /// 追従対象の中心座標を取得する。
    /// </summary>
    private Vector3 GetFollowCenter()
    {
        if (followTarget != null)
            return followTarget.position;

        if (followGroup != null && followGroup.Length > 0)
        {
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (var npc in followGroup)
            {
                if (npc != null)
                {
                    center += npc.transform.position;
                    count++;
                }
            }
            if (count > 0)
                return center / count;
        }

        return transform.position;
    }

    // ==================== Public API ====================

    public void FocusOnPosition(Vector3 worldPosition)
    {
        float h = transform.position.y;
        float off = h / Mathf.Tan(rotX * Mathf.Deg2Rad);
        targetPos = new Vector3(worldPosition.x, h, worldPosition.z - off);
    }
}