using UnityEngine;
using Unity.AI.Navigation;

/// <summary>
/// ワールド全体のChunk管理クラス。
/// Perlinノイズで地形を生成し、チャンク分割してメッシュを構築する。
/// マップサイズ: 40x40、深さ6層のブロック地形。
/// </summary>
[DefaultExecutionOrder(-50)]
public class VoxelWorld : MonoBehaviour
{
    public static VoxelWorld Instance { get; private set; }

    [Header("World Settings")]
    [SerializeField] private int worldWidthInBlocks = 40;
    [SerializeField] private int worldDepthInBlocks = 40;
    [SerializeField] private int worldHeightInBlocks = 8;

    [Header("Terrain Generation")]
    [SerializeField] private int surfaceBaseHeight = 4;
    [SerializeField] private float noiseScale = 0.08f;
    [SerializeField] private int noiseAmplitude = 2;
    [SerializeField] private int dirtLayerDepth = 3;
    [SerializeField] private int seed = 0;

    [Header("Material")]
    [SerializeField] private Material terrainMaterial;

    [Header("NavMesh")]
    [SerializeField] private NavMeshSurface navMeshSurface;

    private BlockType[,,] worldBlocks;
    private VoxelChunk[,,] chunks;
    private int chunksX, chunksY, chunksZ;

    public int WorldWidth => worldWidthInBlocks;
    public int WorldDepth => worldDepthInBlocks;
    public int WorldHeight => worldHeightInBlocks;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (terrainMaterial == null)
        {
            BuildFallbackMaterial();
        }
        BuildWorld();
    }

    /// <summary>
    /// テクスチャアトラスが未設定時の仮マテリアル。単色タイルで表現。
    /// </summary>
    private void BuildFallbackMaterial()
    {
        int tileSize = 64;
        int atlasWidth = tileSize * VoxelData.AtlasTileCount;
        Texture2D atlas = new Texture2D(atlasWidth, tileSize, TextureFormat.RGBA32, false);
        atlas.filterMode = FilterMode.Point;
        atlas.wrapMode = TextureWrapMode.Clamp;

        Color[] colors = new Color[]
        {
            new Color(0.35f, 0.65f, 0.15f), // GrassTop
            new Color(0.55f, 0.35f, 0.18f), // Dirt
            new Color(0.50f, 0.50f, 0.50f), // Stone
            new Color(0.85f, 0.78f, 0.55f), // Sand
            new Color(0.30f, 0.60f, 0.85f), // Water
        };

        for (int t = 0; t < VoxelData.AtlasTileCount; t++)
        {
            Color[] tile = new Color[tileSize * tileSize];
            for (int i = 0; i < tile.Length; i++) tile[i] = colors[t];
            atlas.SetPixels(t * tileSize, 0, tileSize, tileSize, tile);
        }
        atlas.Apply();

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        terrainMaterial = new Material(shader);
        terrainMaterial.mainTexture = atlas;
        terrainMaterial.name = "TerrainFallback";
        Debug.Log("[VoxelWorld] フォールバックマテリアルを生成しました。");
    }

    /// <summary>
    /// ワールド全体を生成する。
    /// </summary>
    public void BuildWorld()
    {
        DestroyAllChunks();
        worldBlocks = new BlockType[worldWidthInBlocks, worldHeightInBlocks, worldDepthInBlocks];
        PopulateTerrain();
        InstantiateChunks();
        RefreshNavMesh();
        RepositionSceneObjects();
        Debug.Log($"[VoxelWorld] ワールド生成完了: {worldWidthInBlocks}x{worldHeightInBlocks}x{worldDepthInBlocks}");
    }

    /// <summary>
    /// NPC、リソース、カメラなどのシーンオブジェクトを地形の表面に再配置する。
    /// </summary>
    private void RepositionSceneObjects()
    {
        // NPCを地形の上に移動（NavMeshAgent.Warpで確実にNavMesh上に配置）
        GameObject[] npcs = GameObject.FindGameObjectsWithTag("NPC");
        foreach (var npc in npcs)
        {
            Vector3 pos = npc.transform.position;
            float surfaceY = GetSurfaceWorldY(pos.x, pos.z);
            Vector3 newPos = new Vector3(pos.x, surfaceY, pos.z);

            var agent = npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
                npc.transform.position = newPos;
                agent.enabled = true;
                agent.Warp(newPos);
            }
            else
            {
                npc.transform.position = newPos;
            }
            Debug.Log($"[VoxelWorld] NPC '{npc.name}' をY={surfaceY}に再配置");
        }

        // リソースノードを地形の上に移動
        ResourceNode[] resources = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        foreach (var res in resources)
        {
            Vector3 pos = res.transform.position;
            float surfaceY = GetSurfaceWorldY(pos.x, pos.z);
            res.transform.position = new Vector3(pos.x, surfaceY, pos.z);
        }

        // メインカメラのターゲットY座標を地形に合わせる
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 camPos = mainCam.transform.position;
            float centerSurfaceY = GetSurfaceWorldY(
                worldWidthInBlocks * VoxelData.BlockWidth * 0.5f,
                worldDepthInBlocks * VoxelData.BlockDepth * 0.5f
            );
            mainCam.transform.position = new Vector3(camPos.x, centerSurfaceY + 15f, camPos.z);
        }
    }

    /// <summary>
    /// Perlinノイズで自然な地形を生成する。
    /// </summary>
    private void PopulateTerrain()
    {
        float offsetX = seed * 0.7f;
        float offsetZ = seed * 1.3f;

        for (int x = 0; x < worldWidthInBlocks; x++)
        {
            for (int z = 0; z < worldDepthInBlocks; z++)
            {
                float noiseValue = Mathf.PerlinNoise(
                    (x + offsetX) * noiseScale,
                    (z + offsetZ) * noiseScale
                );
                int surfaceY = surfaceBaseHeight + Mathf.RoundToInt(noiseValue * noiseAmplitude);
                surfaceY = Mathf.Clamp(surfaceY, 1, worldHeightInBlocks - 1);

                for (int y = 0; y < worldHeightInBlocks; y++)
                {
                    if (y > surfaceY)
                    {
                        worldBlocks[x, y, z] = BlockType.Air;
                    }
                    else if (y == surfaceY)
                    {
                        worldBlocks[x, y, z] = BlockType.Dirt_GrassTop;
                    }
                    else if (y > surfaceY - dirtLayerDepth)
                    {
                        worldBlocks[x, y, z] = BlockType.Dirt;
                    }
                    else
                    {
                        worldBlocks[x, y, z] = BlockType.Stone;
                    }
                }
            }
        }
    }

    /// <summary>
    /// チャンクを生成してメッシュを構築する。
    /// </summary>
    private void InstantiateChunks()
    {
        chunksX = Mathf.CeilToInt((float)worldWidthInBlocks / VoxelData.ChunkWidth);
        chunksY = Mathf.CeilToInt((float)worldHeightInBlocks / VoxelData.ChunkHeight);
        chunksZ = Mathf.CeilToInt((float)worldDepthInBlocks / VoxelData.ChunkDepth);

        chunks = new VoxelChunk[chunksX, chunksY, chunksZ];

        for (int cx = 0; cx < chunksX; cx++)
            for (int cy = 0; cy < chunksY; cy++)
                for (int cz = 0; cz < chunksZ; cz++)
                    MakeChunk(cx, cy, cz);
    }

    private void MakeChunk(int cx, int cy, int cz)
    {
        Vector3Int chunkBlockPos = new Vector3Int(
            cx * VoxelData.ChunkWidth,
            cy * VoxelData.ChunkHeight,
            cz * VoxelData.ChunkDepth
        );

        Vector3 worldPos = new Vector3(
            chunkBlockPos.x * VoxelData.BlockWidth,
            chunkBlockPos.y * VoxelData.BlockHeight,
            chunkBlockPos.z * VoxelData.BlockDepth
        );

        GameObject chunkObj = new GameObject($"Chunk_{cx}_{cy}_{cz}");
        chunkObj.transform.parent = transform;
        chunkObj.transform.position = worldPos;
        chunkObj.layer = 8; // Groundレイヤー

        chunkObj.AddComponent<MeshFilter>();
        chunkObj.AddComponent<MeshRenderer>();
        chunkObj.AddComponent<MeshCollider>();

        VoxelChunk chunk = chunkObj.AddComponent<VoxelChunk>();
        chunk.Initialize(this, chunkBlockPos, terrainMaterial);

        for (int x = 0; x < VoxelData.ChunkWidth; x++)
            for (int y = 0; y < VoxelData.ChunkHeight; y++)
                for (int z = 0; z < VoxelData.ChunkDepth; z++)
                {
                    int wx = chunkBlockPos.x + x;
                    int wy = chunkBlockPos.y + y;
                    int wz = chunkBlockPos.z + z;

                    if (wx < worldWidthInBlocks && wy < worldHeightInBlocks && wz < worldDepthInBlocks)
                        chunk.SetBlock(x, y, z, worldBlocks[wx, wy, wz]);
                }

        chunk.RebuildMesh();
        chunks[cx, cy, cz] = chunk;
    }

    /// <summary>
    /// ワールド座標(ブロック座標)のブロックを取得する。
    /// </summary>
    public BlockType GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= worldWidthInBlocks ||
            y < 0 || y >= worldHeightInBlocks ||
            z < 0 || z >= worldDepthInBlocks)
            return BlockType.Air;
        return worldBlocks[x, y, z];
    }

    /// <summary>
    /// ブロックを設定し、関連チャンクを再構築する。
    /// </summary>
    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= worldWidthInBlocks ||
            y < 0 || y >= worldHeightInBlocks ||
            z < 0 || z >= worldDepthInBlocks)
            return;

        worldBlocks[x, y, z] = type;

        int cx = x / VoxelData.ChunkWidth;
        int cy = y / VoxelData.ChunkHeight;
        int cz = z / VoxelData.ChunkDepth;

        if (chunks != null && cx < chunksX && cy < chunksY && cz < chunksZ)
        {
            VoxelChunk chunk = chunks[cx, cy, cz];
            if (chunk != null)
            {
                chunk.SetBlock(x % VoxelData.ChunkWidth, y % VoxelData.ChunkHeight, z % VoxelData.ChunkDepth, type);
                chunk.RebuildMesh();
            }

            // チャンク境界の隣接チャンクも再構築
            int lx = x % VoxelData.ChunkWidth;
            int ly = y % VoxelData.ChunkHeight;
            int lz = z % VoxelData.ChunkDepth;
            if (lx == 0 && cx > 0 && chunks[cx-1,cy,cz] != null) chunks[cx-1,cy,cz].RebuildMesh();
            if (lx == VoxelData.ChunkWidth-1 && cx < chunksX-1 && chunks[cx+1,cy,cz] != null) chunks[cx+1,cy,cz].RebuildMesh();
            if (ly == 0 && cy > 0 && chunks[cx,cy-1,cz] != null) chunks[cx,cy-1,cz].RebuildMesh();
            if (ly == VoxelData.ChunkHeight-1 && cy < chunksY-1 && chunks[cx,cy+1,cz] != null) chunks[cx,cy+1,cz].RebuildMesh();
            if (lz == 0 && cz > 0 && chunks[cx,cy,cz-1] != null) chunks[cx,cy,cz-1].RebuildMesh();
            if (lz == VoxelData.ChunkDepth-1 && cz < chunksZ-1 && chunks[cx,cy,cz+1] != null) chunks[cx,cy,cz+1].RebuildMesh();
        }
    }

    /// <summary>
    /// ワールド座標(float)からブロック座標(int)を取得。
    /// </summary>
    public Vector3Int WorldPosToBlockCoord(Vector3 pos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(pos.x / VoxelData.BlockWidth),
            Mathf.FloorToInt(pos.y / VoxelData.BlockHeight),
            Mathf.FloorToInt(pos.z / VoxelData.BlockDepth)
        );
    }

    /// <summary>
    /// 指定XZ座標での地表面のワールドY座標を返す。
    /// </summary>
    public float GetSurfaceWorldY(float worldX, float worldZ)
    {
        int bx = Mathf.Clamp(Mathf.FloorToInt(worldX / VoxelData.BlockWidth), 0, worldWidthInBlocks - 1);
        int bz = Mathf.Clamp(Mathf.FloorToInt(worldZ / VoxelData.BlockDepth), 0, worldDepthInBlocks - 1);

        for (int y = worldHeightInBlocks - 1; y >= 0; y--)
        {
            if (BlockData.IsSolid(worldBlocks[bx, y, bz]))
                return (y + 1) * VoxelData.BlockHeight;
        }
        return 0f;
    }

    /// <summary>
    /// NavMeshを再ベイクする。
    /// </summary>
    public void RefreshNavMesh()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("[VoxelWorld] NavMesh再ベイク完了");
        }
        else
        {
            Debug.LogWarning("[VoxelWorld] NavMeshSurfaceが未設定です。インスペクターで設定してください。");
        }
    }

    private void DestroyAllChunks()
    {
        if (chunks != null)
        {
            foreach (var chunk in chunks)
                if (chunk != null) Destroy(chunk.gameObject);
            chunks = null;
        }
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }
}
