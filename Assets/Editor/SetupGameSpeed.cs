using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class SetupGameSpeed
{
    [MenuItem("Tools/Setup Game Speed Controller")]
    public static void Setup()
    {
        // 1. GameSpeedPanel を探す
        GameObject panel = GameObject.Find("GameSpeedPanel");
        Canvas mainCanvas = null;

        if (panel == null)
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                Transform found = FindChildRecursive(canvas.transform, "GameSpeedPanel");
                if (found != null)
                {
                    panel = found.gameObject;
                    mainCanvas = canvas;
                    break;
                }
            }
        }
        else
        {
            mainCanvas = panel.GetComponentInParent<Canvas>();
        }

        if (panel == null)
        {
            Debug.LogError("[SetupGameSpeed] GameSpeedPanel not found in scene!");
            return;
        }

        if (mainCanvas == null)
        {
            Debug.LogError("[SetupGameSpeed] Canvas not found!");
            return;
        }

        // 2. Controllerの追加
        GameSpeedController controller = panel.GetComponent<GameSpeedController>();
        if (controller == null)
        {
            controller = panel.AddComponent<GameSpeedController>();
            Debug.Log("[SetupGameSpeed] GameSpeedController added to GameSpeedPanel.");
        }

        // 3. Pause Overlay のエディタ内生成（フォント等を変えられるようにヒエラルキーに作成）
        Transform existingOverlay = FindChildRecursive(mainCanvas.transform, "PauseOverlay");
        GameObject pauseOverlay;
        if (existingOverlay != null)
        {
            pauseOverlay = existingOverlay.gameObject;
            Debug.Log("[SetupGameSpeed] PauseOverlay already exists in hierarchy.");
        }
        else
        {
            pauseOverlay = CreatePauseOverlay(mainCanvas.transform);
            Debug.Log("[SetupGameSpeed] PauseOverlay created in hierarchy.");
        }

        // 4. SerializeFieldの割り当て
        SerializedObject so = new SerializedObject(controller);
        SerializedProperty prop = so.FindProperty("pauseOverlay");
        if (prop != null)
        {
            prop.objectReferenceValue = pauseOverlay;
            so.ApplyModifiedProperties();
        }

        // 5. ボタンの確認
        string[] buttonNames = { "Stop", "Normal", "2Speed", "3Speed" };
        foreach (string name in buttonNames)
        {
            Transform btn = FindChildRecursive(panel.transform, name);
            if (btn != null)
            {
                var button = btn.GetComponent<Button>();
                if (button == null) Debug.LogWarning($"[SetupGameSpeed] {name} found but has no Button component!");
            }
            else
            {
                Debug.LogWarning($"[SetupGameSpeed] Button '{name}' not found under GameSpeedPanel!");
            }
        }

        EditorUtility.SetDirty(panel);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(panel.scene);
        Debug.Log("[SetupGameSpeed] Setup complete! You can now edit the PauseOverlay in the Canvas.");
    }

    private static GameObject CreatePauseOverlay(Transform canvasTransform)
    {
        // オーバーレイ本体
        GameObject overlay = new GameObject("PauseOverlay");
        overlay.transform.SetParent(canvasTransform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform rect = overlay.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        CanvasGroup cg = overlay.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // 上部グラデーション
        CreateEdgeImage(overlay.transform, "TopEdge", true);

        // 下部グラデーション
        CreateEdgeImage(overlay.transform, "BottomEdge", false);

        // テキスト背景
        GameObject textBg = new GameObject("PauseTextBG");
        textBg.transform.SetParent(overlay.transform, false);
        RectTransform textBgRect = textBg.AddComponent<RectTransform>();
        textBgRect.anchorMin = new Vector2(0.5f, 0.5f);
        textBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        textBgRect.sizeDelta = new Vector2(320f, 60f);
        textBgRect.anchoredPosition = Vector2.zero;

        Image textBgImage = textBg.AddComponent<Image>();
        textBgImage.color = new Color(0f, 0.08f, 0.25f, 0.65f);

        // テキスト
        GameObject textObj = new GameObject("PauseText");
        textObj.transform.SetParent(overlay.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(400f, 60f);
        textRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "一 時 停 止 中";
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.85f, 0.92f, 1f, 0.95f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = false;

        overlay.SetActive(false); // 初期非表示
        return overlay;
    }

    private static void CreateEdgeImage(Transform parent, string name, bool isTop)
    {
        GameObject edgeObj = new GameObject(name);
        edgeObj.transform.SetParent(parent, false);

        RectTransform rect = edgeObj.AddComponent<RectTransform>();

        if (isTop)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
        }
        rect.sizeDelta = new Vector2(0f, 140f);

        Image img = edgeObj.AddComponent<Image>();
        
        // 色設定用。テクスチャの動的生成は実行時に任せるか、単色にする
        // エディタ内でテクスチャを作ってアセット保存するのは冗長なので、色は白のまま、
        // 実際のグラデーション生成は GameSpeedController 側で（必要なら）やっても良いですが、
        // 今回は実行時にスプライトが割り当てられないので単色の半透明にしておきます。
        // もし必要なら GameSpeedController 側の実行時生成ロジックに任せます。
        Color edgeColor = new Color(0.15f, 0.35f, 0.85f, 0.5f);
        img.color = edgeColor;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }
}
