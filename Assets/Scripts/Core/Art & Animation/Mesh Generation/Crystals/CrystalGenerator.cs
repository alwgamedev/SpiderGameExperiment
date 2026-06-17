using UnityEngine;

[ExecuteInEditMode]
public class CrystalGenerator : MonoBehaviour
{
    [SerializeField] Mesh mesh;
    [SerializeField] MeshRenderer material;
    [SerializeField] MeshFilter meshFilter;
    [SerializeField] int numLops;
    [SerializeField] Vector2 maxStretch;
    [SerializeField] float stretchRate;

    int stretchProperty = Shader.PropertyToID("Stretch");
    float timer;
    Vector2 stretchGoal;

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