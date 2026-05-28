public enum NPCState
{
    Idle,
    Wandering,      // 目的もなく歩き回っている状態
    Moving,
    MovingToResource,
    Gathering,
    PuttingAway,    // ツールをしまっている最中
    Working,
    Guarding,
    Fighting
}
