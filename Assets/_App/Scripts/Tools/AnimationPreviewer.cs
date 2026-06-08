using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.Animations;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// アニメーションプレビューツール（ランタイム）。
/// PlayableGraph を使用し、Animator Controller を一切不要として
/// AnimationClip を直接再生する。
/// </summary>
public class AnimationPreviewer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI clipNameText;
    [SerializeField] private TextMeshProUGUI indexText;
    [SerializeField] private TextMeshProUGUI clipInfoText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button prevButton;
    [SerializeField] private Slider speedSlider;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private Toggle loopToggle;

    [Header("Animation Search Path")]
    [Tooltip("このフォルダ以下のアニメーションクリップを全て収集する")]
    [SerializeField] private string animationFolderPath = "Assets/Animation";

    [Header("Model")]
    [SerializeField] private Animator targetAnimator;

    // 収集されたクリップ一覧
    private List<AnimationClip> clips = new List<AnimationClip>();
    private int currentIndex = 0;

    // PlayableGraph（Animator Controller 不要でクリップを直接再生する仕組み）
    private PlayableGraph graph;
    private AnimationClipPlayable currentPlayable;

    void Start()
    {
        // ボタンイベントの登録
        if (nextButton != null) nextButton.onClick.AddListener(NextClip);
        if (prevButton != null) prevButton.onClick.AddListener(PrevClip);
        if (speedSlider != null)
        {
            speedSlider.minValue = 0f;
            speedSlider.maxValue = 3f;
            speedSlider.value = 1f;
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);
        }
        if (loopToggle != null)
        {
            loopToggle.isOn = true;
            loopToggle.onValueChanged.AddListener(OnLoopChanged);
        }

        // クリップ収集
        CollectClips();

        if (clips.Count > 0)
        {
            PlayClip(0);
        }
        else
        {
            if (clipNameText != null) clipNameText.text = "No clips found!";
        }
    }

    void Update()
    {
        // キーボードショートカット
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            NextClip();
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            PrevClip();
        }

        // 再生進捗の表示
        if (graph.IsValid() && currentPlayable.IsValid() && clipInfoText != null)
        {
            AnimationClip clip = clips[currentIndex];
            double time = currentPlayable.GetTime();
            float length = clip.length;
            float progress = length > 0 ? (float)(time % length) / length * 100f : 0f;
            clipInfoText.text = $"Length: {length:F2}s | Time: {time:F2}s | {progress:F0}% | Loop: {clip.isLooping}";
        }
    }

    void OnDestroy()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }

    // ==================== Clip Collection ====================

    private void CollectClips()
    {
        clips.Clear();

#if UNITY_EDITOR
        // エディタ実行時: AssetDatabase を使ってフォルダ内の全クリップを収集
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AnimationClip", new[] { animationFolderPath });
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);

            // FBX の場合は内包クリップを全て取得
            Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    // 重複チェック（同名クリップが別パスに存在する場合がある）
                    if (!clips.Exists(c => c.name == clip.name))
                    {
                        clips.Add(clip);
                    }
                }
            }
        }

        // 名前順でソート
        clips.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

        Debug.Log($"[AnimationPreviewer] {clips.Count} clips collected from '{animationFolderPath}'");
#else
        Debug.LogWarning("[AnimationPreviewer] This tool requires the Unity Editor to collect clips.");
#endif
    }

    // ==================== Playback ====================

    private void PlayClip(int index)
    {
        if (clips.Count == 0) return;

        currentIndex = index;
        AnimationClip clip = clips[currentIndex];

        // 既存のグラフを破棄
        if (graph.IsValid())
        {
            graph.Destroy();
        }

        // 新しい PlayableGraph を作成
        graph = PlayableGraph.Create("AnimationPreviewGraph");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        // AnimationClipPlayable を作成
        currentPlayable = AnimationClipPlayable.Create(graph, clip);

        // ループ設定
        bool shouldLoop = loopToggle != null ? loopToggle.isOn : true;
        currentPlayable.SetDuration(shouldLoop ? double.MaxValue : clip.length);

        // 再生速度
        float speed = speedSlider != null ? speedSlider.value : 1f;
        currentPlayable.SetSpeed(speed);

        // AnimationPlayableOutput を作成してAnimatorに接続
        var output = AnimationPlayableOutput.Create(graph, "Animation", targetAnimator);
        output.SetSourcePlayable(currentPlayable);

        // 再生開始
        graph.Play();

        // UI更新
        UpdateUI(clip);
    }

    private void UpdateUI(AnimationClip clip)
    {
        if (clipNameText != null)
        {
            clipNameText.text = clip.name;
        }
        if (indexText != null)
        {
            indexText.text = $"{currentIndex + 1} / {clips.Count}";
        }
        if (speedText != null)
        {
            float speed = speedSlider != null ? speedSlider.value : 1f;
            speedText.text = $"Speed: {speed:F1}x";
        }
    }

    // ==================== UI Callbacks ====================

    public void NextClip()
    {
        if (clips.Count == 0) return;
        int next = (currentIndex + 1) % clips.Count;
        PlayClip(next);
    }

    public void PrevClip()
    {
        if (clips.Count == 0) return;
        int prev = (currentIndex - 1 + clips.Count) % clips.Count;
        PlayClip(prev);
    }

    private void OnSpeedChanged(float value)
    {
        if (currentPlayable.IsValid())
        {
            currentPlayable.SetSpeed(value);
        }
        if (speedText != null)
        {
            speedText.text = $"Speed: {value:F1}x";
        }
    }

    private void OnLoopChanged(bool isOn)
    {
        // 現在のクリップを再再生（ループ設定を反映）
        PlayClip(currentIndex);
    }
}
