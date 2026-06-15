using UnityEngine;

public class CrystalGenerator : MonoBehaviour
{
    [SerializeField] Mesh mesh;
    [SerializeField] MeshFilter meshFilter;

    public void Generate()
    {
        if (mesh)
        {
            DestroyImmediate(mesh);
        }

        mesh = CrystalTools.GenerateCrystalMesh();
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