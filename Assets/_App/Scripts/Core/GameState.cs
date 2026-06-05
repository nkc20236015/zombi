/// <summary>
/// ゲームの状態を定義するenum
/// </summary>
public enum GameState
{
    /// <summary>昼間フェーズ — 物資収集・建築が可能</summary>
    Daytime,
    
    /// <summary>夕方 — 夜への移行期間、警告表示</summary>
    Evening,
    
    /// <summary>夜間フェーズ — ゾンビ襲来</summary>
    Night,
    
    /// <summary>夜明け — 昼への移行期間</summary>
    Dawn,
    
    /// <summary>ゲームオーバー</summary>
    GameOver,
    
    /// <summary>一時停止</summary>
    Paused
}

/// <summary>
/// プレイヤーの操作モードを定義するenum
/// </summary>
public enum PlayerMode
{
    /// <summary>通常モード（移動・探索）</summary>
    Normal,
    
    /// <summary>建築モード（ブロック設置）</summary>
    Building,
    
    /// <summary>伐採タスク登録モード（AxePanel押下後）</summary>
    Gathering,
    
    /// <summary>タスクキャンセルモード（CancelPanel押下後）</summary>
    Cancelling,
    
    /// <summary>備蓄場作成モード（エリアボタン押下後）</summary>
    StockpileZoning,
    
    /// <summary>キノコなどを切って収穫するモード（カマ・ナイフ）</summary>
    Cutting,
    
    /// <summary>キノコなどを採取するモード（素手）</summary>
    Picking
}
