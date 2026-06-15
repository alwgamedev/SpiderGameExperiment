using Unity.Collections;
using UnityEngine;

public static class CrystalTools
{
    public static Mesh CrystalMesh()
    {
        var crystal = StartingCrystal(Allocator.Temp);

        var vertices = crystal.vertex;
        Vector2 bbMin = vertices[0];
        Vector2 bbMax = vertices[0];
        for (int i = 1; i < vertices.Length; i++)
        {
            var v = vertices[i];
            bbMin = Vector2.Min(bbMin, v);
            bbMax = Vector2.Max(bbMax, v);
        }

        var bbSpan = bbMax - bbMin;
        var uv = new NativeArray<Vector2>(vertices.Length, Allocator.Temp);
        for (int i = 0; i < vertices.Length; i++)
        {
            uv[i] = ((Vector2)vertices[i] - bbMin) / bbSpan;
        }

        var triangles = new NativeList<int>(Allocator.Temp);
        var face = crystal.face;
        var faceStart = crystal.faceStart;
        for (int i = 0; i < faceStart.Length - 1; i++)
        {
            var start = faceStart[i];
            var end = faceStart[i + 1];
            var faceSize = end - start;

            if (faceSize == 3)
            {
                triangles.Add(face[start]);
                triangles.Add(face[start + 1]);
                triangles.Add(face[start + 2]);
            }
            else if (faceSize == 4)
            {
                triangles.Add(face[start]);
                triangles.Add(face[start + 1]);
                triangles.Add(face[start + 2]);

                triangles.Add(face[start]);
                triangles.Add(face[start + 2]);
                triangles.Add(face[start + 3]);
            }
            else
            {
                Debug.Log("Crystal faces should have <= 4 vertices...");
            }
        }

        var mesh = new Mesh();
        mesh.SetVertices(vertices.AsArray());
        mesh.SetIndices(triangles.AsArray(), MeshTopology.Triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.RecalculateNormals();
        return mesh;
    }

    struct Crystal
    {
        //in our crystals all vertices will have degree 3
        //and all faces have 3 or 4 sides
        public NativeList<Vector3> vertex;
        public NativeList<int> face;
        public NativeList<int> faceStart;
        public NativeList<int> neighbor;
        //the 3 neighbors are listed in CW order (wrt outward normal)

        public void Dispose()
        {
            if (vertex.IsCreated)
            {
                vertex.Dispose();
            }
            if (face.IsCreated)
            {
                face.Dispose();
            }
            if (faceStart.IsCreated)
            {
                faceStart.Dispose();
            }
        }
    }

    static Vector3 RandomV3(float xMin, float xMax, float yMin, float yMax, float zMin, float zMax)
    {
        return new(MathTools.RandomFloat(xMin, xMax), MathTools.RandomFloat(yMin, yMax),
            MathTools.RandomFloat(zMin, zMax));
    }

    static Crystal StartingCrystal(Allocator allocator)
    {
        var v0 = RandomV3(-.625f, -.5f, -.625f, -.5f, 0, 0);
        var v1 = RandomV3(-.625f, -.5f, .5f, .625f, 0, 0);
        var v2 = RandomV3(.5f, .625f, 0.5f, 0.625f, 0, 0);
        var v3 = RandomV3(0.5f, .625f, -.625f, -.5f, 0, 0);
        var v4 = RandomV3(-.375f, -.25f, -.375f, -.25f, 1, 1);
        var v5 = RandomV3(-.375f, -.25f, .25f, .375f, 1, 1);
        var v6 = RandomV3(.25f, .375f, .25f, .375f, 1, 1);
        var v7 = RandomV3(.25f, .375f, -.375f, -.25f, 1, 1);

        var vertex = new NativeList<Vector3>(allocator) { v0, v1, v2, v3, v4, v5, v6, v7 };
        var face = new NativeList<int>(allocator)
        {
            0, 1, 5, 4,
            1, 2, 6, 5,
            2, 3, 7, 6,
            7, 3, 0, 4,
            4, 5, 6, 7
        };
        var faceStart = new NativeList<int>(allocator) { 0, 4, 8, 12, 16, 20 };
        var neighbor = new NativeList<int>(allocator)
        {
            1, 4, 3,//v0 nbrs
            2, 5, 0,//v1 nbrs 
            3, 6, 1,//v2 nbrs
            0, 7, 2,//v3 nbrs
            5, 7, 0,//v4 nbrs
            6, 4, 1,//v5 nbrs
            2, 7, 5,//v6 nbrs
            6, 3, 4//v7 nbrs
        };

        return new()
        {
            vertex = vertex,
            face = face,
            faceStart = faceStart,
            neighbor = neighbor
        };
    }

    static int PrevIndexInFace(int j, int faceStart, int faceEnd)
    {
        return j == faceStart ? faceEnd - 1 : j - 1;
    }

    static int NextIndexInFace(int j, int faceStart, int faceEnd)
    {
        return j == faceEnd - 1 ? faceStart : j + 1;
    }
}