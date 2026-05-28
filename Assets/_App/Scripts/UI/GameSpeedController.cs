using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// ゲーム速度を制御するコントローラー。
/// GameSpeedPanel内の4つのボタン（Stop, Normal, 2Speed, 3Speed）と
/// キーボードショートカット（Space, 1, 2, 3）でゲーム速度を変更する。
/// </summary>
public class GameSpeedController : MonoBehaviour
{
    [Header("Speed Buttons (auto-detected if empty)")]
    [SerializeField] private Button stopButton;
    [SerializeField] private Button normalButton;
    [SerializeField] private Button speed2Button;
    [SerializeField] private Button speed3Button;

    [Header("Pause Overlay (auto-created if empty)")]
    [SerializeField] private GameObject pauseOverlay;

    [Header("Settings")]
    [SerializeField] private Color activeButtonColor = new Color(0.3f, 0.7f, 1f, 1f);
    [SerializeField] private Color normalButtonColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color pauseEdgeColor = new Color(0.15f, 0.35f, 0.85f, 0.5f);

    private float currentSpeed = 1f;
    private float previousSpeed = 1f;
    private bool isPaused = false;
    private float defaultFixedDeltaTime;

    private CanvasGroup pauseCanvasGroup;
    private TextMeshProUGUI pauseText;

    private Image stopImage;
    private Image normalImage;
    private Image speed2Image;
    private Image speed3Image;

    private void Awake()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    private void Start()
    {
        FindButtons();
        SetupButtonListeners();
        SetupPauseOverlay();

        SetSpeed(1f);
    }

    private void OnDestroy()
    {
        if (stopButton != null) stopButton.onClick.RemoveListener(OnStopClicked);
        if (normalButton != null) normalButton.onClick.RemoveListener(OnNormalClicked);
        if (speed2Button != null) speed2Button.onClick.RemoveListener(OnSpeed2Clicked);
        if (speed3Button != null) speed3Button.onClick.RemoveListener(OnSpeed3Clicked);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
        else if (Input.GetKeyDown(KeyCode.Alpha1)) SetSpeed(1f);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SetSpeed(2f);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SetSpeed(3f);
    }

    private void FindButtons()
    {
        Transform panel = transform;
        if (panel.name != "GameSpeedPanel")
        {
            Transform found = FindChildRecursive(transform.root, "GameSpeedPanel");
            if (found != null) panel = found;
        }

        if (stopButton == null) stopButton = GetButton(panel, "Stop");
        if (normalButton == null) normalButton = GetButton(panel, "Normal");
        if (speed2Button == null) speed2Button = GetButton(panel, "2Speed");
        if (speed3Button == null) speed3Button = GetButton(panel, "3Speed");

        if (stopButton != null) stopImage = stopButton.GetComponent<Image>();
        if (normalButton != null) normalImage = normalButton.GetComponent<Image>();
        if (speed2Button != null) speed2Image = speed2Button.GetComponent<Image>();
        if (speed3Button != null) speed3Image = speed3Button.GetComponent<Image>();
    }

    private Button GetButton(Transform parent, string name)
    {
        Transform t = FindChildRecursive(parent, name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    private void SetupButtonListeners()
    {
        if (stopButton != null) stopButton.onClick.AddListener(OnStopClicked);
        if (normalButton != null) normalButton.onClick.AddListener(OnNormalClicked);
        if (speed2Button != null) speed2Button.onClick.AddListener(OnSpeed2Clicked);
        if (speed3Button != null) speed3Button.onClick.AddListener(OnSpeed3Clicked);
    }

    private void OnStopClicked() { TogglePause(); }
    private void OnNormalClicked() { SetSpeed(1f); }
    private void OnSpeed2Clicked() { SetSpeed(2f); }
    private void OnSpeed3Clicked() { SetSpeed(3f); }

    public void SetSpeed(float speed)
    {
        if (isPaused)
        {
            isPaused = false;
            ShowPauseOverlay(false);
        }

        currentSpeed = speed;
        previousSpeed = speed;
        Time.timeScale = speed;
        Time.fixedDeltaTime = defaultFixedDeltaTime * speed;

        UpdateButtonHighlights();
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = previousSpeed;
            Time.fixedDeltaTime = defaultFixedDeltaTime * previousSpeed;
            ShowPauseOverlay(false);
        }
        else
        {
            isPaused = true;
            previousSpeed = currentSpeed;
            Time.timeScale = 0f;
            ShowPauseOverlay(true);
        }

        UpdateButtonHighlights();
    }

    private void UpdateButtonHighlights()
    {
        SetButtonColor(stopImage, normalButtonColor);
        SetButtonColor(normalImage, normalButtonColor);
        SetButtonColor(speed2Image, normalButtonColor);
        SetButtonColor(speed3Image, normalButtonColor);

        if (isPaused)
        {
            SetButtonColor(stopImage, activeButtonColor);
        }
        else
        {
            if (Mathf.Approximately(currentSpeed, 1f))
                SetButtonColor(normalImage, activeButtonColor);
            else if (Mathf.Approximately(currentSpeed, 2f))
                SetButtonColor(speed2Image, activeButtonColor);
            else if (Mathf.Approximately(currentSpeed, 3f))
                SetButtonColor(speed3Image, activeButtonColor);
        }
    }

    private void SetButtonColor(Image img, Color color)
    {
        if (img == null) return;
        img.DOKill();
        img.DOColor(color, 0.15f).SetUpdate(true);
    }

    private void SetupPauseOverlay()
    {
        if (pauseOverlay != null)
        {
            // エディタスクリプトによって事前に生成されている場合
            pauseCanvasGroup = pauseOverlay.GetComponent<CanvasGroup>();
            Transform existingTextObj = FindChildRecursive(pauseOverlay.transform, "PauseText");
            if (existingTextObj != null) pauseText = existingTextObj.GetComponent<TextMeshProUGUI>();

            // グラデーションの再適用
            Transform existingTopEdge = FindChildRecursive(pauseOverlay.transform, "TopEdge");
            if (existingTopEdge != null) CreateGradientSprite(existingTopEdge.GetComponent<Image>(), true);

            Transform existingBottomEdge = FindChildRecursive(pauseOverlay.transform, "BottomEdge");
            if (existingBottomEdge != null) CreateGradientSprite(existingBottomEdge.GetComponent<Image>(), false);

            pauseOverlay.SetActive(false);
            return;
        }

        // --- 以下は実行時に生成する場合のフォールバック ---
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    canvas = c;
                    break;
                }
            }
        }
        if (canvas == null) return;

        pauseOverlay = new GameObject("PauseOverlay");
        pauseOverlay.transform.SetParent(canvas.transform, false);
        pauseOverlay.transform.SetAsLastSibling();

        RectTransform overlayRect = pauseOverlay.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        overlayRect.anchoredPosition = Vector2.zero;

        pauseCanvasGroup = pauseOverlay.AddComponent<CanvasGroup>();
        pauseCanvasGroup.alpha = 0f;
        pauseCanvasGroup.blocksRaycasts = false;
        pauseCanvasGroup.interactable = false;

        GameObject topEdge = new GameObject("TopEdge");
        topEdge.transform.SetParent(pauseOverlay.transform, false);
        RectTransform topRect = topEdge.AddComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.sizeDelta = new Vector2(0f, 140f);
        CreateGradientSprite(topEdge.AddComponent<Image>(), true);

        GameObject botEdge = new GameObject("BottomEdge");
        botEdge.transform.SetParent(pauseOverlay.transform, false);
        RectTransform botRect = botEdge.AddComponent<RectTransform>();
        botRect.anchorMin = new Vector2(0f, 0f);
        botRect.anchorMax = new Vector2(1f, 0f);
        botRect.pivot = new Vector2(0.5f, 0f);
        botRect.sizeDelta = new Vector2(0f, 140f);
        CreateGradientSprite(botEdge.AddComponent<Image>(), false);

        GameObject textBg = new GameObject("PauseTextBG");
        textBg.transform.SetParent(pauseOverlay.transform, false);
        RectTransform textBgRect = textBg.AddComponent<RectTransform>();
        textBgRect.anchorMin = new Vector2(0.5f, 0.5f);
        textBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        textBgRect.sizeDelta = new Vector2(320f, 60f);
        textBgRect.anchoredPosition = Vector2.zero;
        Image textBgImage = textBg.AddComponent<Image>();
        textBgImage.color = new Color(0f, 0.08f, 0.25f, 0.65f);

        GameObject textObj = new GameObject("PauseText");
        textObj.transform.SetParent(pauseOverlay.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(400f, 60f);
        textRect.anchoredPosition = Vector2.zero;
        pauseText = textObj.AddComponent<TextMeshProUGUI>();
        pauseText.text = "一 時 停 止 中";
        pauseText.fontSize = 36;
        pauseText.alignment = TextAlignmentOptions.Center;
        pauseText.color = new Color(0.85f, 0.92f, 1f, 0.95f);
        pauseText.fontStyle = FontStyles.Bold;
        pauseText.enableWordWrapping = false;

        pauseOverlay.SetActive(false);
    }

    private void CreateGradientSprite(Image img, bool isTop)
    {
        if (img == null) return;
        img.color = Color.white;

        Texture2D gradTex = new Texture2D(1, 64);
        gradTex.wrapMode = TextureWrapMode.Clamp;
        gradTex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < 64; y++)
        {
            float t = (float)y / 63f;
            float alpha = isTop ? t : (1f - t);
            gradTex.SetPixel(0, y, new Color(pauseEdgeColor.r, pauseEdgeColor.g, pauseEdgeColor.b, pauseEdgeColor.a * alpha));
        }
        gradTex.Apply();

        img.sprite = Sprite.Create(gradTex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
    }

    private void ShowPauseOverlay(bool show)
    {
        if (pauseOverlay == null) return;

        if (show)
        {
            pauseOverlay.SetActive(true);
            pauseOverlay.transform.SetAsLastSibling();

            pauseCanvasGroup.DOKill();
            pauseCanvasGroup.alpha = 0f;
            pauseCanvasGroup.DOFade(1f, 0.25f).SetUpdate(true);

            if (pauseText != null)
            {
                pauseText.transform.DOKill();
                pauseText.transform.localScale = Vector3.one;
                pauseText.transform.DOScale(1.04f, 1.2f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true);
            }
        }
        else
        {
            if (pauseText != null)
            {
                pauseText.transform.DOKill();
                pauseText.transform.localScale = Vector3.one;
            }

            pauseCanvasGroup.DOKill();
            pauseCanvasGroup.DOFade(0f, 0.15f)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (pauseOverlay != null)
                        pauseOverlay.SetActive(false);
                });
        }
    }
}
