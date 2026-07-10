using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

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

    [Header("Task Panel Buttons")]
    [SerializeField] private Button axeButton;      // AxeImage に付いている Button
    [SerializeField] private Button cancelButton;   // CancelImage に付いている Button
    [SerializeField] private Button stockpileButton; // エリア作成ボタン
    [SerializeField] private Button kamaButton;     // kamaImage に付いている Button
    [SerializeField] private Button farmButton;     // farmImage に付いている Button

    [Header("Detail Panel Settings")]
    [Tooltip("詳細パネルのプレハブ。未設定の場合は自動生成されます。")]
    [SerializeField] private GameObject detailPanelPrefab;
    [Tooltip("詳細パネル表示時に非表示にしたいUIオブジェクトのリスト（トップバーやボタンなど）")]
    [SerializeField] private List<GameObject> uiElementsToHide = new List<GameObject>();

    // 詳細情報パネル関連のメンバ変数
    private GameObject taskPanelObject;
    private GameObject detailPanelObj;
    private TextMeshProUGUI detailNameText;
    private TextMeshProUGUI detailDescText;
    private GameObject detailStatsContainer;
    private List<TextMeshProUGUI> activeStatTexts = new List<TextMeshProUGUI>();

    // ハイライト用の色
    private Color normalButtonColor = Color.white;
    private Color activeButtonColor = new Color(1f, 0.85f, 0.4f, 1f); // 黄金色ハイライト

    private void Start()
    {
        // 既存のボタンの親（タスクパネルなど）を自動収集して、非表示リストに追加する（重複防止）
        AddParentToHideList(axeButton);
        AddParentToHideList(cancelButton);
        AddParentToHideList(stockpileButton);
        AddParentToHideList(kamaButton);
        AddParentToHideList(farmButton);

        // 詳細情報パネルを動的に生成
        CreateDetailPanel();

        if (GameManager.Instance != null)
        {
            // GameManagerのイベントに登録
            GameManager.Instance.OnGameStateChanged += UpdatePhaseUI;
            GameManager.Instance.OnNewDay += UpdateDayUI;
            GameManager.Instance.OnPlayerModeChanged += UpdateTaskPanelHighlight;
            
            // 初期状態の反映
            UpdateDayUI(GameManager.Instance.CurrentDay);
            UpdatePhaseUI(GameManager.Instance.CurrentGameState);
        }

        // SelectionManagerのイベントに登録
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnSelectionChanged += OnSelectionChanged;
        }

        // ResourceManagerのイベントに登録
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += UpdateResourceUI;
            // 初期値の反映
            UpdateAllResourceUI();
        }

        // タスクパネルボタンのイベント接続
        if (axeButton != null)
        {
            axeButton.onClick.AddListener(OnAxeButtonClicked);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelButtonClicked);
        }
        if (stockpileButton != null)
        {
            stockpileButton.onClick.AddListener(OnStockpileButtonClicked);
        }
        if (kamaButton != null)
        {
            kamaButton.onClick.AddListener(OnKamaButtonClicked);
        }
        if (farmButton != null)
        {
            farmButton.onClick.AddListener(OnFarmButtonClicked);
        }
    }

    private void AddParentToHideList(Button btn)
    {
        if (btn != null && btn.transform.parent != null)
        {
            GameObject parentObj = btn.transform.parent.gameObject;
            if (!uiElementsToHide.Contains(parentObj))
            {
                uiElementsToHide.Add(parentObj);
            }
        }
    }

    private void OnDestroy()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnSelectionChanged -= OnSelectionChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= UpdatePhaseUI;
            GameManager.Instance.OnNewDay -= UpdateDayUI;
            GameManager.Instance.OnPlayerModeChanged -= UpdateTaskPanelHighlight;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= UpdateResourceUI;
        }

        if (axeButton != null) axeButton.onClick.RemoveListener(OnAxeButtonClicked);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancelButtonClicked);
        if (stockpileButton != null) stockpileButton.onClick.RemoveListener(OnStockpileButtonClicked);
        if (kamaButton != null) kamaButton.onClick.RemoveListener(OnKamaButtonClicked);
        if (farmButton != null) farmButton.onClick.RemoveListener(OnFarmButtonClicked);
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

    // ==================== Task Panel Buttons ====================

    private void OnAxeButtonClicked()
    {
        if (GameManager.Instance == null) return;

        // 既にGatheringモードならNormalに戻す（トグル）
        if (GameManager.Instance.CurrentPlayerMode == PlayerMode.Gathering)
        {
            GameManager.Instance.SetPlayerMode(PlayerMode.Normal);
        }
        else
        {
            GameManager.Instance.SetPlayerMode(PlayerMode.Gathering);
        }
    }

    private void OnCancelButtonClicked()
    {
        if (GameManager.Instance == null) return;

        // 既にCancellingモードならNormalに戻す（トグル）
        if (GameManager.Instance.CurrentPlayerMode == PlayerMode.Cancelling)
        {
            GameManager.Instance.SetPlayerMode(PlayerMode.Normal);
        }
        else
        {
            GameManager.Instance.SetPlayerMode(PlayerMode.Cancelling);
        }
    }

    private void OnStockpileButtonClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPlayerMode(
                GameManager.Instance.CurrentPlayerMode == PlayerMode.StockpileZoning ? PlayerMode.Normal : PlayerMode.StockpileZoning
            );
        }
    }

    private void OnKamaButtonClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPlayerMode(
                GameManager.Instance.CurrentPlayerMode == PlayerMode.Cutting ? PlayerMode.Normal : PlayerMode.Cutting
            );
        }
    }

    private void OnFarmButtonClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPlayerMode(
                GameManager.Instance.CurrentPlayerMode == PlayerMode.Picking ? PlayerMode.Normal : PlayerMode.Picking
            );
        }
    }

    /// <summary>
    /// PlayerModeが変化したときにボタンのハイライトを更新
    /// </summary>
    private void UpdateTaskPanelHighlight(PlayerMode mode)
    {
        if (axeButton != null) axeButton.GetComponent<Image>().color = (mode == PlayerMode.Gathering) ? activeButtonColor : normalButtonColor;
        if (cancelButton != null) cancelButton.GetComponent<Image>().color = (mode == PlayerMode.Cancelling) ? activeButtonColor : normalButtonColor;
        if (stockpileButton != null) stockpileButton.GetComponent<Image>().color = (mode == PlayerMode.StockpileZoning) ? activeButtonColor : normalButtonColor;
        if (kamaButton != null) kamaButton.GetComponent<Image>().color = (mode == PlayerMode.Cutting) ? activeButtonColor : normalButtonColor;
        if (farmButton != null) farmButton.GetComponent<Image>().color = (mode == PlayerMode.Picking) ? activeButtonColor : normalButtonColor;
    }

    // ==================== Detail Panel UI ====================

    private void CreateDetailPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[HUDManager] Canvas が見つからないため詳細パネルを生成できません");
            return;
        }

        // プレハブが設定されている場合はそれを使う
        if (detailPanelPrefab != null)
        {
            detailPanelObj = Instantiate(detailPanelPrefab, canvas.transform, false);
            
            // プレハブ内の要素を取得
            Transform nameTr = detailPanelObj.transform.Find("NameText");
            if (nameTr != null) detailNameText = nameTr.GetComponent<TextMeshProUGUI>();
            
            Transform descTr = detailPanelObj.transform.Find("DescText");
            if (descTr != null) detailDescText = descTr.GetComponent<TextMeshProUGUI>();
            
            Transform statsTr = detailPanelObj.transform.Find("StatsContainer");
            if (statsTr != null) detailStatsContainer = statsTr.gameObject;
            
            // プレハブ使用時も枠（Outline）を追加する
            if (detailPanelObj.GetComponent<Outline>() == null)
            {
                Outline outline = detailPanelObj.AddComponent<Outline>();
                outline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            detailPanelObj.SetActive(false);
            return;
        }

        // プレハブ未設定時: 動的に生成 (フォールバック)
        // 1. DetailPanel の作成
        detailPanelObj = new GameObject("DetailPanel");
        detailPanelObj.transform.SetParent(canvas.transform, false);

        RectTransform rtPanel = detailPanelObj.AddComponent<RectTransform>();
        rtPanel.anchorMin = new Vector2(1f, 0f);
        rtPanel.anchorMax = new Vector2(1f, 0f);
        rtPanel.pivot = new Vector2(1f, 0f);
        rtPanel.anchoredPosition = new Vector2(-20f, 20f);
        rtPanel.sizeDelta = new Vector2(300f, 220f);

        Image bgImage = detailPanelObj.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        Outline outline = detailPanelObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup layout = detailPanelObj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 15, 15);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(detailPanelObj.transform, false);
        detailNameText = nameObj.AddComponent<TextMeshProUGUI>();
        detailNameText.fontSize = 18f;
        detailNameText.fontStyle = FontStyles.Bold;
        detailNameText.color = new Color(1f, 0.85f, 0.4f, 1f);

        GameObject divider = new GameObject("Divider");
        divider.transform.SetParent(detailPanelObj.transform, false);
        Image divImg = divider.AddComponent<Image>();
        divImg.color = new Color(0.3f, 0.3f, 0.4f, 0.5f);
        RectTransform rtDiv = divider.GetComponent<RectTransform>();
        rtDiv.sizeDelta = new Vector2(0f, 2f);

        GameObject descObj = new GameObject("DescText");
        descObj.transform.SetParent(detailPanelObj.transform, false);
        detailDescText = descObj.AddComponent<TextMeshProUGUI>();
        detailDescText.fontSize = 12f;
        detailDescText.color = Color.white;
        detailDescText.enableWordWrapping = true;

        detailStatsContainer = new GameObject("StatsContainer");
        detailStatsContainer.transform.SetParent(detailPanelObj.transform, false);
        VerticalLayoutGroup statsLayout = detailStatsContainer.AddComponent<VerticalLayoutGroup>();
        statsLayout.spacing = 4f;
        statsLayout.childControlHeight = true;
        statsLayout.childControlWidth = true;
        statsLayout.childForceExpandHeight = false;
        statsLayout.childForceExpandWidth = true;

        detailPanelObj.SetActive(false);
    }

    private void OnSelectionChanged(ISelectable selectable)
    {
        if (selectable != null)
        {
            if (detailNameText != null) detailNameText.text = selectable.GetSelectionName();
            if (detailDescText != null) detailDescText.text = selectable.GetSelectionDescription();

            foreach (var txt in activeStatTexts)
            {
                if (txt != null) Destroy(txt.gameObject);
            }
            activeStatTexts.Clear();

            // プレハブ内に残っているダミーテキスト等も確実に削除する
            if (detailStatsContainer != null)
            {
                foreach (Transform child in detailStatsContainer.transform)
                {
                    Destroy(child.gameObject);
                }
            }

            var stats = selectable.GetSelectionStats();
            if (stats != null && detailStatsContainer != null)
            {
                foreach (var kvp in stats)
                {
                    GameObject statObj = new GameObject("Stat_" + kvp.Key);
                    statObj.transform.SetParent(detailStatsContainer.transform, false);
                    TextMeshProUGUI statText = statObj.AddComponent<TextMeshProUGUI>();
                    
                    // 日本語フォント（GenEiKiwamiGo SDFなど）を引き継ぐ
                    if (detailNameText != null)
                    {
                        statText.font = detailNameText.font;
                    }
                    
                    statText.fontSize = 12f;
                    statText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                    statText.text = $"• {kvp.Key}: <color=#FFFFFF>{kvp.Value}</color>";
                    activeStatTexts.Add(statText);
                }
            }

            // 指定されたUI要素をすべて非表示
            foreach (var ui in uiElementsToHide)
            {
                if (ui != null) ui.SetActive(false);
            }
            
            // 旧来のタスクパネルも念のため非表示
            if (taskPanelObject != null)
            {
                taskPanelObject.SetActive(false);
            }

            if (detailPanelObj != null) detailPanelObj.SetActive(true);
        }
        else
        {
            if (detailPanelObj != null) detailPanelObj.SetActive(false);

            // 指定されたUI要素を再表示
            foreach (var ui in uiElementsToHide)
            {
                if (ui != null) ui.SetActive(true);
            }

            // 旧来のタスクパネルも再表示
            if (taskPanelObject != null)
            {
                taskPanelObject.SetActive(true);
            }
        }
    }
}