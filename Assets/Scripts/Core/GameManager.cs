using UnityEngine;
using System;

/// <summary>
/// ゲーム全体の状態を管理するシングルトン。
/// 昼夜サイクル、日数、プレイヤーモードの切り替えを統括する。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    [SerializeField] private GameState currentGameState = GameState.Daytime;
    [SerializeField] private PlayerMode currentPlayerMode = PlayerMode.Normal;
    [SerializeField] private int currentDay = 1;

    // Properties
    public GameState CurrentGameState => currentGameState;
    public PlayerMode CurrentPlayerMode => currentPlayerMode;
    public int CurrentDay => currentDay;
    public bool IsDaytime => currentGameState == GameState.Daytime || currentGameState == GameState.Dawn;
    public bool IsNight => currentGameState == GameState.Night;

    // Events
    /// <summary>ゲーム状態が変化したとき</summary>
    public event Action<GameState> OnGameStateChanged;
    
    /// <summary>プレイヤーモードが変化したとき</summary>
    public event Action<PlayerMode> OnPlayerModeChanged;
    
    /// <summary>新しい日が始まったとき</summary>
    public event Action<int> OnNewDay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// ゲーム状態を変更する
    /// </summary>
    public void SetGameState(GameState newState)
    {
        if (currentGameState == newState) return;
        
        GameState previousState = currentGameState;
        currentGameState = newState;
        
        Debug.Log($"[GameManager] State changed: {previousState} -> {newState}");
        
        // 夜間に入ったら自動的に戦闘モードに切り替え
        if (newState == GameState.Night)
        {
            SetPlayerMode(PlayerMode.Combat);
        }
        // 昼間に入ったら通常モードに戻す
        else if (newState == GameState.Daytime)
        {
            SetPlayerMode(PlayerMode.Normal);
        }
        
        OnGameStateChanged?.Invoke(newState);
    }

    /// <summary>
    /// プレイヤーモードを変更する
    /// </summary>
    public void SetPlayerMode(PlayerMode newMode)
    {
        if (currentPlayerMode == newMode) return;
        
        PlayerMode previousMode = currentPlayerMode;
        currentPlayerMode = newMode;
        
        Debug.Log($"[GameManager] Player mode changed: {previousMode} -> {newMode}");
        OnPlayerModeChanged?.Invoke(newMode);
    }

    /// <summary>
    /// 建築モードをトグルする（昼間のみ可能）
    /// </summary>
    public void ToggleBuildMode()
    {
        if (!IsDaytime)
        {
            Debug.Log("[GameManager] Cannot enter build mode during night!");
            return;
        }

        if (currentPlayerMode == PlayerMode.Building)
        {
            SetPlayerMode(PlayerMode.Normal);
        }
        else
        {
            SetPlayerMode(PlayerMode.Building);
        }
    }

    /// <summary>
    /// 新しい日に進む
    /// </summary>
    public void AdvanceDay()
    {
        currentDay++;
        Debug.Log($"[GameManager] Day {currentDay} has begun!");
        OnNewDay?.Invoke(currentDay);
    }

    /// <summary>
    /// ゲームオーバー処理
    /// </summary>
    public void TriggerGameOver()
    {
        Debug.Log($"[GameManager] GAME OVER - Survived {currentDay} days!");
        SetGameState(GameState.GameOver);
    }
}
