using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Terrain Tree に対するホバー（ハイライト）、クリック、範囲選択を管理するクラス。
/// 木は普段 Terrain Data として軽量に描画され、クリック/選択時にのみ本物の GameObject（ResourceNode）へ変換される。
/// </summary>
public class TerrainTreeInteractManager : MonoBehaviour
{
    public static TerrainTreeInteractManager Instance { get; private set; }

    [Header("Highlight Settings")]
    [SerializeField] private Material highlightMaterialNormal;
    [SerializeField] private Material highlightMaterialGathering;
    [SerializeField] private Material highlightMaterialCancel;

    private VoxelWorld voxelWorld;
    private Terrain terrain;
    private TerrainCollider terrainCollider;
    private Camera mainCam;

    private int hoveredTreeIndex = -1;
    private GameObject highlightObject;
    private MeshFilter highlightMeshFilter;
    private MeshRenderer highlightMeshRenderer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        voxelWorld = VoxelWorld.Instance;
        mainCam = Camera.main;
        
        // ハイライト用オブジェクトの準備
        highlightObject = new GameObject("TreeHighlight");
        highlightMeshFilter = highlightObject.AddComponent<MeshFilter>();
        highlightMeshRenderer = highlightObject.AddComponent<MeshRenderer>();
        highlightObject.SetActive(false);

        Shader shader = Shader.Find("Sprites/Default");
        if (highlightMaterialNormal == null)
        {
            highlightMaterialNormal = new Material(shader);
            highlightMaterialNormal.color = new Color(1f, 1f, 1f, 0.3f);
        }
        if (highlightMaterialGathering == null)
        {
            highlightMaterialGathering = new Material(shader);
            highlightMaterialGathering.color = new Color(1f, 0.9f, 0.2f, 0.4f);
        }
        if (highlightMaterialCancel == null)
        {
            highlightMaterialCancel = new Material(shader);
            highlightMaterialCancel.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        }
    }

    void Update()
    {
        if (terrain == null || terrainCollider == null)
        {
            if (voxelWorld != null) terrain = voxelWorld.GetComponentInChildren<Terrain>();
            if (terrain == null) terrain = FindAnyObjectByType<Terrain>();
            
            if (terrain != null) terrainCollider = terrain.GetComponent<TerrainCollider>();
            return;
        }

        // ホバー処理（常に実行）
        UpdateHover();
    }

    private void UpdateHover()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            ClearHover();
            return;
        }

        // 建築モード中は木をハイライトしない
        if (GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Building)
        {
            ClearHover();
            return;
        }

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (terrainCollider.Raycast(ray, out RaycastHit hit, 200f))
        {
            // ヒット地点周辺の木を取得（カメラ角度によるズレを考慮し、広めの半径で取得）
            int closestIndex = FindClosestTreeIndexScreenSpace(Input.mousePosition, hit.point, 15f, 50f);
            if (closestIndex != -1)
            {
                if (hoveredTreeIndex != closestIndex)
                {
                    hoveredTreeIndex = closestIndex;
                    UpdateHighlightMesh(closestIndex);
                }
                
                // モードに応じてマテリアルを切り替え
                Material selectedMat = highlightMaterialNormal;
                if (GameManager.Instance != null)
                {
                    if (GameManager.Instance.CurrentPlayerMode == PlayerMode.Gathering) selectedMat = highlightMaterialGathering;
                    else if (GameManager.Instance.CurrentPlayerMode == PlayerMode.Cancelling) selectedMat = highlightMaterialCancel;
                }

                // 全サブメッシュにマテリアルを適用
                if (highlightMeshFilter.sharedMesh != null)
                {
                    Material[] mats = new Material[highlightMeshFilter.sharedMesh.subMeshCount];
                    for (int i = 0; i < mats.Length; i++) mats[i] = selectedMat;
                    highlightMeshRenderer.sharedMaterials = mats;
                }

                highlightObject.SetActive(true);
            }
            else
            {
                ClearHover();
            }
        }
        else
        {
            ClearHover();
        }
    }

    private void ClearHover()
    {
        hoveredTreeIndex = -1;
        if (highlightObject.activeSelf) highlightObject.SetActive(false);
    }

    private void UpdateHighlightMesh(int index)
    {
        var instances = terrain.terrainData.treeInstances;
        if (index < 0 || index >= instances.Length) return;

        TreeInstance instance = instances[index];
        GameObject prefab = terrain.terrainData.treePrototypes[instance.prototypeIndex].prefab;

        // ハイライト用のメッシュを探す（LOD0のメッシュ）
        Mesh targetMesh = null;
        var meshFilters = prefab.GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length > 0) targetMesh = meshFilters[0].sharedMesh;
        else
        {
            var skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skinned.Length > 0) targetMesh = skinned[0].sharedMesh;
        }

        if (targetMesh != null)
        {
            highlightMeshFilter.sharedMesh = targetMesh;
            
            // ワールド座標と回転を合わせる
            Vector3 worldPos = Vector3.Scale(instance.position, terrain.terrainData.size) + terrain.transform.position;
            highlightObject.transform.position = worldPos;
            highlightObject.transform.rotation = Quaternion.Euler(0, instance.rotation * Mathf.Rad2Deg, 0);
            
            // プレハブがスケール0.5を持っているので、それに合わせる（Zファイティング防止のため僅かに大きくする）
            highlightObject.transform.localScale = prefab.transform.localScale * 1.02f;
        }
    }

    /// <summary>
    /// マウス位置からスクリーンスペースで最も近い木を探す（高速化のためワールドXZのラフな半径で絞り込む）
    /// </summary>
    public int FindClosestTreeIndexScreenSpace(Vector2 mousePos, Vector3 groundHitPos, float searchRadiusXZ, float maxScreenDist)
    {
        if (terrain == null || terrain.terrainData == null || mainCam == null) return -1;

        Vector3 terrainPos = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        
        // Terrainの正規化座標(0~1)に変換
        Vector3 normalizedPos = new Vector3(
            (groundHitPos.x - terrainPos.x) / size.x,
            0,
            (groundHitPos.z - terrainPos.z) / size.z
        );

        float normalizedRadius = searchRadiusXZ / size.x; 

        TreeInstance[] instances = terrain.terrainData.treeInstances;
        float minScreenDist = maxScreenDist;
        int bestIndex = -1;

        for (int i = 0; i < instances.Length; i++)
        {
            Vector3 pos = instances[i].position;
            // ラフな距離判定
            if (Mathf.Abs(pos.x - normalizedPos.x) > normalizedRadius || Mathf.Abs(pos.z - normalizedPos.z) > normalizedRadius)
                continue;

            // ワールド座標に戻す
            Vector3 worldPos = new Vector3(pos.x * size.x, pos.y * size.y, pos.z * size.z) + terrainPos;
            
            // 木の高さを考慮して中心点（少し上）をスクリーン座標に変換
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos + Vector3.up * 2f);
            
            // カメラの後ろにある場合は除外
            if (screenPos.z < 0) continue;

            float dist = Vector2.Distance(mousePos, new Vector2(screenPos.x, screenPos.y));
            if (dist < minScreenDist)
            {
                minScreenDist = dist;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// 現在ホバーしているTerrain Treeのインデックスを取得
    /// </summary>
    public int GetHoveredTreeIndex()
    {
        return hoveredTreeIndex;
    }

    /// <summary>
    /// 指定された矩形範囲内にあるTerrain Treeのインデックスのリストを返す
    /// </summary>
    public List<int> GetTreesInRect(float minX, float maxX, float minZ, float maxZ)
    {
        List<int> result = new List<int>();
        if (terrain == null || terrain.terrainData == null) return result;

        Vector3 terrainPos = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;

        TreeInstance[] instances = terrain.terrainData.treeInstances;
        for (int i = 0; i < instances.Length; i++)
        {
            Vector3 pos = instances[i].position;
            float worldX = pos.x * size.x + terrainPos.x;
            float worldZ = pos.z * size.z + terrainPos.z;

            if (worldX >= minX && worldX <= maxX && worldZ >= minZ && worldZ <= maxZ)
            {
                result.Add(i);
            }
        }
        return result;
    }
}
