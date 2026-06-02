using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Zombi.UI
{
    /// <summary>
    /// UI要素にマウスホバーした際の説明パネル（ツールチップ）を表示・管理するシングルトン。
    /// </summary>
    public class TooltipManager : MonoBehaviour
    {
        public static TooltipManager Instance { get; private set; }

        [Header("UI References (Auto-generated if left empty)")]
        [SerializeField] private RectTransform tooltipPanel;
        [SerializeField] private TextMeshProUGUI tooltipText;

        [Header("Settings")]
        [SerializeField] private Vector2 offset = new Vector2(10f, -10f); // マウスからのオフセット
        [SerializeField] private float fontSize = 18f; // フォントサイズ
        [SerializeField] private Vector4 padding = new Vector4(12, 8, 12, 8); // パネルの余白 (Left, Top, Right, Bottom)
        [Tooltip("好きなフォントがあればヒエラルキーから設定できます")]
        public TMP_FontAsset customFont;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetupUI();
            HideTooltip();
        }

        void Update()
        {
            if (tooltipPanel != null && tooltipPanel.gameObject.activeSelf)
            {
                // マウスのスクリーン座標にツールチップを追従
                tooltipPanel.position = (Vector3)Input.mousePosition + (Vector3)offset;

                // 画面外にはみ出さないように調整 (GetWorldCornersを使うと正確)
                Vector3[] corners = new Vector3[4];
                tooltipPanel.GetWorldCorners(corners);
                // corners: 0=BottomLeft, 1=TopLeft, 2=TopRight, 3=BottomRight

                float screenWidth = Screen.width;
                float screenHeight = Screen.height;

                float shiftX = 0f;
                float shiftY = 0f;

                // 右端・左端のチェック
                if (corners[2].x > screenWidth) shiftX = screenWidth - corners[2].x;
                else if (corners[0].x < 0) shiftX = -corners[0].x;

                // 上端・下端のチェック
                if (corners[0].y < 0) shiftY = -corners[0].y;
                else if (corners[1].y > screenHeight) shiftY = screenHeight - corners[1].y;

                // はみ出ている場合は位置を補正
                if (shiftX != 0f || shiftY != 0f)
                {
                    tooltipPanel.position += new Vector3(shiftX, shiftY, 0f);
                }
            }
        }

        private void SetupUI()
        {
            if (tooltipPanel != null && tooltipText != null) return;

            // 1. Tooltip用のCanvas
            GameObject canvasObj = new GameObject("TooltipCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000; // CursorManagerより少し下、通常UIより上
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);

            // 2. パネル
            GameObject panelObj = new GameObject("TooltipPanel");
            tooltipPanel = panelObj.AddComponent<RectTransform>();
            tooltipPanel.SetParent(canvas.transform);
            tooltipPanel.pivot = new Vector2(0f, 1f); // 左上ピボット

            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // 黒い半透明背景
            panelImage.raycastTarget = false;

            // Layout Element（テキストサイズに自動で合わせる）
            ContentSizeFitter fitter = panelObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            HorizontalLayoutGroup layout = panelObj.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // 3. テキスト
            GameObject textObj = new GameObject("TooltipText");
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.SetParent(tooltipPanel);
            
            tooltipText = textObj.AddComponent<TextMeshProUGUI>();
            if (customFont != null) tooltipText.font = customFont;
            tooltipText.fontSize = fontSize;
            tooltipText.color = Color.white;
            tooltipText.raycastTarget = false;
            tooltipText.alignment = TextAlignmentOptions.Center;
            tooltipText.margin = padding;
        }

        /// <summary>
        /// ツールチップを表示する
        /// </summary>
        public void ShowTooltip(string content)
        {
            if (tooltipText != null)
            {
                tooltipText.text = content;
                LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel); // パネルのサイズを即座に更新
            }
            if (tooltipPanel != null)
            {
                tooltipPanel.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// ツールチップを非表示にする
        /// </summary>
        public void HideTooltip()
        {
            if (tooltipPanel != null)
            {
                tooltipPanel.gameObject.SetActive(false);
            }
        }
    }
}
