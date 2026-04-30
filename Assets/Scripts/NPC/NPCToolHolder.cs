using UnityEngine;

/// <summary>
/// NPCの右手にツール（斧・ピッケル）を表示/非表示するコンポーネント。
/// Animatorの右手ボーンに空のホルダーオブジェクトを作成し、
/// 採取開始時にツールプレハブをインスタンス化して手に持たせる。
/// </summary>
public class NPCToolHolder : MonoBehaviour
{
    [Header("Tool Prefabs")]
    [SerializeField] private GameObject axePrefab;     // Veresen axe
    [SerializeField] private GameObject pickaxePrefab;  // Veresen pickaxe

    [Header("Hold Settings")]
    [SerializeField] private Vector3 holdPositionOffset = new Vector3(0.05f, 0.05f, 0f);
    [SerializeField] private Vector3 holdRotationOffset = new Vector3(0f, 0f, 0f);

    private Transform handBone;      // 右手ボーン
    private Transform toolHolder;    // ツール配置用の親Transform
    private GameObject currentTool;  // 現在手に持っているツールインスタンス
    private ResourceType? currentToolType; // 現在のツールタイプ

    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();

#if UNITY_EDITOR
        // エディタ上でプレハブが未設定の場合、自動ロード
        if (axePrefab == null)
            axePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Veresen/BasicTools/Prefabs/axe.prefab");
        if (pickaxePrefab == null)
            pickaxePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Veresen/BasicTools/Prefabs/pickaxe.prefab");
#endif
    }

    void Start()
    {
        SetupToolHolder();
    }

    private void SetupToolHolder()
    {
        if (animator == null) return;

        // Humanoid Avatarの右手ボーンを取得
        handBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (handBone == null)
        {
            Debug.LogWarning("[NPCToolHolder] Right hand bone not found!");
            return;
        }

        // ツールを配置するための空のホルダーを作成
        var holderGO = new GameObject("ToolHolder");
        toolHolder = holderGO.transform;
        toolHolder.SetParent(handBone, false);
        toolHolder.localPosition = holdPositionOffset;
        toolHolder.localRotation = Quaternion.Euler(holdRotationOffset);
    }

    /// <summary>
    /// 指定したResourceTypeに応じたツールを手に表示する。
    /// TakeItemアニメーション中に呼ばれることを想定。
    /// </summary>
    public void ShowTool(ResourceType type)
    {
        // 同じツールが既に表示されている場合はスキップ
        if (currentTool != null && currentToolType == type)
        {
            currentTool.SetActive(true);
            return;
        }

        // 既存のツールを破棄
        HideTool();

        GameObject prefab = GetToolPrefab(type);
        if (prefab == null || toolHolder == null) return;

        currentTool = Instantiate(prefab, toolHolder);
        currentTool.transform.localPosition = Vector3.zero;
        currentTool.transform.localRotation = Quaternion.identity;
        currentToolType = type;
    }

    /// <summary>
    /// 現在手に持っているツールを非表示にする。
    /// PutItemアニメーション終了時に呼ばれることを想定。
    /// </summary>
    public void HideTool()
    {
        if (currentTool != null)
        {
            Destroy(currentTool);
            currentTool = null;
            currentToolType = null;
        }
    }

    private GameObject GetToolPrefab(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:
                return axePrefab;
            case ResourceType.Stone:
                return pickaxePrefab;
            default:
                return null;
        }
    }
}
