using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshVoxelizer
{
    // Number of threads to divide each chunk during update
    // AKA number^3 of voxels every thread computes
    private const int threadGroupSize = 8;

    // Triangle struct passed to the compute shader
    struct Triangle
    {
        public Vector3 vertexA;
        public Vector3 vertexB;
        public Vector3 vertexC;
    };

    // Turn a mesh into a 2^octreeDepth grid of voxels
    public float[] MeshToVoxel(Mesh mesh, int octreeDepth, ComputeShader voxelizer) 
    {
        // Compute voxelSize and gridSize
        Bounds bounds = mesh.bounds;
        float boundMax = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float voxelSize = boundMax/(Mathf.Pow(2, octreeDepth));
        float div = boundMax/voxelSize;
        Vector3Int gridSize = new Vector3Int((int)div, (int)div, (int)div);

        // Send all of the data to the compute shader
        voxelizer.SetFloats("BBMin", bounds.min.x, bounds.min.y, bounds.min.z);
        voxelizer.SetFloats("BBMax", bounds.max.x, bounds.max.z, bounds.max.z);
        voxelizer.SetInts("gridSize", gridSize.x, gridSize.y, gridSize.z);
        voxelizer.SetFloat("voxelSize", voxelSize);
        voxelizer.SetInt("trianglesNum", mesh.triangles.Length / 3);

        ComputeBuffer voxelsBuffer = new ComputeBuffer(gridSize.x * gridSize.y * gridSize.z, sizeof(float));
        voxelizer.SetBuffer(0, "voxels", voxelsBuffer);

        // Formate the trianglesBuffer data
        Triangle[] triangles = new Triangle[mesh.triangles.Length/3];
        for(int i=0; i<triangles.Length; i++)
        {
            triangles[i].vertexA = mesh.vertices[mesh.triangles[i*3]];
            triangles[i].vertexB = mesh.vertices[mesh.triangles[i*3+1]];
            triangles[i].vertexC = mesh.vertices[mesh.triangles[i*3+2]];
        }
        ComputeBuffer trianglesBuffer = new ComputeBuffer(triangles.Length, sizeof(float)*3*3);
        trianglesBuffer.SetData(triangles);
        voxelizer.SetBuffer(0, "triangles", trianglesBuffer);

        // Find the exact number of threads depending on the size of the chunks and threadGroupSize
        Vector3Int numThreads = new Vector3Int(Mathf.CeilToInt((gridSize.x)/(float)threadGroupSize),
                                               Mathf.CeilToInt((gridSize.y)/(float)threadGroupSize),
                                               Mathf.CeilToInt((gridSize.z)/(float)threadGroupSize));

        // Dispatch the compute shader
        voxelizer.Dispatch(0, numThreads.x, numThreads.y, numThreads.z);

        // Stores the ComputeShader result in res
        float[] res = new float[gridSize.x*gridSize.y*gridSize.z];
        voxelsBuffer.GetData(res, 0, 0, res.Length);

        // Release buffers
        voxelsBuffer.Release();
        trianglesBuffer.Release();

        return res;
    }
}
