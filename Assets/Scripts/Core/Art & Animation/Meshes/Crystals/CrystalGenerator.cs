using UnityEngine;

[ExecuteInEditMode]
public class CrystalGenerator : MonoBehaviour
{
    [SerializeField] Mesh mesh;
    [SerializeField] MeshFilter meshFilter;
    [SerializeField] int numLops;
    
    public void GenerateMesh()
    {
        if (mesh)
        {
            DestroyImmediate(mesh);
        }

        mesh = CrystalTools.GenerateCrystalMesh(numLops);
        meshFilter.sharedMesh = mesh;
    }

    private void OnDestroy()
    {
        if (mesh)
        {
            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }
        }
    }
}