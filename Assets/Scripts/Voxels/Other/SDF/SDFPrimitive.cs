using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SDFPrimitive : MonoBehaviour
{
    // Number of threads to divide each chunk during update
    // AKA number^3 of voxels every thread computes
    private const int threadGroupSize = 8;

    private const int octreeDepth = 4;

    // Enum listing the primitive shapes
    public enum ShapeType 
    { 
        Sphere, 
        Cube,
        Torus, 
        Cone, 
        Plane, 
        Prism, 
        Cylinder 
    };
    // Enum listing the operations between the SDFs
    public enum Operation
    {
        Union,
        Blend,
        Cut,
        Inter
    };

    // The shape primitive
    public ShapeType shapeType;
    // The operation to use with this primitive
    public Operation operation;
    // Blend range for the Blend Operation
    [Range(0, 1)]
    public float blendStrength;

    // Position of the object
    public Vector3 Position => transform.position;
    // Size of the object (TODO adapt it in editor for every shapes)
    public Vector3 Scale;

    // Position the object was in on the last frame
    private Vector3 LastFramePos;

    private float[] voxelValues; // TODELETE
    public ComputeShader compute; // TODELETE

    private void Awake()
    {
        LastFramePos = Position;
    }

    private void Update()
    {
        if (LastFramePos != Position)
        {
            Vector3 min = Vector3.Min(Position - Scale, LastFramePos - Scale);
            Vector3 max = Vector3.Max(Position + Scale, LastFramePos + Scale);
            // MapEvents.current.ChunkUpdateRequest(min, max);
            LastFramePos = Position;
        }

        voxelValues = GetVoxelValues(4, compute); // TODELETE
    }

    private void OnDrawGizmos()
    {
        Vector3 BBMin = Position - Scale;
        float boundMax = Mathf.Max(Scale.x*2, Scale.y*2, Scale.z*2);
        float voxelSize = boundMax / (Mathf.Pow(2, octreeDepth));

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Position, Scale * 2);

        if (voxelValues != null && voxelValues.Length >= Mathf.Pow(Mathf.Pow(2, octreeDepth),3)) 
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < (int)Mathf.Pow(2, octreeDepth); i++)
                for (int j = 0; j < (int)Mathf.Pow(2, octreeDepth); j++)
                    for (int k = 0; k < (int)Mathf.Pow(2, octreeDepth); k++)
                        if (voxelValues[i + j * (int)Mathf.Pow(2, octreeDepth) + k * (int)Mathf.Pow(2, octreeDepth) * (int)Mathf.Pow(2, octreeDepth)] < 0)
                            Gizmos.DrawWireCube(BBMin + Vector3.one * voxelSize / 2 + new Vector3(i, j, k) * voxelSize, Vector3.one * voxelSize);
        }
    }


    public float[] GetVoxelValues(int octreeDepth, ComputeShader discretizeSDF) 
    {
        // Compute voxelSize and gridSize
        float boundMax = Mathf.Max(Scale.x, Scale.y, Scale.z);
        float voxelSize = boundMax/(Mathf.Pow(2, octreeDepth));
        float div = boundMax/voxelSize;
        Vector3Int gridSize = new Vector3Int((int)div, (int)div, (int)div);

        // Send all of the data to the compute shader
        Vector3 BBMin = Position-Scale;
        Vector3 BBMax = Position+Scale;
        discretizeSDF.SetFloats("BBMin", BBMin.x, BBMin.y, BBMin.z);
        discretizeSDF.SetFloats("BBMax", BBMax.x, BBMax.z, BBMax.z);
        discretizeSDF.SetInts("gridSize", gridSize.x, gridSize.y, gridSize.z);
        discretizeSDF.SetFloat("voxelSize", voxelSize);

        discretizeSDF.SetFloats("position", Position.x, Position.y, Position.z);
        discretizeSDF.SetFloats("size", Scale.x/2, Scale.y/2, Scale.z/2);
        discretizeSDF.SetInt("shapeType", (int)shapeType);

        ComputeBuffer voxelsBuffer = new ComputeBuffer(gridSize.x*gridSize.y*gridSize.z, sizeof(float));
        discretizeSDF.SetBuffer(0, "voxels", voxelsBuffer);

        // Find the exact number of threads depending on the size of the chunks and threadGroupSize
        Vector3Int numThreads = new Vector3Int(Mathf.CeilToInt((gridSize.x) / (float)threadGroupSize),
                                               Mathf.CeilToInt((gridSize.y) / (float)threadGroupSize),
                                               Mathf.CeilToInt((gridSize.z) / (float)threadGroupSize));

        // Dispatch the compute shader
        discretizeSDF.Dispatch(0, numThreads.x, numThreads.y, numThreads.z);

        // Stores the ComputeShader result in res
        float[] res = new float[gridSize.x*gridSize.y*gridSize.z];
        voxelsBuffer.GetData(res, 0, 0, res.Length);

        // Release buffers
        voxelsBuffer.Release();

        return res;
    }
}
