using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class CrystalGenerator : MonoBehaviour
{
    [SerializeField] Mesh mesh;
    [SerializeField] MeshFilter meshFilter;
    [SerializeField] int numLops;

    // void Start()
    // {
    //     GenerateMesh();
    // }

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