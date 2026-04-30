/// <summary>
/// ゲーム内で管理する資源の種類。
/// Phase 2以降で種類を追加する場合はここに列挙する。
/// </summary>
public enum ResourceType
{
    Wood,   // 木材 - 木から採取
    Stone,  // 石材 - 岩から採取
    Food    // 食料 - 将来的に農業や狩猟から
}
