using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    // Grid containing the voxels of the chunk
    public float[] voxelData;

    // Chunk Meshes
    [HideInInspector]
    // Mesh storing the result of MarchingCubes
    public Mesh mesh;
    [HideInInspector]
    // Managing collisions
    public MeshCollider meshCollider;
    // Passes the mesh to the MeshRenderer
    private MeshFilter meshFilter;
    // Managing rendering
    [HideInInspector]
    public MeshRenderer meshRenderer;

    // Usefull values
    [HideInInspector]
    public Vector3Int chunkIndex;
    [HideInInspector]
    public Vector3Int chunkVoxels;

    // Initialize the chunk
    public void SetUp(Vector3Int index, Vector3Int size, Material mat, bool generateCollider)
    {
        // Init variables
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        // If no mesh Filter/Renderer/Collider are found, add them
        // if generateCollider == false, remove the meshCollider
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        if (meshCollider == null && generateCollider)
            meshCollider = gameObject.AddComponent<MeshCollider>();
        if (meshCollider != null && !generateCollider)
            DestroyImmediate(meshCollider);

        // Initialize the mesh
        mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.sharedMesh = mesh;
        }

        // Initialize the meshCollider if generateCollider == true
        if (generateCollider)
        {
            if (meshCollider.sharedMesh == null)
                meshCollider.sharedMesh = mesh;

            // force update the meshCollider
            meshCollider.enabled = false;
            meshCollider.enabled = true;
        }

        // Sets the material used to render the mesh
        meshRenderer.material = mat;

        chunkIndex = index;
        chunkVoxels = size;
    }

    // When a chunk goes out of render distance. We delete the mesh but we need to
    // keep the voxelData array, so we don't destroy the gameObject
    public void Disable()
    {
        mesh.Clear();
        gameObject.SetActive(false);
    }

    // When an already initialized chunk goes back in render distance
    // The mesh in generated in VoxelMap
    public void Enable()
    {
        gameObject.SetActive(true);
    }
}