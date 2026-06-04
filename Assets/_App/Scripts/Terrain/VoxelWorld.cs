using UnityEngine;
using Unity.AI.Navigation;
using System.Collections.Generic;

/// <summary>
/// ワールド全体を管理するクラス。
/// Unity Terrain + Handpainted Grass & Ground Textures Pro Pack で地面を描画。
/// 内部にボクセルデータを保持し、将来の掘削機能に対応。
/// </summary>
[DefaultExecutionOrder(-50)]
public class VoxelWorld : MonoBehaviour
{
    public static VoxelWorld Instance { get; private set; }

    [Header("World Settings")]
    [SerializeField] private int worldWidthInBlocks = 150;
    [SerializeField] private int worldDepthInBlocks = 150;
    [SerializeField] private int worldHeightInBlocks = 8;

    [Header("Terrain Generation")]
    [SerializeField] private int surfaceBaseHeight = 4;
    [SerializeField] private float noiseScale = 0.02f;
    [SerializeField] private float noiseAmplitude = 0.015f; // Terrain heightmap上での起伏量
    [SerializeField] private int dirtLayerDepth = 3;
    [SerializeField] private int seed = 0;

    [Header("Terrain Textures")]
    [Tooltip("field_grass.png - メインの草原")]
    [SerializeField] private Texture2D fieldGrassDiffuse;
    [SerializeField] private Texture2D fieldGrassNormal;
    [SerializeField] private Texture2D fieldGrassMask;
    [Tooltip("forest_grass.png - 森の暗い草")]
    [SerializeField] private Texture2D forestGrassDiffuse;
    [SerializeField] private Texture2D forestGrassNormal;
    [SerializeField] private Texture2D forestGrassMask;
    [Tooltip("dirt.png - 土")]
    [SerializeField] private Texture2D dirtDiffuse;
    [SerializeField] private Texture2D dirtNormal;
    [SerializeField] private Texture2D dirtMask;
    [Tooltip("ground.png - 地面")]
    [SerializeField] private Texture2D groundDiffuse;
    [SerializeField] private Texture2D groundNormal;
    [SerializeField] private Texture2D groundMask;

    [Header("Splatmap Noise")]
    [SerializeField] private float splatNoiseScale = 0.04f;
    [SerializeField] private float splatDetailScale = 0.12f;
    [SerializeField] private float dirtPatchNoiseScale = 0.08f;
    [SerializeField] private float dirtPatchThreshold = 0.65f;

    [Header("NavMesh")]
    [SerializeField] private NavMeshSurface navMeshSurface;

    [Header("Trees (Procedural Generation)")]
    [SerializeField] private TreeSpeciesGroup[] treeSpeciesGroups;
    [SerializeField] private float treeDensityNoiseScale = 0.03f;
    [SerializeField] private float treeSpawnThreshold = 0.50f;
    [SerializeField] private float treeSpawnProbability = 0.08f;
    [SerializeField] private float speciesNoiseScale = 0.015f; // 種族ゾーンの大きさ
    [SerializeField] private int safeRadius = 10;

    [Header("Mushrooms")]
    [SerializeField] private GameObject mushroomPrefab;
    [SerializeField] private float mushroomSpawnProbability = 0.3f;

    // 内部ボクセルデータ（将来の掘削用に保持）
    private BlockType[,,] worldBlocks;

    // 生成されたTerrain
    private Terrain generatedTerrain;
    private TerrainData generatedTerrainData;

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
        // 既にエディタ上で生成されているかチェック（手動編集の保護）
        Transform existingTerrain = transform.Find("GameTerrain");
        if (existingTerrain != null)
        {
            generatedTerrain = existingTerrain.GetComponent<Terrain>();
            if (generatedTerrain != null)
            {
                generatedTerrainData = generatedTerrain.terrainData;
            }
            
            // 内部データだけ初期化しておく
            worldBlocks = new BlockType[worldWidthInBlocks, worldHeightInBlocks, worldDepthInBlocks];
            PopulateBlockData();
            Debug.Log("[VoxelWorld] 既存のTerrainを検出したため、自動生成をスキップしました（手動編集モード）。");
        }
        else
        {
            BuildWorld();
        }
    }

    // ==================== World Generation ====================

    [ContextMenu("Generate World")]
    public void BuildWorld()
    {
        if (GridManager.Instance != null) GridManager.Instance.InitializeGrid();
        DestroyExistingTerrain();

        // 内部ボクセルデータの生成（掘削用に保持）
        worldBlocks = new BlockType[worldWidthInBlocks, worldHeightInBlocks, worldDepthInBlocks];
        PopulateBlockData();

        // Unity Terrain を生成
        GenerateUnityTerrain();

        // 木のプロシージャル生成
        PopulateTrees();

        // NavMesh
        RefreshNavMesh();

        // シーンオブジェクトの再配置
        RepositionSceneObjects();

        Debug.Log($"[VoxelWorld] ワールド生成完了: {worldWidthInBlocks}x{worldDepthInBlocks}, Terrain使用");
    }

    // ==================== Terrain Generation ====================

    private void GenerateUnityTerrain()
    {
        // heightmap解像度はワールドサイズ+1（Terrainの仕様）
        int hmRes = Mathf.NextPowerOfTwo(Mathf.Max(worldWidthInBlocks, worldDepthInBlocks)) + 1;
        // パフォーマンスのため上限を設定
        hmRes = Mathf.Min(hmRes, 513);

        generatedTerrainData = new TerrainData();
        generatedTerrainData.heightmapResolution = hmRes;
        generatedTerrainData.size = new Vector3(worldWidthInBlocks, worldHeightInBlocks * VoxelData.BlockHeight, worldDepthInBlocks);
        generatedTerrainData.alphamapResolution = Mathf.Min(512, hmRes - 1);
        generatedTerrainData.baseMapResolution = 256;
        generatedTerrainData.SetDetailResolution(256, 8);

        // Heightmap: ほぼ平坦 + 小さな丘
        SetHeightmap(hmRes);

        // Terrain Layers の作成
        SetTerrainLayers();

        // Splatmap（テクスチャペイント）の設定
        PaintSplatmap();

        // Terrain GameObjectを作成
        GameObject terrainObj = Terrain.CreateTerrainGameObject(generatedTerrainData);
        terrainObj.name = "GameTerrain";
        terrainObj.transform.parent = transform;
        terrainObj.transform.position = Vector3.zero;
        terrainObj.layer = 8; // Groundレイヤー

        // エディタ上で保存・編集できるようにする
        terrainObj.hideFlags = HideFlags.None;

        generatedTerrain = terrainObj.GetComponent<Terrain>();
        // URP の Terrain Lit マテリアルを使用
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
        {
            generatedTerrain.materialTemplate = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;
        }

        // パフォーマンス設定
        generatedTerrain.heightmapPixelError = 5;
        generatedTerrain.basemapDistance = 300;
        generatedTerrain.drawInstanced = true;

        Debug.Log("[VoxelWorld] Unity Terrain を生成しました。");
    }

    private void SetHeightmap(int hmRes)
    {
        float[,] heights = new float[hmRes, hmRes];
        float baseNormalized = (float)surfaceBaseHeight / worldHeightInBlocks;

        float offsetX = seed * 0.7f;
        float offsetZ = seed * 1.3f;

        for (int z = 0; z < hmRes; z++)
        {
            for (int x = 0; x < hmRes; x++)
            {
                float nx = (float)x / hmRes * worldWidthInBlocks;
                float nz = (float)z / hmRes * worldDepthInBlocks;

                // 大きな波 + 小さな波でなだらかな丘を形成
                float noise1 = Mathf.PerlinNoise((nx + offsetX) * noiseScale, (nz + offsetZ) * noiseScale);
                float noise2 = Mathf.PerlinNoise((nx + offsetX) * noiseScale * 3f, (nz + offsetZ) * noiseScale * 3f);
                float combinedNoise = noise1 * 0.8f + noise2 * 0.2f;

                heights[z, x] = baseNormalized + combinedNoise * noiseAmplitude;
            }
        }

        generatedTerrainData.SetHeights(0, 0, heights);
    }

    private void SetTerrainLayers()
    {
        List<TerrainLayer> layers = new List<TerrainLayer>();

        // Layer 0: field_grass（ベース）
        layers.Add(CreateTerrainLayer("FieldGrass", fieldGrassDiffuse, fieldGrassNormal, fieldGrassMask, 5f));
        // Layer 1: forest_grass
        layers.Add(CreateTerrainLayer("ForestGrass", forestGrassDiffuse, forestGrassNormal, forestGrassMask, 5f));
        // Layer 2: dirt
        layers.Add(CreateTerrainLayer("Dirt", dirtDiffuse, dirtNormal, dirtMask, 5f));
        // Layer 3: ground
        layers.Add(CreateTerrainLayer("Ground", groundDiffuse, groundNormal, groundMask, 5f));

        generatedTerrainData.terrainLayers = layers.ToArray();
    }

    private TerrainLayer CreateTerrainLayer(string name, Texture2D diffuse, Texture2D normal, Texture2D mask, float tileSize)
    {
        TerrainLayer layer = new TerrainLayer();
        layer.name = name;
        if (diffuse != null) layer.diffuseTexture = diffuse;
        if (normal != null) layer.normalMapTexture = normal;
        // マスクマップ(Aチャンネル)によって地面がプラスチックのようにテカるのを防ぐため、意図的に無効化
        // if (mask != null) layer.maskMapTexture = mask;
        layer.tileSize = new Vector2(tileSize, tileSize);
        layer.tileOffset = Vector2.zero;
        layer.smoothness = 0f;
        layer.metallic = 0f;
        layer.normalScale = 1f;
        return layer;
    }

    private void PaintSplatmap()
    {
        int alphaRes = generatedTerrainData.alphamapResolution;
        int layerCount = generatedTerrainData.terrainLayers.Length;
        if (layerCount == 0) return;

        float[,,] splatmapData = new float[alphaRes, alphaRes, layerCount];

        float splatOffsetX = seed * 2.1f;
        float splatOffsetZ = seed * 3.7f;
        float dirtOffsetX = seed * 5.3f + 500f;
        float dirtOffsetZ = seed * 7.1f + 500f;

        for (int z = 0; z < alphaRes; z++)
        {
            for (int x = 0; x < alphaRes; x++)
            {
                float worldX = (float)x / alphaRes * worldWidthInBlocks;
                float worldZ = (float)z / alphaRes * worldDepthInBlocks;

                // 草のバリエーション（field_grass vs forest_grass）
                float grassNoise = Mathf.PerlinNoise(
                    (worldX + splatOffsetX) * splatNoiseScale,
                    (worldZ + splatOffsetZ) * splatNoiseScale
                );
                float grassDetail = Mathf.PerlinNoise(
                    (worldX + splatOffsetX) * splatDetailScale,
                    (worldZ + splatOffsetZ) * splatDetailScale
                );
                float grassBlend = grassNoise * 0.7f + grassDetail * 0.3f;

                // 土のパッチ
                float dirtNoise = Mathf.PerlinNoise(
                    (worldX + dirtOffsetX) * dirtPatchNoiseScale,
                    (worldZ + dirtOffsetZ) * dirtPatchNoiseScale
                );
                float dirtAmount = Mathf.Clamp01((dirtNoise - dirtPatchThreshold) / (1f - dirtPatchThreshold));
                dirtAmount *= dirtAmount; // 二乗でメリハリ

                // ブレンド計算
                float fieldGrassW = Mathf.Clamp01(1f - grassBlend * 1.5f);
                float forestGrassW = Mathf.Clamp01(grassBlend * 1.5f - 0.3f);
                float dirtW = dirtAmount * 0.6f;
                float groundW = dirtAmount * 0.3f;

                // 正規化
                float total = fieldGrassW + forestGrassW + dirtW + groundW;
                if (total > 0f)
                {
                    fieldGrassW /= total;
                    forestGrassW /= total;
                    dirtW /= total;
                    groundW /= total;
                }
                else
                {
                    fieldGrassW = 1f;
                }

                splatmapData[z, x, 0] = fieldGrassW;
                if (layerCount > 1) splatmapData[z, x, 1] = forestGrassW;
                if (layerCount > 2) splatmapData[z, x, 2] = dirtW;
                if (layerCount > 3) splatmapData[z, x, 3] = groundW;
            }
        }

        generatedTerrainData.SetAlphamaps(0, 0, splatmapData);
    }

    // ==================== Block Data (for future digging) ====================

    private void PopulateBlockData()
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
                int surfaceY = surfaceBaseHeight + Mathf.RoundToInt(noiseValue * 2f);
                surfaceY = Mathf.Clamp(surfaceY, 1, worldHeightInBlocks - 1);

                for (int y = 0; y < worldHeightInBlocks; y++)
                {
                    if (y > surfaceY) worldBlocks[x, y, z] = BlockType.Air;
                    else if (y == surfaceY) worldBlocks[x, y, z] = BlockType.Dirt_GrassTop;
                    else if (y > surfaceY - dirtLayerDepth) worldBlocks[x, y, z] = BlockType.Dirt;
                    else worldBlocks[x, y, z] = BlockType.Stone;
                }
            }
        }
    }

    // ==================== Trees ====================

    [System.Serializable]
    public class TreeSpeciesGroup
    {
        public string speciesName;
        public GameObject[] prefabs;
    }

    private void PopulateTrees()
    {
        if (treeSpeciesGroups == null || treeSpeciesGroups.Length == 0 || generatedTerrainData == null) return;

        // 全てのプレハブを集めてTreePrototypesに登録する
        List<TreePrototype> prototypes = new List<TreePrototype>();
        Dictionary<GameObject, int> prefabToPrototypeIndex = new Dictionary<GameObject, int>();

        foreach (var group in treeSpeciesGroups)
        {
            if (group.prefabs == null) continue;
            foreach (var prefab in group.prefabs)
            {
                if (prefab != null && !prefabToPrototypeIndex.ContainsKey(prefab))
                {
                    TreePrototype proto = new TreePrototype();
                    proto.prefab = prefab;
                    int index = prototypes.Count;
                    prototypes.Add(proto);
                    prefabToPrototypeIndex[prefab] = index;
                }
            }
        }
        generatedTerrainData.treePrototypes = prototypes.ToArray();

        HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();
        // 既存のResourceNodeの位置を登録（手動配置分など）
        ResourceNode[] existingNodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        foreach (var node in existingNodes)
        {
            int bx = Mathf.FloorToInt(node.transform.position.x);
            int bz = Mathf.FloorToInt(node.transform.position.z);
            occupiedPositions.Add(new Vector2Int(bx, bz));
        }

        Vector2Int center = new Vector2Int(worldWidthInBlocks / 2, worldDepthInBlocks / 2);

        // 既存の Trees 子オブジェクトを削除（アプローチ3では不要ですが念のため残骸を消す）
        Transform existingTreesParent = transform.Find("Trees");
        if (existingTreesParent != null) DestroyImmediate(existingTreesParent.gameObject);

        float forestOffsetX = seed * 5.1f + 1000f;
        float forestOffsetZ = seed * 7.3f + 1000f;

        // 種族ごとのノイズオフセット
        float[] speciesOffsetX = new float[treeSpeciesGroups.Length];
        float[] speciesOffsetZ = new float[treeSpeciesGroups.Length];
        for (int i = 0; i < treeSpeciesGroups.Length; i++)
        {
            speciesOffsetX[i] = seed * (i + 1) * 3.7f + i * 200f;
            speciesOffsetZ[i] = seed * (i + 1) * 2.3f + i * 300f;
        }

        int treeCount = 0;
        int mushroomCount = 0;
        List<TreeInstance> treeInstancesList = new List<TreeInstance>();

        GameObject mushroomParentObj = new GameObject("Mushrooms");
        Transform mushroomParent = mushroomParentObj.transform;
        mushroomParent.SetParent(transform);

        // Pre-load Mushroom Prefab in Editor Mode if missing
#if UNITY_EDITOR
        if (mushroomPrefab == null)
            mushroomPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Low Poly Mushrooms Pack/Prefabs/Mushrooms/MushroomGroup.prefab");
#endif

        for (int x = 0; x < worldWidthInBlocks; x++)
        {
            for (int z = 0; z < worldDepthInBlocks; z++)
            {
                if (Vector2Int.Distance(new Vector2Int(x, z), center) < safeRadius) continue;
                if (occupiedPositions.Contains(new Vector2Int(x, z))) continue;

                // 隣接チェック：8方向に木があるなら置かない
                bool hasNeighbor = false;
                for (int dx = -1; dx <= 1 && !hasNeighbor; dx++)
                {
                    for (int dz = -1; dz <= 1 && !hasNeighbor; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        if (occupiedPositions.Contains(new Vector2Int(x + dx, z + dz)))
                            hasNeighbor = true;
                    }
                }
                if (hasNeighbor) continue;

                // 森ノイズ
                float forestNoise = Mathf.PerlinNoise(
                    (x + forestOffsetX) * treeDensityNoiseScale,
                    (z + forestOffsetZ) * treeDensityNoiseScale
                );
                if (forestNoise <= treeSpawnThreshold) continue;
                if (Random.value >= treeSpawnProbability) continue;

                // 種族選択：各種族のノイズ値を比較し、最大値の種族を選ぶ
                int bestSpecies = 0;
                float bestNoise = -1f;
                for (int s = 0; s < treeSpeciesGroups.Length; s++)
                {
                    float sNoise = Mathf.PerlinNoise(
                        (x + speciesOffsetX[s]) * speciesNoiseScale,
                        (z + speciesOffsetZ[s]) * speciesNoiseScale
                    );
                    if (sNoise > bestNoise)
                    {
                        bestNoise = sNoise;
                        bestSpecies = s;
                    }
                }

                TreeSpeciesGroup group = treeSpeciesGroups[bestSpecies];
                if (group.prefabs == null || group.prefabs.Length == 0) continue;

                GameObject prefab = group.prefabs[Random.Range(0, group.prefabs.Length)];
                if (prefab == null || !prefabToPrototypeIndex.ContainsKey(prefab)) continue;

                int protoIndex = prefabToPrototypeIndex[prefab];

                // TerrainTreeInstanceを作成
                TreeInstance instance = new TreeInstance();
                // Terrainのローカル座標(0~1)に変換
                instance.position = new Vector3((x + 0.5f) / worldWidthInBlocks, 0f, (z + 0.5f) / worldDepthInBlocks);
                instance.prototypeIndex = protoIndex;
                instance.widthScale = 1f;
                instance.heightScale = 1f;
                instance.color = Color.white;
                instance.lightmapColor = Color.white;
                instance.rotation = Random.Range(0f, Mathf.PI * 2f);

                treeInstancesList.Add(instance);

                occupiedPositions.Add(new Vector2Int(x, z));
                treeCount++;

                // キノコ生成判定（木の周辺にスポーン）
                if (mushroomPrefab != null && Random.value < mushroomSpawnProbability)
                {
                    // 木の周囲の空いているマスを探す（簡易的に十字方向のいずれか）
                    Vector2Int[] offsets = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
                    Vector2Int offset = offsets[Random.Range(0, 4)];
                    Vector2Int mPos = new Vector2Int(x + offset.x, z + offset.y);
                    
                    if (mPos.x >= 0 && mPos.x < worldWidthInBlocks && mPos.y >= 0 && mPos.y < worldDepthInBlocks && !occupiedPositions.Contains(mPos))
                    {
                        float mX = mPos.x * VoxelData.BlockWidth + VoxelData.BlockWidth * 0.5f;
                        float mZ = mPos.y * VoxelData.BlockDepth + VoxelData.BlockDepth * 0.5f;
                        float mY = GetSurfaceWorldY(mX, mZ);

                        // マップ中心からのオフセットを加算
                        mX += transform.position.x;
                        mZ += transform.position.z;

                        GameObject mushroomObj = Instantiate(mushroomPrefab, new Vector3(mX, mY, mZ), Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                        mushroomObj.transform.SetParent(mushroomParent);
                        
                        ResourceNode rNode = mushroomObj.GetComponent<ResourceNode>();
                        if (rNode != null)
                        {
                            rNode.SetGridPosition(mPos);
                        }
                        
                        occupiedPositions.Add(mPos);
                        mushroomCount++;
                    }
                }
            }
        }

        generatedTerrainData.SetTreeInstances(treeInstancesList.ToArray(), true);

        // TerrainColliderを更新（木のコライダーを有効化するため）
        TerrainCollider tc = generatedTerrain.gameObject.GetComponent<TerrainCollider>();
        if (tc == null) tc = generatedTerrain.gameObject.AddComponent<TerrainCollider>();
        tc.terrainData = generatedTerrainData;

        Debug.Log($"[VoxelWorld] Terrain Tree の自動生成完了: {treeCount}本, キノコ生成完了: {mushroomCount}個");
    }

    // ==================== Terrain Tree Interaction ====================

    /// <summary>
    /// 指定されたインデックスのTerrain Treeを削除し、同じ場所に実体のGameObject（木）をスポーンして返す。
    /// </summary>
    public GameObject ConvertTerrainTreeToGameObject(int treeIndex)
    {
        if (generatedTerrainData == null || generatedTerrain == null) return null;
        var instances = generatedTerrainData.treeInstances;
        if (treeIndex < 0 || treeIndex >= instances.Length) return null;

        TreeInstance instance = instances[treeIndex];
        
        // プロトタイプからプレハブを取得
        if (instance.prototypeIndex < 0 || instance.prototypeIndex >= generatedTerrainData.treePrototypes.Length) return null;
        GameObject prefab = generatedTerrainData.treePrototypes[instance.prototypeIndex].prefab;

        // ワールド座標を計算
        Vector3 worldPos = Vector3.Scale(instance.position, generatedTerrainData.size) + generatedTerrain.transform.position;
        // Terrainの正確な高さを取得
        worldPos.y = GetSurfaceWorldY(worldPos.x, worldPos.z);

        // 実体化
        GameObject go = Instantiate(prefab, worldPos, Quaternion.Euler(0, instance.rotation * Mathf.Rad2Deg, 0));
        go.transform.parent = transform; // VoxelWorldの子に
        go.name = prefab.name + "_Spawned";

        // レイキャスト（クリック判定）に引っかかるよう、Resourceレイヤーに設定
        SetLayerRecursively(go, LayerMask.NameToLayer("Resource"));

        // TerrainDataから木を削除する（再構築）
        List<TreeInstance> newList = new List<TreeInstance>(instances);
        newList.RemoveAt(treeIndex);
        generatedTerrainData.treeInstances = newList.ToArray();

        // Terrainのコライダーを更新（少し重いが、木1本なら許容範囲）
        TerrainCollider tc = generatedTerrain.GetComponent<TerrainCollider>();
        if (tc != null) tc.terrainData = generatedTerrainData;

        return go;
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    // ==================== Scene Object Repositioning ====================

    private void RepositionSceneObjects()
    {
        float centerWorldX = worldWidthInBlocks * 0.5f;
        float centerWorldZ = worldDepthInBlocks * 0.5f;

        // NPC
        GameObject[] npcs = GameObject.FindGameObjectsWithTag("NPC");
        float npcOffset = 0f;
        foreach (var npc in npcs)
        {
            float targetX = centerWorldX + npcOffset;
            float targetZ = centerWorldZ + (npcOffset * 0.5f);
            float surfaceY = GetSurfaceWorldY(targetX, targetZ);
            Vector3 newPos = new Vector3(targetX, surfaceY, targetZ);

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
            Debug.Log($"[VoxelWorld] NPC '{npc.name}' を中央付近に再配置");
            npcOffset += 2f;
        }

        // Camera
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            float centerSurfaceY = GetSurfaceWorldY(centerWorldX, centerWorldZ);
            Vector3 camPos = mainCam.transform.position;
            mainCam.transform.position = new Vector3(centerWorldX, centerSurfaceY + 15f, centerWorldZ - 10f);
        }
    }

    // ==================== Public API ====================

    public BlockType GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= worldWidthInBlocks ||
            y < 0 || y >= worldHeightInBlocks ||
            z < 0 || z >= worldDepthInBlocks)
            return BlockType.Air;
        return worldBlocks[x, y, z];
    }

    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= worldWidthInBlocks ||
            y < 0 || y >= worldHeightInBlocks ||
            z < 0 || z >= worldDepthInBlocks)
            return;
        worldBlocks[x, y, z] = type;
    }

    public Vector3Int WorldPosToBlockCoord(Vector3 pos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(pos.x),
            Mathf.FloorToInt(pos.y / VoxelData.BlockHeight),
            Mathf.FloorToInt(pos.z)
        );
    }

    /// <summary>
    /// 指定ワールドXZ座標での地表面Y座標。Terrain.SampleHeightを使用。
    /// </summary>
    public float GetSurfaceWorldY(float worldX, float worldZ)
    {
        if (generatedTerrain != null)
        {
            return generatedTerrain.SampleHeight(new Vector3(worldX, 0, worldZ))
                   + generatedTerrain.transform.position.y;
        }
        // フォールバック
        return surfaceBaseHeight * VoxelData.BlockHeight;
    }

    /// <summary>
    /// 指定のグリッド座標（1x1ブロック範囲内）に木（TerrainTree）が存在するかどうかを返す。
    /// </summary>
    public bool HasTreeAt(Vector2Int gridPos)
    {
        if (generatedTerrainData == null) return false;

        float cellMinX = gridPos.x * VoxelData.BlockWidth;
        float cellMaxX = cellMinX + VoxelData.BlockWidth;
        float cellMinZ = gridPos.y * VoxelData.BlockDepth;
        float cellMaxZ = cellMinZ + VoxelData.BlockDepth;

        Vector3 terrainSize = generatedTerrainData.size;

        foreach (var tree in generatedTerrainData.treeInstances)
        {
            float treeX = tree.position.x * terrainSize.x;
            float treeZ = tree.position.z * terrainSize.z;

            // 木の中心座標がセルの範囲内に収まっているか判定
            if (treeX >= cellMinX && treeX < cellMaxX && treeZ >= cellMinZ && treeZ < cellMaxZ)
            {
                return true;
            }
        }

        // すでにGameObject(ResourceNode)化されている木がないかもチェックする
        ResourceNode[] nodes = Object.FindObjectsOfType<ResourceNode>();
        foreach(var node in nodes)
        {
            if (node.Type == ResourceType.Wood && node.HasResources)
            {
                float nx = node.transform.position.x;
                float nz = node.transform.position.z;
                if (nx >= cellMinX && nx < cellMaxX && nz >= cellMinZ && nz < cellMaxZ)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 指定グリッド（1x1ブロック）の地形ディテール（生えている草など）を消す。
    /// 備蓄場作成時などに使用し、地面のテクスチャ（スプラットマップ）は変更しない。
    /// </summary>
    public void ClearGrassAtGrid(Vector2Int gridPos)
    {
        if (generatedTerrainData == null) return;

        int detailRes = generatedTerrainData.detailResolution;
        int layerCount = generatedTerrainData.detailPrototypes.Length;
        if (layerCount == 0) return; // 生えている草（Detail）がない場合は何もしない

        // グリッド座標からワールド座標へ（ブロックサイズは1と仮定）
        float worldMinX = gridPos.x;
        float worldMaxX = gridPos.x + 1f;
        float worldMinZ = gridPos.y;
        float worldMaxZ = gridPos.y + 1f;

        // ワールド座標からディテールマップの座標へ
        int detailMinX = Mathf.FloorToInt(worldMinX / worldWidthInBlocks * detailRes);
        int detailMaxX = Mathf.CeilToInt(worldMaxX / worldWidthInBlocks * detailRes);
        int detailMinZ = Mathf.FloorToInt(worldMinZ / worldDepthInBlocks * detailRes);
        int detailMaxZ = Mathf.CeilToInt(worldMaxZ / worldDepthInBlocks * detailRes);

        // クランプ
        detailMinX = Mathf.Clamp(detailMinX, 0, detailRes - 1);
        detailMaxX = Mathf.Clamp(detailMaxX, 0, detailRes - 1);
        detailMinZ = Mathf.Clamp(detailMinZ, 0, detailRes - 1);
        detailMaxZ = Mathf.Clamp(detailMaxZ, 0, detailRes - 1);

        int width = detailMaxX - detailMinX;
        int height = detailMaxZ - detailMinZ;

        if (width <= 0 || height <= 0) return;

        // すべてのディテールレイヤー（grass02等の草）の密度を0にして消す
        for (int l = 0; l < layerCount; l++)
        {
            int[,] detailData = generatedTerrainData.GetDetailLayer(detailMinX, detailMinZ, width, height, l);
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    detailData[z, x] = 0;
                }
            }
            generatedTerrainData.SetDetailLayer(detailMinX, detailMinZ, l, detailData);
        }
    }

    // ==================== NavMesh ====================

    public void RefreshNavMesh()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("[VoxelWorld] NavMesh再ベイク完了");
        }
    }

    // ==================== Cleanup ====================

    [ContextMenu("Clear World")]
    private void DestroyExistingTerrain()
    {
        if (generatedTerrain != null)
        {
            DestroyImmediate(generatedTerrain.gameObject);
            generatedTerrain = null;
        }
        if (generatedTerrainData != null)
        {
            DestroyImmediate(generatedTerrainData, true);
            generatedTerrainData = null;
        }
        // 古いチャンクベースの子オブジェクトも削除
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }
}
