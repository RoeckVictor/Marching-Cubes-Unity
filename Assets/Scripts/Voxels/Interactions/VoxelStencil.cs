using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A Class used for multiple voxel editting within a VoxelMap
public class VoxelStencil
{
    // The different shapes of stencils
    public enum eVoxelStencilType
    {
        Cube,
        Sphere
    }

    // Front bottom left bound of the stencil
    public Vector3Int boundStart
    {
        get { return center-Vector3Int.one*(radius+1); }
    }
    // Back top right bound of the stencil
    public Vector3Int boundEnd
    {
        get { return center+Vector3Int.one*(radius-1); }
    }

    // The change in isoValue to apply to affected voxels
    public float fillType;
    // The center point of the stencil
    public Vector3Int center;
    // The radius of the stencil
    public int radius;
    // The shape of the stencil
    public eVoxelStencilType stencilType;

    // Apply the stencil to theVoxel depending on its coords
    // and returns the result
    public float Apply(Vector3Int coords, float theVoxel)
    {
        // Result
        float newVox;

        // The operation will be different depending on the stencilType
        switch (stencilType)
        {
            // If it's a cube
            case eVoxelStencilType.Cube:
                // Since the BoundingBox is already matching the shape, we don't need to do additional
                // calulations to determin whether we should change the voxel or not
                // We simply add the fillType to the isoValue
                newVox = theVoxel+fillType;
                return newVox;
            case eVoxelStencilType.Sphere:
                // Since the BoundingBox is a rectangle, we need to figure out if the voxel is inside de stencil
                coords -= center;
                // dst = 0 if the voxel is on the edge of the sphere, > 0 if inside, < 0 if outside
                float dst = 1-(Mathf.Pow(coords.x,2)+Mathf.Pow(coords.y,2)+Mathf.Pow(coords.z,2))/Mathf.Pow(radius-1,2);
                // If dst > 0 then we want to change the voxel
                if (dst > 0) 
                {
                    // To get a nice smooth sphere, we change the isoValue depending on dst*fillType
                    newVox = theVoxel+dst*fillType;
                    return newVox;
                }
                return theVoxel;
        }
        return theVoxel;
    }
}
