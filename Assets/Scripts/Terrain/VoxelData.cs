using UnityEngine;

/// <summary>
/// ボクセル（ブロック）地形のメッシュ生成に必要な定数データ。
/// 頂点位置、面の頂点インデックス、法線方向、隣接チェック方向を定義。
/// ブロックサイズ: 1(X) × 2(Y) × 1(Z)
/// </summary>
public static class VoxelData
{
    // ブロックの物理サイズ
    public const float BlockWidth = 1f;
    public const float BlockHeight = 2f;
    public const float BlockDepth = 1f;

    // チャンクあたりのブロック数
    public const int ChunkWidth = 16;
    public const int ChunkHeight = 16;
    public const int ChunkDepth = 16;

    // テクスチャアトラスのタイル数
    public const int AtlasTileCount = 5; // GrassTop, Dirt, Stone, Sand, Water

    /// <summary>
    /// 1ブロックの8頂点の位置（ローカル座標）。
    /// Y軸だけBlockHeight(2)倍のスケール。
    /// </summary>
    public static readonly Vector3[] Vertices = new Vector3[8]
    {
        new Vector3(0, 0, 0),                    // 0: 左下奥
        new Vector3(BlockWidth, 0, 0),            // 1: 右下奥
        new Vector3(BlockWidth, BlockHeight, 0),  // 2: 右上奥
        new Vector3(0, BlockHeight, 0),           // 3: 左上奥
        new Vector3(0, 0, BlockDepth),            // 4: 左下前
        new Vector3(BlockWidth, 0, BlockDepth),   // 5: 右下前
        new Vector3(BlockWidth, BlockHeight, BlockDepth), // 6: 右上前
        new Vector3(0, BlockHeight, BlockDepth),  // 7: 左上前
    };

    /// <summary>
    /// 6面それぞれの4頂点インデックス。
    /// 順序は時計回り（Unity前面描画用）で、三角形は (0,1,2),(0,2,3) で生成。
    /// [面インデックス, 頂点番号(0-3)]
    /// 面: 0=Back(-Z), 1=Front(+Z), 2=Top(+Y), 3=Bottom(-Y), 4=Left(-X), 5=Right(+X)
    /// </summary>
    public static readonly int[,] FaceVertices = new int[6, 4]
    {
        { 0, 3, 2, 1 }, // Back  (-Z): 法線が-Zを向く面
        { 5, 6, 7, 4 }, // Front (+Z): 法線が+Zを向く面
        { 3, 7, 6, 2 }, // Top   (+Y): 法線が+Yを向く面
        { 1, 5, 4, 0 }, // Bottom(-Y): 法線が-Yを向く面
        { 4, 7, 3, 0 }, // Left  (-X): 法線が-Xを向く面
        { 1, 2, 6, 5 }, // Right (+X): 法線が+Xを向く面
    };

    /// <summary>
    /// 各面の法線ベクトル（隣接ブロックチェック方向）。
    /// </summary>
    public static readonly Vector3Int[] FaceNormals = new Vector3Int[6]
    {
        new Vector3Int(0, 0, -1),  // Back
        new Vector3Int(0, 0, 1),   // Front
        new Vector3Int(0, 1, 0),   // Top
        new Vector3Int(0, -1, 0),  // Bottom
        new Vector3Int(-1, 0, 0),  // Left
        new Vector3Int(1, 0, 0),   // Right
    };

    /// <summary>
    /// 1面のUV座標テンプレート。
    /// アトラスのタイル位置に応じてU座標をオフセットする。
    /// </summary>
    public static readonly Vector2[] BaseUVs = new Vector2[4]
    {
        new Vector2(0, 0),
        new Vector2(0, 1),
        new Vector2(1, 1),
        new Vector2(1, 0),
    };

    /// <summary>
    /// テクスチャアトラス内のタイルに対応するUV座標を返す。
    /// アトラスは横一列にタイルが並ぶ（1行N列）。
    /// </summary>
    public static Vector2[] GetFaceUVs(TextureTileIndex tileIndex)
    {
        float tileWidth = 1f / AtlasTileCount;
        float uOffset = (int)tileIndex * tileWidth;

        return new Vector2[4]
        {
            new Vector2(uOffset, 0),
            new Vector2(uOffset, 1),
            new Vector2(uOffset + tileWidth, 1),
            new Vector2(uOffset + tileWidth, 0),
        };
    }
}
