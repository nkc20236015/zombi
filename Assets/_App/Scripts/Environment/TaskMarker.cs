using UnityEngine;

public class TaskMarker : MonoBehaviour
{
    private Vector3 initialLocalPos;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatHeight = 0.2f;

    void Start()
    {
        initialLocalPos = transform.localPosition;
    }

    void Update()
    {
        // 上下にフワフワ
        float newY = initialLocalPos.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.localPosition = new Vector3(initialLocalPos.x, newY, initialLocalPos.z);
        
        // カメラの方を向く（ビルボード）
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }

    public void SetVisible(bool isVisible)
    {
        gameObject.SetActive(isVisible);
    }
}
