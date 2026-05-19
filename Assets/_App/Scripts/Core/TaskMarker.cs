using UnityEngine;

/// <summary>
/// タスクマーカー（アイコン）の表示とカメラ追従を管理する。
/// </summary>
public class TaskMarker : MonoBehaviour
{
    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;

    [SerializeField] private float bobbingSpeed = 2f;
    [SerializeField] private float bobbingAmount = 0.2f;
    private Vector3 initialLocalPosition;

    void Awake()
    {
        mainCamera = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialLocalPosition = transform.localPosition;
    }

    void Update()
    {
        if (mainCamera == null) return;

        // カメラの方を常に向く（ビルボード）
        transform.forward = mainCamera.transform.forward;

        // 上下にフワフワさせるアニメーション
        float newY = initialLocalPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;
        transform.localPosition = new Vector3(initialLocalPosition.x, newY, initialLocalPosition.z);
    }

    /// <summary>
    /// マーカーの表示ON/OFFを設定
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = isVisible;
        }
        else
        {
            gameObject.SetActive(isVisible);
        }
    }
}