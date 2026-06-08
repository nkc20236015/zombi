using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Unityエディタメニューから "Tools > Create Animation Preview Scene" を実行すると、
/// NPCモデル・カメラ・UI・AnimationPreviewerが全てセットアップされた
/// プレビュー専用シーンを自動生成するエディタ拡張。
/// </summary>
public class AnimationPreviewBuilder
{
#if UNITY_EDITOR
    [MenuItem("Tools/Create Animation Preview Scene")]
    public static void CreatePreviewScene()
    {
        // ==================== 1. 新しい空のシーンを作成 ====================
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ==================== 2. 床（視覚的な参照用） ====================
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(2f, 1f, 2f);

        // 床を暗めのグレーに
        var floorRenderer = floor.GetComponent<Renderer>();
        if (floorRenderer != null)
        {
            Material floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (floorMat.shader.name == "Hidden/InternalErrorShader")
            {
                floorMat = new Material(Shader.Find("Standard"));
            }
            floorMat.color = new Color(0.25f, 0.25f, 0.28f);
            floorRenderer.material = floorMat;
        }

        // ==================== 3. NPCモデルを配置 ====================
        // CityPeople アセットの Male_Adult プレハブを使用
        string[] npcPrefabCandidates = new string[]
        {
            "Assets/CityPeople/Prefabs/Male_Adult/Male_Adult_ColorA.prefab",
            "Assets/CityPeople/Prefabs/Male_Adult/Male_Adult_CustomSkinA.prefab",
            "Assets/CityPeople/Prefabs/Female_Adult/Female_Adult_ColorA.prefab",
        };

        GameObject npcInstance = null;
        foreach (string path in npcPrefabCandidates)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                npcInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                npcInstance.name = "PreviewModel";
                Debug.Log($"[AnimationPreviewBuilder] NPC model loaded from: {path}");
                break;
            }
        }

        if (npcInstance == null)
        {
            // CityPeople が見つからなければ Crafter.FBX をフォールバック
            GameObject crafterFBX = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Animation/Crafting Mecanim Animation Pack/Characters/Crafter.FBX");
            if (crafterFBX != null)
            {
                npcInstance = (GameObject)PrefabUtility.InstantiatePrefab(crafterFBX);
                npcInstance.name = "PreviewModel";
                Debug.Log("[AnimationPreviewBuilder] Crafter.FBX model loaded as fallback.");
            }
        }

        if (npcInstance == null)
        {
            Debug.LogError("[AnimationPreviewBuilder] NPCモデルが見つかりませんでした。手動でモデルを配置してください。");
            // ダミーのカプセルを代替として配置
            npcInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcInstance.name = "PreviewModel (Placeholder)";
        }

        npcInstance.transform.position = Vector3.zero;
        npcInstance.transform.rotation = Quaternion.identity;

        // Animator コンポーネントを確保（PlayableGraph に必要）
        Animator animator = npcInstance.GetComponent<Animator>();
        if (animator == null)
        {
            animator = npcInstance.AddComponent<Animator>();
        }
        // Animator Controller は不要（PlayableGraph で直接再生するため）
        animator.runtimeAnimatorController = null;

        // Humanoid Avatar の設定（CityPeople の Avatar を使用）
        if (animator.avatar == null)
        {
            // FBX のモデルインポーターからアバターを自動検出
            string modelPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(npcInstance);
            if (!string.IsNullOrEmpty(modelPath))
            {
                ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (importer != null && importer.animationType == ModelImporterAnimationType.Human)
                {
                    // アバターは FBX 内に含まれるのでそのまま使用可能
                    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
                    foreach (var asset in assets)
                    {
                        if (asset is Avatar av)
                        {
                            animator.avatar = av;
                            Debug.Log($"[AnimationPreviewBuilder] Avatar set: {av.name}");
                            break;
                        }
                    }
                }
            }
        }

        // ==================== 4. カメラの調整 ====================
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(0f, 1.2f, 3.5f);
            mainCam.transform.rotation = Quaternion.Euler(10f, 180f, 0f);
            mainCam.backgroundColor = new Color(0.15f, 0.15f, 0.18f);
            mainCam.clearFlags = CameraClearFlags.SolidColor;
        }

        // ==================== 5. ライティング ====================
        // デフォルトの Directional Light を調整
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
            {
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1.2f;
                light.color = new Color(1f, 0.96f, 0.9f);
            }
        }

        // ==================== 6. Canvas & UI ====================
        // Canvas
        GameObject canvasGO = new GameObject("PreviewCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // EventSystem
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // --- ヘッダー: クリップ名 ---
        GameObject clipNameGO = CreateUIText(canvasGO.transform, "ClipNameText",
            new Vector2(0f, -40f), new Vector2(800f, 60f), "", 36, TextAlignmentOptions.Center);
        TextMeshProUGUI clipNameText = clipNameGO.GetComponent<TextMeshProUGUI>();

        // --- インデックス表示 ---
        GameObject indexGO = CreateUIText(canvasGO.transform, "IndexText",
            new Vector2(0f, -90f), new Vector2(400f, 40f), "", 24, TextAlignmentOptions.Center);
        TextMeshProUGUI indexText = indexGO.GetComponent<TextMeshProUGUI>();

        // --- クリップ情報 ---
        GameObject infoGO = CreateUIText(canvasGO.transform, "ClipInfoText",
            new Vector2(0f, -130f), new Vector2(600f, 40f), "", 20, TextAlignmentOptions.Center);
        TextMeshProUGUI clipInfoText = infoGO.GetComponent<TextMeshProUGUI>();
        clipInfoText.color = new Color(0.7f, 0.7f, 0.7f);

        // --- Prev / Next ボタン ---
        Button prevButton = CreateUIButton(canvasGO.transform, "PrevButton",
            new Vector2(-200f, 60f), new Vector2(160f, 50f), "◀ Prev (A)");
        Button nextButton = CreateUIButton(canvasGO.transform, "NextButton",
            new Vector2(200f, 60f), new Vector2(160f, 50f), "Next (D) ▶");

        // --- 速度スライダー ---
        GameObject speedLabelGO = CreateUIText(canvasGO.transform, "SpeedLabel",
            new Vector2(-250f, 130f), new Vector2(120f, 30f), "Speed:", 18, TextAlignmentOptions.Right);
        
        GameObject speedSliderGO = CreateUISlider(canvasGO.transform, "SpeedSlider",
            new Vector2(0f, 130f), new Vector2(300f, 30f));
        Slider speedSlider = speedSliderGO.GetComponent<Slider>();

        GameObject speedValueGO = CreateUIText(canvasGO.transform, "SpeedValueText",
            new Vector2(200f, 130f), new Vector2(100f, 30f), "1.0x", 18, TextAlignmentOptions.Left);
        TextMeshProUGUI speedText = speedValueGO.GetComponent<TextMeshProUGUI>();

        // --- ループトグル ---
        GameObject loopToggleGO = CreateUIToggle(canvasGO.transform, "LoopToggle",
            new Vector2(0f, 180f), new Vector2(200f, 30f), "Loop");
        Toggle loopToggle = loopToggleGO.GetComponent<Toggle>();

        // --- 操作説明 ---
        CreateUIText(canvasGO.transform, "HelpText",
            new Vector2(0f, 230f), new Vector2(600f, 30f),
            "← A / D → or ◀ Prev / Next ▶ to browse clips",
            16, TextAlignmentOptions.Center).GetComponent<TextMeshProUGUI>().color = new Color(0.5f, 0.5f, 0.5f);

        // ==================== 7. AnimationPreviewer コンポーネントの設定 ====================
        AnimationPreviewer previewer = npcInstance.AddComponent<AnimationPreviewer>();

        // リフレクションで SerializeField にアクセスして自動設定
        var previewerType = typeof(AnimationPreviewer);
        SetPrivateField(previewerType, previewer, "clipNameText", clipNameText);
        SetPrivateField(previewerType, previewer, "indexText", indexText);
        SetPrivateField(previewerType, previewer, "clipInfoText", clipInfoText);
        SetPrivateField(previewerType, previewer, "nextButton", nextButton);
        SetPrivateField(previewerType, previewer, "prevButton", prevButton);
        SetPrivateField(previewerType, previewer, "speedSlider", speedSlider);
        SetPrivateField(previewerType, previewer, "speedText", speedText);
        SetPrivateField(previewerType, previewer, "loopToggle", loopToggle);
        SetPrivateField(previewerType, previewer, "targetAnimator", animator);

        // ==================== 8. シーンの保存 ====================
        string scenePath = "Assets/_App/Scenes/AnimationPreview.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[AnimationPreviewBuilder] Preview scene created and saved to: {scenePath}");
        EditorUtility.DisplayDialog("Animation Preview Scene",
            $"プレビューシーンが作成されました！\n\n保存先: {scenePath}\n\nPlayボタンを押してアニメーションを確認してください。\n← A / D → キーでクリップを切り替えられます。",
            "OK");
    }

    // ==================== UI Helper Methods ====================

    private static GameObject CreateUIText(Transform parent, string name, Vector2 anchoredPos, Vector2 size, string text, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

    private static Button CreateUIButton(Transform parent, string name, Vector2 anchoredPos, Vector2 size, string label)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.35f);

        Button btn = go.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.3f, 0.3f, 0.35f);
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.5f);
        colors.pressedColor = new Color(0.25f, 0.25f, 0.3f);
        btn.colors = colors;

        // ボタンのラベルテキスト
        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    private static GameObject CreateUISlider(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
    {
        // Slider を手動で構築
        GameObject sliderGO = new GameObject(name);
        sliderGO.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderGO.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 1f);
        sliderRect.anchorMax = new Vector2(0.5f, 1f);
        sliderRect.pivot = new Vector2(0.5f, 1f);
        sliderRect.anchoredPosition = anchoredPos;
        sliderRect.sizeDelta = size;

        // Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        RectTransform bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.25f);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(5f, 0f);
        fillAreaRect.offsetMax = new Vector2(-5f, 0f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.45f, 0.65f, 0.9f);

        // Handle Slide Area
        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderGO.transform, false);
        RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 0f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        Slider slider = sliderGO.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 3f;
        slider.value = 1f;

        return sliderGO;
    }

    private static GameObject CreateUIToggle(Transform parent, string name, Vector2 anchoredPos, Vector2 size, string label)
    {
        GameObject toggleGO = new GameObject(name);
        toggleGO.transform.SetParent(parent, false);
        RectTransform toggleRect = toggleGO.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0.5f, 1f);
        toggleRect.anchorMax = new Vector2(0.5f, 1f);
        toggleRect.pivot = new Vector2(0.5f, 1f);
        toggleRect.anchoredPosition = anchoredPos;
        toggleRect.sizeDelta = size;

        // Background
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(toggleGO.transform, false);
        RectTransform bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.5f);
        bgRect.anchorMax = new Vector2(0f, 0.5f);
        bgRect.pivot = new Vector2(0f, 0.5f);
        bgRect.anchoredPosition = new Vector2(0f, 0f);
        bgRect.sizeDelta = new Vector2(24f, 24f);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.3f, 0.35f);

        // Checkmark
        GameObject checkmark = new GameObject("Checkmark");
        checkmark.transform.SetParent(bg.transform, false);
        RectTransform checkRect = checkmark.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.1f, 0.1f);
        checkRect.anchorMax = new Vector2(0.9f, 0.9f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;
        Image checkImg = checkmark.AddComponent<Image>();
        checkImg.color = new Color(0.45f, 0.85f, 0.45f);

        // Label
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(toggleGO.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(32f, 0f);
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI labelTmp = labelGO.AddComponent<TextMeshProUGUI>();
        labelTmp.text = label;
        labelTmp.fontSize = 18;
        labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        labelTmp.color = Color.white;

        Toggle toggle = toggleGO.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;
        toggle.isOn = true;

        return toggleGO;
    }

    private static void SetPrivateField(System.Type type, object target, string fieldName, object value)
    {
        var field = type.GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
        }
        else
        {
            Debug.LogWarning($"[AnimationPreviewBuilder] Field '{fieldName}' not found on {type.Name}");
        }
    }
#endif
}
