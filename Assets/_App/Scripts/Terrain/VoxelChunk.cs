using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 16x16x16ブロックを1つのメッシュにまとめるChunkクラス。
/// 隣接ブロックを見て露出面のみメッシュ生成する（パフォーマンス最適化の要）。
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class VoxelChunk : MonoBehaviour
{
    private BlockType[,,] blocks;
    public Vector3Int ChunkPosition { get; private set; }
    private VoxelWorld world;

    private List<Vector3> verts = new List<Vector3>();
    private List<int>[] submeshTris;
    private List<Vector2> uvsBuffer = new List<Vector2>();
    private List<Color> vertColors = new List<Color>();

    private MeshFilter mf;
    private MeshRenderer mr;
    private MeshCollider mc;
    private Mesh mesh;

    public void Initialize(VoxelWorld parentWorld, Vector3Int chunkPos, Material[] terrainMats)
    {
        world = parentWorld;
        ChunkPosition = chunkPos;
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();
        mc = GetComponent<MeshCollider>();
        mr.materials = terrainMats;
        blocks = new BlockType[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkDepth];
        mesh = new Mesh();
        mesh.name = $"Chunk_{chunkPos.x}_{chunkPos.y}_{chunkPos.z}";
        mf.mesh = mesh;
        
        submeshTris = new List<int>[terrainMats.Length];
        for (int i = 0; i < terrainMats.Length; i++)
        {
            submeshTris[i] = new List<int>();
        }
    }

    public BlockType GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= VoxelData.ChunkWidth ||
            y < 0 || y >= VoxelData.ChunkHeight ||
            z < 0 || z >= VoxelData.ChunkDepth)
            return BlockType.Air;
        return blocks[x, y, z];
    }

    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= VoxelData.ChunkWidth ||
            y < 0 || y >= VoxelData.ChunkHeight ||
            z < 0 || z >= VoxelData.ChunkDepth)
            return;
        blocks[x, y, z] = type;
    }

    public void RebuildMesh()
    {
        verts.Clear();
        foreach (var tris in submeshTris) tris.Clear();
        uvsBuffer.Clear();
        vertColors.Clear();

        for (int x = 0; x < VoxelData.ChunkWidth; x++)
            for (int y = 0; y < VoxelData.ChunkHeight; y++)
                for (int z = 0; z < VoxelData.ChunkDepth; z++)
                {
                    BlockType block = blocks[x, y, z];
                    if (block == BlockType.Air) continue;
                    BuildBlockFaces(x, y, z, block);
                }

        mesh.Clear();
        if (verts.Count == 0) { mc.sharedMesh = null; return; }
        mesh.SetVertices(verts);
        
        mesh.subMeshCount = submeshTris.Length;
        for (int i = 0; i < submeshTris.Length; i++)
        {
            mesh.SetTriangles(submeshTris[i], i);
        }
        
        mesh.SetUVs(0, uvsBuffer);
        mesh.SetColors(vertColors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mc.sharedMesh = null;
        mc.sharedMesh = mesh;
    }

    private void BuildBlockFaces(int x, int y, int z, BlockType block)
    {
        for (int face = 0; face < 6; face++)
        {
            Vector3Int n = VoxelData.FaceNormals[face];
            BlockType neighbor = GetNeighbor(x + n.x, y + n.y, z + n.z);

            if (!BlockData.IsTransparent(neighbor)) continue;
            if (block == BlockType.Water && neighbor == BlockType.Water) continue;

            int vs = verts.Count;
            Vector3 offset = new Vector3(x * VoxelData.BlockWidth, y * VoxelData.BlockHeight, z * VoxelData.BlockDepth);

            for (int v = 0; v < 4; v++)
            {
                int vi = VoxelData.FaceVertices[face, v];
                verts.Add(VoxelData.Vertices[vi] + offset);
            }

            TextureTileIndex tile = BlockData.GetTextureTile(block, face);
            int submeshIndex = (int)tile;

            submeshTris[submeshIndex].Add(vs); submeshTris[submeshIndex].Add(vs + 1); submeshTris[submeshIndex].Add(vs + 2);
            submeshTris[submeshIndex].Add(vs); submeshTris[submeshIndex].Add(vs + 2); submeshTris[submeshIndex].Add(vs + 3);

            // サブメッシュ方式なのでUVは常に0~1を使用
            Vector2[] fuv = VoxelData.BaseUVs;
            uvsBuffer.Add(fuv[0]); uvsBuffer.Add(fuv[1]);
            uvsBuffer.Add(fuv[2]); uvsBuffer.Add(fuv[3]);

            // 頂点カラーは白
            vertColors.Add(Color.white);
            vertColors.Add(Color.white);
            vertColors.Add(Color.white);
            vertColors.Add(Color.white);
        }
    }

    private BlockType GetNeighbor(int lx, int ly, int lz)
    {
        if (lx >= 0 && lx < VoxelData.ChunkWidth &&
            ly >= 0 && ly < VoxelData.ChunkHeight &&
            lz >= 0 && lz < VoxelData.ChunkDepth)
            return blocks[lx, ly, lz];

        if (world != null)
            return world.GetBlock(ChunkPosition.x + lx, ChunkPosition.y + ly, ChunkPosition.z + lz);

        return BlockType.Air;
    }
}
