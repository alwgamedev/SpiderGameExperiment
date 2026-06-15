using UnityEngine;

public class CrystalGenerator : MonoBehaviour
{
    [SerializeField] Mesh mesh;
    [SerializeField] MeshFilter meshFilter;
    [SerializeField] int numLopsMin;
    [SerializeField] int numLopsMax;

    public void GenerateMesh()
    {
        if (mesh)
        {
            DestroyImmediate(mesh);
        }

        mesh = CrystalTools.GenerateCrystalMesh(numLopsMin, numLopsMax);
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