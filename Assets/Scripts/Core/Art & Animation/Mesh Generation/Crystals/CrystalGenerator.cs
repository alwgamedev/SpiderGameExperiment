using System.Collections.Generic;
using Unity.Collections;
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

        var vertices = mesh.vertices;
        // var triangles = mesh.triangles;
        var baryCoords = new List<Vector4>();
        mesh.GetUVs(2, baryCoords);

        // for (int i = 0; i < triangles.Length; i++)
        // {
        //     var iNext = MeshTools.NextIndexInTriangle(i);
        //     var p = transform.TransformPoint(vertices[triangles[i]]);
        //     var pNext = transform.TransformPoint(vertices[triangles[iNext]]);
        //     Debug.DrawLine(p, pNext, Color.yellow, 20);
        // }

        Debug.Log($"v {vertices.Length} b {baryCoords.Count}");
        
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = transform.TransformPoint(vertices[i]);
            var color = BaryColor(baryCoords[i]);
            Debug.DrawLine(p - new Vector3(0.05f, 0), p + new Vector3(0.05f, 0), color, 30);
            Debug.DrawLine(p - new Vector3(0, 0.05f), p + new Vector3(0, 0.05f), color, 30);
        }

        Color BaryColor(Vector4 v)
        {
            if (v.x != 0)
            {
                return Color.red;
            }
            if (v.y != 0)
            {
                return Color.green;
            }
            if (v.z != 0)
            {
                return Color.blue;
            }
            if (v.w != 0)
            {
                return Color.hotPink;
            }

            return Color.white;
        }
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