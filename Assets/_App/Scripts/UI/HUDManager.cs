using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 画面のHUD（ヘッドアップディスプレイ）を管理するクラス。
/// GameManagerのイベントをリッスンしてUIを更新します。
/// </summary>
public class HUDManager : MonoBehaviour
{
    [Header("Top Bar UI")]
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private TextMeshProUGUI phaseText;

    [Header("Resource UI")]
    [SerializeField] private TextMeshProUGUI woodText;
    [SerializeField] private TextMeshProUGUI stoneText;
    [SerializeField] private TextMeshProUGUI foodText;

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            // GameManagerのイベントに登録
            GameManager.Instance.OnGameStateChanged += UpdatePhaseUI;
            GameManager.Instance.OnNewDay += UpdateDayUI;
            
            // 初期状態の反映
            UpdateDayUI(GameManager.Instance.CurrentDay);
            UpdatePhaseUI(GameManager.Instance.CurrentGameState);
        }

        // ResourceManagerのイベントに登録
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += UpdateResourceUI;
            // 初期値の反映
            UpdateAllResourceUI();
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= UpdatePhaseUI;
            GameManager.Instance.OnNewDay -= UpdateDayUI;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= UpdateResourceUI;
        }
    }

    // ==================== Phase / Day UI ====================

    private void UpdatePhaseUI(GameState state)
    {
        if (phaseText != null)
        {
            string newText = "";
            Color newColor = Color.white;

            switch (state)
            {
                case GameState.Dawn: 
                    newText = "朝"; 
                    newColor = new Color(1f, 0.8f, 0.5f); 
                    break;
                case GameState.Daytime: 
                    newText = "昼"; 
                    newColor = Color.white; 
                    break;
                case GameState.Evening: 
                    newText = "夕方"; 
                    newColor = new Color(1f, 0.5f, 0.3f); 
                    break;
                case GameState.Night: 
                    newText = "夜"; 
                    newColor = new Color(0.5f, 0.5f, 1f); 
                    break;
                default: 
                    newText = "Phase: " + state.ToString(); 
                    newColor = Color.white;
                    break;
            }

            phaseText.text = newText;
            
            // DOTween アニメーション (ふわっとしたポップ)
            phaseText.transform.DOKill(true);
            phaseText.transform.localScale = Vector3.one;
            phaseText.transform.DOScale(1.1f, 0.2f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.InOutSine);
            phaseText.DOColor(newColor, 1f); // 1秒かけて色を滑らかに変更
        }
    }

    private void UpdateDayUI(int day)
    {
        if (dayText != null)
        {
            dayText.text = day + "日目";

            // DOTween アニメーション (ふわっとしたポップ)
            dayText.transform.DOKill(true);
            dayText.transform.localScale = Vector3.one;
            dayText.transform.DOScale(1.1f, 0.2f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }
    }

    // ==================== Resource UI ====================

    private void UpdateResourceUI(ResourceType type, int amount)
    {
        switch (type)
        {
            case ResourceType.Wood:
                UpdateResourceText(woodText, amount);
                break;
            case ResourceType.Stone:
                UpdateResourceText(stoneText, amount);
                break;
            case ResourceType.Food:
                UpdateResourceText(foodText, amount);
                break;
        }
    }

    private void UpdateResourceText(TextMeshProUGUI text, int amount)
    {
        if (text == null) return;

        text.text = amount.ToString();

        // 揺れ（スケール）を完全に無くし、色だけがふわっと光るアニメーションに変更
        text.DOKill(true);
        text.color = Color.white;
        
        // 少し黄色っぽく光って、元に戻る
        text.DOColor(new Color(1f, 0.9f, 0.5f), 0.15f).SetLoops(2, LoopType.Yoyo);
    }

    private void UpdateAllResourceUI()
    {
        if (ResourceManager.Instance == null) return;

        UpdateResourceUI(ResourceType.Wood, ResourceManager.Instance.GetResource(ResourceType.Wood));
        UpdateResourceUI(ResourceType.Stone, ResourceManager.Instance.GetResource(ResourceType.Stone));
        UpdateResourceUI(ResourceType.Food, ResourceManager.Instance.GetResource(ResourceType.Food));
    }
}
