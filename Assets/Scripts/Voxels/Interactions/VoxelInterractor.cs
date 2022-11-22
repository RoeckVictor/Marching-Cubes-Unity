using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Class that allows the player to interract with the VoxelMap using a VoxelStencil
public class VoxelInterractor : MonoBehaviour
{
    // The VoxelMap to interract with
    public VoxelWorldManager voxelWorld;

    // The GameObjects that are created to display the ghost
    public GameObject cubeGhost;
    public GameObject sphereGhost;

    // Whether the ghost should be displayed or not
    public bool useGhost = true;

    // Values fed to the VoxelStencil
    public float fillType;
    public int radius;
    public VoxelStencil.eVoxelStencilType stencilType;

    // The VoxelStencil used to interract
    private VoxelStencil activeStencil = new VoxelStencil();
    // Generated object to display the ghost
    private GameObject ghost;

    private float[] voxelizedMesh;

    private void Update()
    {
        // Feed the values to the VoxelStencil
        activeStencil.radius = radius;
        activeStencil.fillType = fillType;
        activeStencil.stencilType = stencilType;

        // If a ghost exists, destroys it
        if (ghost)
            Destroy(ghost);

        // Draw a ray from the camera to what the mouse is pointing
        RaycastHit hitInfo;
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hitInfo))
        {
            // If what we hit is a chunk (direct child of the VoxelMap)
            if (hitInfo.collider.gameObject.transform.parent == voxelWorld.voxelMap.transform)
            {
                // If useGhost == true, instantiate the correct GameObject depending on
                // the stencilType, else we do nothing since we already destroyed
                // all previously instantied ghosts
                if (useGhost)
                {
                    // Cube
                    if(activeStencil.stencilType == VoxelStencil.eVoxelStencilType.Cube)
                        ghost = (GameObject)Instantiate(cubeGhost, hitInfo.point, Quaternion.identity);
                    // Sphere
                    //else if (activeStencil.stencilType == VoxelStencil.eVoxelStencilType.Sphere)
                    else
                        ghost = (GameObject)Instantiate(sphereGhost, hitInfo.point, Quaternion.identity);

                    // Sets the scale of the ghost
                    ghost.transform.localScale = voxelWorld.voxelMap.voxelSize * activeStencil.radius;
                }
                // If LMB is pressed, send the coordinates of the collision and the stencil
                // to voxelMap to change the affected chunks
                if (Input.GetMouseButton(0))
                {
                    voxelWorld.EditVoxels(hitInfo.point, activeStencil);
                }
            }
        }
    }
}
