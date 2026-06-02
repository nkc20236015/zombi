using UnityEngine;
using UnityEngine.UI;

namespace Zombi.UI
{
    /// <summary>
    /// カスタムカーソルをUI (Canvas) で描画・管理するクラス。
    /// GameManagerの現在のモードに応じて、カーソルの右上にアイコン（斧やバツ印など）を追加表示する。
    /// </summary>
    public class CursorManager : MonoBehaviour
    {
        public static CursorManager Instance { get; private set; }

        [Header("Cursor Sprites")]
        [Tooltip("普段のマウスカーソル（白い矢印など）")]
        public Sprite normalCursorSprite;
        
        [Tooltip("伐採モード時の右上に表示するアイコン（斧など）")]
        public Sprite axeIconSprite;
        
        [Tooltip("キャンセルモード時の右上に表示するアイコン（赤いバツ印など）")]
        public Sprite cancelIconSprite;

        [Tooltip("備蓄場作成モード時の右上に表示するアイコン")]
        public Sprite stockpileIconSprite;

        [Header("UI References (Auto-generated if left empty)")]
        [SerializeField] private RectTransform cursorRoot;
        [SerializeField] private Image mainCursorImage;
        [SerializeField] private Image subIconImage;

        void Awake()
        {
            if (Instance != null && Instance != this) 
            { 
                Destroy(gameObject); 
                return; 
            }
            Instance = this;
            
            SetupUI();
        }

        void Start()
        {
            // OS標準のハードウェアカーソルを非表示にする
            Cursor.visible = false;
        }

        void Update()
        {
            // エディタ外に出た時などにカーソルが復活するのを防ぐため毎フレーム非表示を維持
            if (Cursor.visible) Cursor.visible = false;

            if (cursorRoot != null)
            {
                // マウスのスクリーン座標にUIを追従させる
                cursorRoot.position = Input.mousePosition;
            }

            UpdateCursorState();
        }

        /// <summary>
        /// 必要なCanvasやImageがアタッチされていない場合、自動生成する。
        /// </summary>
        private void SetupUI()
        {
            if (cursorRoot != null) return;

            // 1. カーソル描画用の最前面Canvasを作成
            GameObject canvasObj = new GameObject("CustomCursorCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // 他のUIより必ず手前に表示
            
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // シーン遷移してもカーソルが消えないようにする
            DontDestroyOnLoad(canvasObj);

            // 2. カーソルのルートオブジェクト（マウス位置に追従する本体）
            GameObject rootObj = new GameObject("CursorRoot");
            cursorRoot = rootObj.AddComponent<RectTransform>();
            cursorRoot.SetParent(canvas.transform);
            // マウスの先端（左上）をピボットにする
            cursorRoot.pivot = new Vector2(0f, 1f); 
            cursorRoot.sizeDelta = new Vector2(32f, 32f); // カーソルの基本サイズ

            // 3. メインカーソル画像
            GameObject mainObj = new GameObject("MainCursorImage");
            mainCursorImage = mainObj.AddComponent<Image>();
            mainCursorImage.rectTransform.SetParent(cursorRoot);
            mainCursorImage.rectTransform.anchorMin = Vector2.zero;
            mainCursorImage.rectTransform.anchorMax = Vector2.one;
            mainCursorImage.rectTransform.offsetMin = Vector2.zero;
            mainCursorImage.rectTransform.offsetMax = Vector2.zero;
            mainCursorImage.raycastTarget = false; // クリック判定をブロックしないようにする

            // 4. 右上のサブアイコン画像
            GameObject subObj = new GameObject("SubIconImage");
            subIconImage = subObj.AddComponent<Image>();
            subIconImage.rectTransform.SetParent(cursorRoot);
            
            // メインカーソルの右上に配置
            subIconImage.rectTransform.pivot = new Vector2(0f, 0f); // 左下ピボット
            subIconImage.rectTransform.anchorMin = new Vector2(1f, 1f); // 右上アンカー
            subIconImage.rectTransform.anchorMax = new Vector2(1f, 1f);
            subIconImage.rectTransform.anchoredPosition = new Vector2(-8f, -8f); // 少し重ねるように微調整
            subIconImage.rectTransform.sizeDelta = new Vector2(24f, 24f); // サブアイコンのサイズ
            subIconImage.raycastTarget = false;
        }

        /// <summary>
        /// GameManagerのモードに応じて、メインカーソルとサブアイコンの見た目を更新する。
        /// </summary>
        private void UpdateCursorState()
        {
            if (GameManager.Instance == null || mainCursorImage == null) return;

            // 普段のカーソルを設定
            mainCursorImage.sprite = normalCursorSprite;

            PlayerMode mode = GameManager.Instance.CurrentPlayerMode;

            if (mode == PlayerMode.Gathering)
            {
                subIconImage.gameObject.SetActive(true);
                subIconImage.sprite = axeIconSprite;
            }
            else if (mode == PlayerMode.Cancelling)
            {
                subIconImage.gameObject.SetActive(true);
                subIconImage.sprite = cancelIconSprite;
            }
            else if (mode == PlayerMode.StockpileZoning)
            {
                subIconImage.gameObject.SetActive(true);
                subIconImage.sprite = stockpileIconSprite;
            }
            else
            {
                // Normalなどの場合はサブアイコンを非表示
                subIconImage.gameObject.SetActive(false);
            }
        }
    }
}
