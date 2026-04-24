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
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= UpdatePhaseUI;
            GameManager.Instance.OnNewDay -= UpdateDayUI;
        }
    }

    private void UpdatePhaseUI(GameState state)
    {
        if (phaseText != null)
        {
            string newText = "";
            Color newColor = Color.white;

            switch (state)
            {
                case GameState.Dawn: 
                    newText = "Phase: 朝 (Dawn)"; 
                    newColor = new Color(1f, 0.8f, 0.5f); 
                    break;
                case GameState.Daytime: 
                    newText = "Phase: 昼 (Daytime)"; 
                    newColor = Color.white; 
                    break;
                case GameState.Evening: 
                    newText = "Phase: 夕方 (Evening)"; 
                    newColor = new Color(1f, 0.5f, 0.3f); 
                    break;
                case GameState.Night: 
                    newText = "Phase: 夜 (Night)"; 
                    newColor = new Color(0.5f, 0.5f, 1f); 
                    break;
                default: 
                    newText = "Phase: " + state.ToString(); 
                    newColor = Color.white;
                    break;
            }

            phaseText.text = newText;
            
            // DOTween アニメーション
            phaseText.transform.DOKill(true); // 重複実行を防ぐためにリセット
            phaseText.transform.DOPunchScale(Vector3.one * 0.2f, 0.5f, 5, 1f); // ポップする動き
            phaseText.DOColor(newColor, 1f); // 1秒かけて色を滑らかに変更
        }
    }

    private void UpdateDayUI(int day)
    {
        if (dayText != null)
        {
            dayText.text = "Day " + day;
            
            // DOTween アニメーション
            dayText.transform.DOKill(true);
            dayText.transform.DOPunchScale(Vector3.one * 0.3f, 0.5f, 5, 1f);
        }
    }
}
