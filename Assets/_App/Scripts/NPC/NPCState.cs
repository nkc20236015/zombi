public enum NPCState
{
    Idle,
    Wandering,      // 目的もなく歩き回っている状態
    Moving,
    MovingToResource,
    Gathering,
    PuttingAway,    // ツールをしまっている最中
    Hauling,        // 落ちているアイテムを拾いに行く
    Carrying,       // アイテムを抱えて備蓄場へ運んでいる
    Working,
    Guarding,
    Fighting
}
