using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TerrainGeneration
{
    // Get the index of an element in a 3D array stored in a 1D array
    private static int GetIndex(Vector3Int pos, Vector3Int size)
    {
        return pos.x+pos.y*size.x+pos.z*size.x*size.y;
    }

    // Perlin function to get a perlin noise in 3D
    private static float Perlin3D(Vector3 pos)
    {
        float ab = Mathf.PerlinNoise(pos.x, pos.y);
        float bc = Mathf.PerlinNoise(pos.y, pos.z);
        float ac = Mathf.PerlinNoise(pos.x, pos.z);

        float ba = Mathf.PerlinNoise(pos.y, pos.x);
        float cb = Mathf.PerlinNoise(pos.z, pos.y);
        float ca = Mathf.PerlinNoise(pos.z, pos.x);

        float abc = ab+bc+ac+ba+cb+ca;

        return abc/6f;
    }

    // Function to layer Perlin noises with different frequency and intencities
    private static float OctavedPerlin(Vector2 pos, int octaves) 
    {
        float res = 0;
        for (int i = 0; i < octaves; i++)
        {
            float octaveMag = Mathf.Pow(2, i*2);
            res += Mathf.PerlinNoise(pos.x*octaveMag, pos.y*octaveMag)/octaveMag;
        }

        return res;
    }

    // Function to layer 3D Perlin noises with different frequency and intencities
    /*
    private static float OctavedPerlin3D(Vector3 pos, int octaves)
    {
        float res = 0;
        for (int i=0; i<octaves; i++)
        {
            float octaveMag = Mathf.Pow(2, i*2);
            Vector3 posVal = pos*octaveMag;
            res += Perlin3D(posVal)/octaveMag;
        }

        return res;
    }
    */

    // Generate chunks using perlin noise
    public static float[] PerlinGeneration(Vector3Int chunkIndex, Vector3Int chunkSize, Vector3 perlinScale, Vector3 seed)
    {
        float[] voxelData = new float[chunkSize.x*chunkSize.y*chunkSize.z];

        for (int i=0; i<chunkSize.x; i++)
        {
            for (int j=0; j<chunkSize.y; j++)
            {
                for (int k=0; k < chunkSize.z; k++)
                {
                    Vector3Int pos = new Vector3Int(i, j, k);

                    Vector3 perlinPos = new Vector3(i+chunkIndex.x*(chunkSize.x-1), j+chunkIndex.y*(chunkSize.y-1), k+chunkIndex.z*(chunkSize.z-1));
                    perlinPos = Vector3.Scale(perlinPos, perlinScale);

                    float perlinVal = Perlin3D(perlinPos+seed)*2-1;
                    voxelData[GetIndex(pos, chunkSize)] = perlinVal;
                }
            }
        }
        return voxelData;
    }

    public static float[] FlatWorldGeneration(Vector3Int chunkIndex, Vector3Int chunkSize) 
    {
        int surfaceLevel = 112;
        int chunkHeight = chunkIndex.y*(chunkSize.y-1);

        float[] voxelData = new float[chunkSize.x*chunkSize.y*chunkSize.z];

        for (int i=0; i<chunkSize.x; i++)
        {
            for (int j=0; j<chunkSize.y; j++)
            {
                for (int k=0; k < chunkSize.z; k++)
                {
                    Vector3Int pos = new Vector3Int(i, j, k);
                    if(chunkHeight+j < surfaceLevel)
                        voxelData[GetIndex(pos, chunkSize)] = -1f;
                    else
                        voxelData[GetIndex(pos, chunkSize)] = 1f;
                }
            }
        }
        return voxelData;
    }

    public static float[] ComplexGeneration(Vector3Int chunkIndex, Vector3Int chunkSize) 
    {
        int defaultSurfaceLevel = 112;
        float surfaceLevel;
        int chunkHeight = chunkIndex.y*(chunkSize.y-1);

        float[] voxelData = new float[chunkSize.x*chunkSize.y*chunkSize.z];
        for (int i=0; i<chunkSize.x; i++)
        {
            for (int k=0; k<chunkSize.z; k++)
            {
                Vector2 PerlinPos2D = new Vector2((i+chunkIndex.x*(chunkSize.x-1))*0.005f, (k+chunkIndex.z*(chunkSize.z-1))*0.005f);
                surfaceLevel = defaultSurfaceLevel + (OctavedPerlin(PerlinPos2D, 4)*2-1)*30;
                for (int j=0; j<chunkSize.y; j++)
                {
                    Vector3Int pos = new Vector3Int(i, j, k);
                    voxelData[GetIndex(pos, chunkSize)] = chunkHeight+j-surfaceLevel;
                    voxelData[GetIndex(pos, chunkSize)] = Mathf.Min(1, voxelData[GetIndex(pos, chunkSize)]);
                    voxelData[GetIndex(pos, chunkSize)] = Mathf.Max(-1, voxelData[GetIndex(pos, chunkSize)]);

                    if(chunkHeight+j+1 < surfaceLevel) 
                    {
                        Vector3 perlinPos = new Vector3(i+chunkIndex.x*(chunkSize.x-1), j+chunkIndex.y*(chunkSize.y-1), k+chunkIndex.z*(chunkSize.z-1));
                        perlinPos = Vector3.Scale(perlinPos, new Vector3(0.04f, 0.05f, 0.04f));

                        float perlinVal = Perlin3D(perlinPos)*2-1;

                        voxelData[GetIndex(pos, chunkSize)] = Mathf.Abs(perlinVal)-0.2f;
                    }
                }
            }

        }
        return voxelData;
    }
}