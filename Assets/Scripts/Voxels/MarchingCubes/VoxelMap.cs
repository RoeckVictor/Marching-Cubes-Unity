using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Threading;

public class VoxelMap : MonoBehaviour
{
    // Number of threads to divide each chunk during update
    // AKA number^3 of voxels every thread computes
    private const int threadGroupSize = 8;

    // The marchingCubes ComputeShader used to update chunks
    public ComputeShader marchingCubes;

    [Header("Chunks")]
    // Size of a chunk in Unity's space unit
    public Vector3 chunkSize;
    // Size of a chunk in voxels
    public Vector3Int chunkVoxels;

    // Material used to render the chunks
    public Material surfaceMat;
    // Wheter the collisions should be generated or not
    public bool generateCollisions;

    // Size of a voxel
    public Vector3 voxelSize => new Vector3(chunkSize.x/chunkVoxels.x, chunkSize.y/chunkVoxels.y, chunkSize.z/chunkVoxels.z);
    // Center of the bounding cube that defines the voxels (center of the (0,0,0) chunk)
    public Vector3 areaCenter => transform.position;

    [Header("Marching Cubes Variables")]
    // isoLevel for the marching cubes algorithm
    public float isoLevel;

    // Dictionary containing every chunks depending on their Coordinates
    public Dictionary<Vector3Int, Chunk> chunks;

    // Compute buffers containing data returned by the compute shader (pos and val of voxels)
    private ComputeBuffer voxelsBuffer;
    // ComputeBuffers for the triangles
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer triCountBuffer;

    // Vertex structure for the Triangle structure
    private struct Vertex 
    {
        public Vector3 pos;
        public Vector2Int id;
    }

    // Triangle structure used to get the marchingCubes ComputeShader data
    private struct Triangle
    {
        #pragma warning disable 649 // disable unassigned variable warning
        // Vertexes
        public Vertex a;
        public Vertex b;
        public Vertex c;

        // Get the Vertexes with this[i]
        public Vertex this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0:
                        return a;
                    case 1:
                        return b;
                    default:
                        return c;
                }
            }
        }
    }

    private void Awake()
    {
        chunks = new Dictionary<Vector3Int, Chunk>();
    }

    // Used for editor
    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            // Draw a yellow wire cube for every chunk
            foreach (var chunk in chunks)
            {
                Gizmos.color = Color.yellow;
                Vector3Int index = chunk.Key;
                Vector3 offset = new Vector3(index.x*chunkSize.x, index.y*chunkSize.y, index.z*chunkSize.z);
                Gizmos.DrawWireCube(areaCenter + offset, new Vector3(chunkSize.x, chunkSize.y, chunkSize.z));
            }
        }
    }

    // Get the index of the chunk which the coords point is located in
    public Vector3Int GetChunkIndex(Vector3 coords) 
    {
        return new Vector3Int(Mathf.FloorToInt((coords.x+chunkSize.x/2)/chunkSize.x),
                              Mathf.FloorToInt((coords.y+chunkSize.y/2)/chunkSize.y),
                              Mathf.FloorToInt((coords.z+chunkSize.z/2)/chunkSize.z));
    }

    // Get the index of the voxel which the coords point is located in
    // the voxel index is independent from the chunks, this function act like
    // the world is just one big voxel grid
    public Vector3Int GetWorldVoxelIndex(Vector3 coords) 
    {
        return new Vector3Int(Mathf.FloorToInt(coords.x/voxelSize.x),
                              Mathf.FloorToInt(coords.y/voxelSize.y),
                              Mathf.FloorToInt(coords.z/voxelSize.z));
    }

    // Get the index of the voxel which the coords point is located in
    // inside of the chunk which the coords point is located in
    public Vector3Int GetVoxelIndex(Vector3 coords)
    {
        //TODO wrong with negative chunks, and probably rounding problems too

        Vector3Int chunk = GetChunkIndex(coords);
        Vector3Int worldPos = GetWorldVoxelIndex(coords);
        Vector3Int offset = chunkVoxels/2;

        // Fucking negative modulo C# piece of shit smfh
        Vector3Int positiveVoxelIndex = new Vector3Int((worldPos.x%chunkVoxels.x+offset.x)%chunkVoxels.x+chunk.x*chunkVoxels.x,
                                                       (worldPos.y%chunkVoxels.y+offset.y)%chunkVoxels.y+chunk.y*chunkVoxels.y,
                                                       (worldPos.z%chunkVoxels.z+offset.z)%chunkVoxels.z+chunk.z*chunkVoxels.z);

        return new Vector3Int(positiveVoxelIndex.x, positiveVoxelIndex.y, positiveVoxelIndex.z);
    }

    // Updates the mesh of the chunk using the marchingCubes Compute Shader
    public void UpdateChunk(Vector3Int chunkIndex)
    {
        // Send all of the data to the compute shader
        PrepareComputeShader(chunkIndex);

        // Find the exact number of threads depending on the size of the chunks and threadGroupSize
        Vector3Int numThreads = new Vector3Int(Mathf.CeilToInt((chunkVoxels.x)/(float)threadGroupSize),
                                               Mathf.CeilToInt((chunkVoxels.y)/(float)threadGroupSize),
                                               Mathf.CeilToInt((chunkVoxels.z)/(float)threadGroupSize));

        // Dispatch the compute shader
        marchingCubes.Dispatch(0, numThreads.x, numThreads.y, numThreads.z);

        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        // Get triangle data from shader
        Triangle[] tris = new Triangle[numTris];
        triangleBuffer.GetData(tris, 0, 0, numTris);

        // Removes the previous mesh and re-generate it
        chunks[chunkIndex].mesh.Clear();

        // OLD: every triangles has it's own 3 vertices
        /* 
        var vertices = new Vector3[numTris*3];
        var meshTriangles = new int[numTris*3];

        for (int i=0; i<numTris; i++)
        {
            for (int j=0; j<3; j++)
            {
                vertices[i*3+j] = tris[i][j].pos;
                meshTriangles[i*3+j] = i*3+j;
            }
        }

        // Set the mesh vertices and triangles
        chunks[chunkIndex].mesh.vertices = vertices;
        chunks[chunkIndex].mesh.triangles = meshTriangles;
        */

        // New: Removes duplicate vertices
        var vertexIndexMap = new Dictionary<Vector2Int, int>();
        var vertices = new List<Vector3>();
        var meshTriangles = new List<int>();
        int triangleIndex = 0;

        for (int i=0; i<numTris; i++)
        {
            for (int j=0; j<3; j++)
            {
                int sharedVertexIndex;
                if(vertexIndexMap.TryGetValue(tris[i][j].id, out sharedVertexIndex)) 
                {
                    meshTriangles.Add(sharedVertexIndex);
                }
                else 
                {
                    vertexIndexMap.Add(tris[i][j].id, triangleIndex);
                    vertices.Add(tris[i][j].pos);
                    meshTriangles.Add(triangleIndex);
                    triangleIndex++;
                }
            }
        }


        // Set the mesh vertices and triangles
        chunks[chunkIndex].mesh.SetVertices(vertices);
        chunks[chunkIndex].mesh.SetTriangles(meshTriangles, 0, true);
        chunks[chunkIndex].mesh.RecalculateNormals();

        // Enables the meshCollider if we want to generate collisions
        if (generateCollisions)
        {
            // force update
            chunks[chunkIndex].meshCollider.enabled = false;
            chunks[chunkIndex].meshCollider.enabled = true;
        }


        // Dispose of the ComputeBuffers
        voxelsBuffer.Dispose();
        triangleBuffer.Release();
        triCountBuffer.Release();
    }

    // Generates a new chunk instance
    public void CreateChunk(Vector3Int chunkIndex)
    {
        GameObject chunkObject = new GameObject();
        chunkObject.AddComponent(typeof(Chunk));

        chunks.Add(chunkIndex, chunkObject.GetComponent<Chunk>());
        chunks[chunkIndex].SetUp(chunkIndex, chunkVoxels, surfaceMat, generateCollisions);

        chunkObject.transform.SetParent(this.transform);
        chunkObject.name = "Chunk " + chunkIndex;
    }

    // Send all of the shapes data to the compute shader 
    private void PrepareComputeShader(Vector3Int chunkIndex)
    {
        // Setup the voxels ComputeBuffer ...
        voxelsBuffer = new ComputeBuffer(chunks[chunkIndex].voxelData.Length, sizeof(float));
        voxelsBuffer.SetData(chunks[chunkIndex].voxelData);
        // ... and sends it to the shader
        marchingCubes.SetBuffer(0, "voxels", voxelsBuffer);

        // Setup the triangle compute buffers
        int maxTriangleCount = chunkVoxels.x*chunkVoxels.y*chunkVoxels.z*5;
        triangleBuffer = new ComputeBuffer(maxTriangleCount, (sizeof(float)*3+sizeof(int)*2)*3, ComputeBufferType.Append);
        triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        triangleBuffer.SetCounterValue(0);
        marchingCubes.SetBuffer(0, "triangles", triangleBuffer);

        // Send other data to the shader
        // Size of the chunk
        marchingCubes.SetInts("size", chunkVoxels.x+1, chunkVoxels.y+1, chunkVoxels.z+1);
        // Center of the world
        marchingCubes.SetFloats("center", areaCenter.x, areaCenter.y, areaCenter.z);
        // Size of a voxel
        marchingCubes.SetFloats("voxelSize", voxelSize.x, voxelSize.y, voxelSize.z);
        // Iso level
        marchingCubes.SetFloat("isoLevel", isoLevel);
        // Index of the chunk
        marchingCubes.SetInts("chunkIndex", chunkIndex.x, chunkIndex.y, chunkIndex.z);
    }
}