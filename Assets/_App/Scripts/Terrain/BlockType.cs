/// <summary>
/// ブロックの種類を定義するenum。
/// 各ブロックは面ごとに異なるテクスチャを持てる（例: Dirt_GrassTop は上面が草、側面が土）。
/// </summary>
public enum BlockType : byte
{
    Air = 0,           // 空気（描画なし）
    Dirt_GrassTop = 1, // 表面の土（上面=草テクスチャ、側面/底面=土テクスチャ）
    Dirt = 2,          // 地中の土（全面土テクスチャ）
    Stone = 3,         // 岩盤（全面石テクスチャ）
    Sand = 4,          // 砂（全面砂テクスチャ）
    Water = 5          // 水（全面水色）※将来の川用プレースホルダー
}

/// <summary>
/// テクスチャアトラス内のタイルインデックス。
/// アトラスは横一列にタイルが並ぶ構成。
/// </summary>
public enum TextureTileIndex : byte
{
    GrassTop = 0,  // 草（上面用）
    Dirt = 1,      // 土
    Stone = 2,     // 石
    Sand = 3,      // 砂
    Water = 4      // 水（単色水色）
}

/// <summary>
/// ブロックタイプごとの面テクスチャ情報を提供するヘルパー。
/// </summary>
public static class BlockData
{
    /// <summary>
    /// 指定ブロックの指定面に対応するテクスチャタイルインデックスを返す。
    /// faceIndex: 0=Back, 1=Front, 2=Top, 3=Bottom, 4=Left, 5=Right
    /// </summary>
    public static TextureTileIndex GetTextureTile(BlockType type, int faceIndex)
    {
        switch (type)
        {
            case BlockType.Dirt_GrassTop:
                // 上面(2)だけ草、それ以外は土
                return faceIndex == 2 ? TextureTileIndex.GrassTop : TextureTileIndex.Dirt;

            case BlockType.Dirt:
                return TextureTileIndex.Dirt;

            case BlockType.Stone:
                return TextureTileIndex.Stone;

            case BlockType.Sand:
                return TextureTileIndex.Sand;

            case BlockType.Water:
                return TextureTileIndex.Water;

            default:
                return TextureTileIndex.Dirt;
        }
    }

    /// <summary>
    /// ブロックが固体（描画対象）かどうか。
    /// </summary>
    public static bool IsSolid(BlockType type)
    {
        return type != BlockType.Air;
    }

    /// <summary>
    /// ブロックが透過性を持つか（隣接面を描画するべきか）。
    /// Water は半透明なので、隣の固体ブロック面は描画する必要がある。
    /// </summary>
    public static bool IsTransparent(BlockType type)
    {
        return type == BlockType.Air || type == BlockType.Water;
    }
}
