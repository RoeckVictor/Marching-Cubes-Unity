using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelWorldManager : MonoBehaviour
{
    // The min index of a chunk, chunk with a smaller index will not generate
    public Vector3Int minChunkBound;
    // The max index of a chunk, chunk with a higher index will not generate
    public Vector3Int maxChunkBound;
    // Distance around the target where the chunks are generated
    public int chunkLoadDistance;
    // Distance around the target where every chunks farther gets disabled
    public int chunkDestroyDistance;
    // The way the terrain is being generated
    public GenerationType genType;

    // Player/MainCamera...
    public Transform target;
    // The voxel map
    public VoxelMap voxelMap;

    // Last chunk the target was in
    private Vector3Int lastTargetPos;
    // List of every chunks that needs to get updated on the next frame
    private List<Vector3Int> markedForTicking;

    public enum GenerationType 
    {
        Flat,
        Perlin,
        Complex
    }

    // Get the index of an element in a 3D array stored in a 1D array
    private static int GetIndex(Vector3Int pos, Vector3Int size)
    {
        return pos.x+pos.y*size.x+pos.z*size.x*size.y;
    }

    private void Awake()
    {
        markedForTicking = new List<Vector3Int>();
    }

    // Start is called before the first frame update
    private void Start()
    {
        lastTargetPos = voxelMap.GetChunkIndex(target.position);
        UpdateChunksList();
    }

    // Update is called once per frame
    private void Update()
    {
        // Get in which chunk the target is
        Vector3Int newTargetPos = voxelMap.GetChunkIndex(target.position);

        // If the target changed chunk since last frame
        if (newTargetPos != lastTargetPos)
        {
            lastTargetPos = newTargetPos;
            // Generates/Disable the concerned chunks
            UpdateChunksList();
        }

        // Copy of the markedForTicking list
        // Need to use a copy since TickChunk can Add/Remove element in the list
        List<Vector3Int> markedForTickingBuffer = new List<Vector3Int>();
        foreach(var chunk in markedForTicking)
            markedForTickingBuffer.Add(chunk);

        // Tick every chunk in the markedForTicking list
        // This way every chunk can only be ticked once per frames max
        foreach(var chunk in markedForTickingBuffer)
            TickChunk(chunk);
    }

    // Tick a chunk (apply gravity, simulates fire ...) and updates its mesh
    public void TickChunk(Vector3Int chunkIndex)
    {
        // Removes the chunk from the markedFroTicking list
        markedForTicking.Remove(chunkIndex);

        // Probably want to make theses 2 functions work on the GPU instead of the CPU
        //ApplyGravity(chunkIndex);
        //ApplySimulation();

        // Update the chunk after ticking
        voxelMap.UpdateChunk(chunkIndex);
    }

    // Create/Disable chunks depending on their positions and the target's
    private void UpdateChunksList()
    {
        foreach (var chunk in voxelMap.chunks)
        {
            // If the chunk is farther than the chunkDestroyDistance, disables it
            if (Vector3Int.Distance(chunk.Key, lastTargetPos) > chunkDestroyDistance)
            {
                chunk.Value.Disable();
            }
        }

        // Loop over the chunkLoadDistance to create every chunk that does not exist yet
        for (int i=-chunkLoadDistance+1; i<chunkLoadDistance; i++)
        {
            for (int j=-chunkLoadDistance+1; j<chunkLoadDistance; j++)
            {
                for (int k=-chunkLoadDistance+1; k<chunkLoadDistance; k++)
                {
                    // If the offset is smaller or equal to the chunkLoadDistance
                    if (Vector3Int.Distance(Vector3Int.zero, new Vector3Int(i, j, k))<=chunkLoadDistance)
                    {
                        // Get the current chunk position of the target
                        Vector3Int currentChunk = voxelMap.GetChunkIndex(target.position);
                        // Add the offset
                        currentChunk += new Vector3Int(i, j, k);
                        // Makes sure the chunk is within the bounds
                        currentChunk = Vector3Int.Max(currentChunk, minChunkBound);
                        currentChunk = Vector3Int.Min(currentChunk, maxChunkBound);

                        // If the chunk does not exist, creates it
                        if (!voxelMap.chunks.ContainsKey(currentChunk))
                        {
                            // Sets up the chunk
                            voxelMap.CreateChunk(currentChunk);
                            // The chunk has size+1 voxels because the last indexes are copies of the first 
                            // indexes of the following chunks, this is done in order to fill the gaps between chunks
                            Vector3Int chunkVoxels = voxelMap.chunkVoxels+Vector3Int.one;
                            // Init voxel values array
                            switch (genType) 
                            {
                                case GenerationType.Flat:
                                    voxelMap.chunks[currentChunk].voxelData = TerrainGeneration.FlatWorldGeneration(currentChunk, chunkVoxels);
                                    break;
                                case GenerationType.Perlin:
                                    voxelMap.chunks[currentChunk].voxelData = TerrainGeneration.PerlinGeneration(currentChunk, chunkVoxels, new Vector3(0.04f, 0.05f, 0.04f), Vector3.one);
                                    break;
                                case GenerationType.Complex:
                                    voxelMap.chunks[currentChunk].voxelData = TerrainGeneration.ComplexGeneration(currentChunk, chunkVoxels);
                                    break;
                            }

                            // Update the chunk mesh
                            if (!markedForTicking.Contains(currentChunk))
                                markedForTicking.Add(currentChunk);
                        }
                        // If the chunk exists but is not active, enables it
                        else if (!voxelMap.chunks[currentChunk].gameObject.activeSelf)
                        {
                            voxelMap.chunks[currentChunk].Enable();
                            if (!markedForTicking.Contains(currentChunk))
                                markedForTicking.Add(currentChunk);
                        }
                    }
                }
            }
        }
    }

    // Apply a stencil by calling the SetVoxel function of the concerned chunks
    // with the correct stencil.center
    public void EditVoxels(Vector3 pos, VoxelStencil stencil)
    {
        //This function uses the position of the voxel transform to figure out it's chunk and index inside the chunk
        //it is way faster than checking every voxels in the map

        Vector3 position = pos+voxelMap.voxelSize;
        //Edit the position vector so that the function works regardles of the VoxelMap position
        //Vector3 position = pos - voxelMap.transform.position;
        //Edit the position vector so that the function works regardles of the VoxelMap rotation
        //position = Quaternion.Euler(-voxelMap.transform.rotation.eulerAngles.x, -voxelMap.transform.rotation.eulerAngles.y, -voxelMap.transform.rotation.eulerAngles.z) * position;

        //Find the coordinate of the voxel relative to the entire map
        Vector3Int voxelCoord = voxelMap.GetVoxelIndex(position);

        //Bounds of the voxels that needs to be edited depending of the radius of the stencil
        Vector3Int boundStart = new Vector3Int((voxelCoord.x-stencil.radius-1)/voxelMap.chunkVoxels.x,
                                               (voxelCoord.y-stencil.radius-1)/voxelMap.chunkVoxels.y,
                                               (voxelCoord.z-stencil.radius-1)/voxelMap.chunkVoxels.z);
        Vector3Int boundEnd = new Vector3Int((voxelCoord.x+stencil.radius)/voxelMap.chunkVoxels.x,
                                             (voxelCoord.y+stencil.radius)/voxelMap.chunkVoxels.y,
                                             (voxelCoord.z+stencil.radius)/voxelMap.chunkVoxels.z);

        // Loop over the bounds to get the concerned chunks
        int offsetZ = boundEnd.z * voxelMap.chunkVoxels.z;
        for (int z = boundEnd.z; z >= boundStart.z; z--)
        {
            int offsetY = boundEnd.y * voxelMap.chunkVoxels.y;
            for (int y = boundEnd.y; y >= boundStart.y; y--)
            {
                int offsetX = boundEnd.x * voxelMap.chunkVoxels.x;
                for (int x = boundEnd.x; x >= boundStart.x; x--)
                {
                    // Set the stencil center
                    stencil.center = new Vector3Int(voxelCoord.x-offsetX, voxelCoord.y-offsetY, voxelCoord.z-offsetZ);

                    Vector3Int currentChunk = new Vector3Int(x, y, z);

                    // If the chunk is within the bounds
                    if (currentChunk == Vector3Int.Max(currentChunk, minChunkBound) &&
                        currentChunk == Vector3Int.Min(currentChunk, maxChunkBound))
                    {
                        // If it does not exists, create it
                        if (!voxelMap.chunks.ContainsKey(currentChunk))
                        {
                            voxelMap.CreateChunk(currentChunk);
                            // The chunk has size+1 voxels because the last indexes are copies of the first 
                            // indexes of the following chunks, this is done in order to fill the gaps between chunks
                            Vector3Int chunkVoxels = voxelMap.chunkVoxels + Vector3Int.one;
                            // Init voxel values array
                            switch (genType)
                            {
                                case GenerationType.Flat:
                                    voxelMap.chunks[currentChunk].voxelData = TerrainGeneration.FlatWorldGeneration(currentChunk, chunkVoxels);
                                    break;
                                case GenerationType.Perlin:
                                    voxelMap.chunks[currentChunk].voxelData = TerrainGeneration.PerlinGeneration(currentChunk, chunkVoxels, new Vector3(0.04f, 0.05f, 0.04f), Vector3.one);
                                    break;
                                case GenerationType.Complex:
                                    voxelMap.chunks[currentChunk].voxelData = TerrainGeneration.ComplexGeneration(currentChunk, chunkVoxels);
                                    break;
                            }

                            // Update the chunk mesh
                            if (!markedForTicking.Contains(currentChunk))
                                markedForTicking.Add(currentChunk);
                            // Instantly disables it since if it does not exist it is not in render distance
                            voxelMap.chunks[currentChunk].Disable();
                        }

                        // Get the bounds of the VoxelStencil
                        Vector3Int chunkBoundStart = stencil.boundStart;
                        Vector3Int chunkBoundEnd = stencil.boundEnd;

                        // If the VoxelStencil is affecting several chunks, changes the bound
                        // to stay inside the chunk, SetVoxel for the other chunks will be called in VoxelMap
                        // if boundStart < 0
                        chunkBoundStart = Vector3Int.Max(Vector3Int.zero, chunkBoundStart);
                        // if boundEnd >= areaVoxels
                        chunkBoundEnd = Vector3Int.Min(voxelMap.chunkVoxels, chunkBoundEnd);

                        // Loop over all of the voxels in the bounds and apply the stencil on them
                        for (int chunkX = chunkBoundStart.x; chunkX <= chunkBoundEnd.x; chunkX++)
                        {
                            for (int chunkY = chunkBoundStart.y; chunkY <= chunkBoundEnd.y; chunkY++)
                            {
                                for (int chunkZ = chunkBoundStart.z; chunkZ <= chunkBoundEnd.z; chunkZ++)
                                {
                                    Vector3Int chunkPos = new Vector3Int(chunkX, chunkY, chunkZ);
                                    float val = stencil.Apply(chunkPos, voxelMap.chunks[currentChunk].voxelData[GetIndex(chunkPos, voxelMap.chunkVoxels+Vector3Int.one)]);
                                    voxelMap.chunks[currentChunk].voxelData[GetIndex(chunkPos, voxelMap.chunkVoxels+Vector3Int.one)] = val;
                                }
                            }
                        }

                        if (!markedForTicking.Contains(currentChunk))
                            markedForTicking.Add(currentChunk);
                    }

                    offsetX -= voxelMap.chunkVoxels.x;
                }
                offsetY -= voxelMap.chunkVoxels.y;
            }
            offsetZ -= voxelMap.chunkVoxels.z;
        }
    }
}
