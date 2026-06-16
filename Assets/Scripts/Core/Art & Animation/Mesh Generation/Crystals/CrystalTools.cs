using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;

public static class CrystalTools
{
    public static Mesh GenerateCrystalMesh(int numLopsMin, int numLopsMax)
    {
        int numLops = MathTools.RNG.Next(numLopsMin, numLopsMax);
        var crystal = RandomCrystal(numLops, Allocator.Temp);

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
        var uv = new NativeArray<Vector3>(vertices.Length, Allocator.Temp);
        for (int i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            var w = ((Vector2)vertices[i] - bbMin) / bbSpan;
            uv[i] = new(w.x, w.y, v.z);//store height in uv0.z
            v.z = 0;
            vertices[i] = v;
        }

        var triangles = new NativeList<int>(Allocator.Temp);
        var face = crystal.face;
        var faceStart = crystal.faceStart;
        for (int i = 0; i < faceStart.Length - 1; i++)
        {
            var start = faceStart[i];
            var end = faceStart[i + 1];

            //triangulate the face
            var v0 = face[start];
            for (int j = start + 1; j < end - 1; j++)
            {
                triangles.Add(v0);
                triangles.Add(face[j]);
                triangles.Add(face[j + 1]);
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
        //all vertices will have degree 3
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

    static Crystal RandomCrystal(int numLops, Allocator allocator)
    {
        var c = StartingCrystal(allocator);
        while (numLops > 0)
        {
            var n = c.vertex.Length;
            for (int i = 0; i < n; i++)
            {
                LopVertex(c, i);
            }
            numLops--;
        }
        // while (numLops > 0)
        // {
        //     var v = MathTools.RNG.Next(c.vertex.Length);
        //     LopVertex(c, v);
        //     numLops--;
        // }

        return c;
    }

    static Crystal StartingCrystal(Allocator allocator)
    {
        //if you want to randomize the starting vertices,
        //need to make sure faces are still planes 
        //(when interior edges are parallel to outer edges, the interior points have to all have same height)
        var v0 = new Vector3(-0.5f, -0.5f, 0);
        var v1 = new Vector3(-0.5f, 0.5f, 0);
        var v2 = new Vector3(0.5f, 0.5f, 0);
        var v3 = new Vector3(0.5f, -0.5f, 0);
        var v4 = new Vector3(-0.25f, -0.25f, 1f);
        var v5 = new Vector3(-0.25f, 0.25f, 1f);
        var v6 = new Vector3(0.25f, 0.25f, 1f);
        var v7 = new Vector3(0.25f, -0.25f, 1f);

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

    static void LopVertex(Crystal crystal, int v)
    {
        var vertex = crystal.vertex;
        var face = crystal.face;
        var faceStart = crystal.faceStart;
        var neighbor = crystal.neighbor;

        var v0 = neighbor[3 * v];
        var v1 = neighbor[3 * v + 1];
        var v2 = neighbor[3 * v + 2];

        //vertex positions
        var p = vertex[v];
        var p0 = vertex[v0];
        var p1 = vertex[v1];
        var p2 = vertex[v2];

        var area = Vector3.Cross(p2 - p0, p1 - p0).magnitude;
        if (area < 0.05f)
        {
            return;
        }

        //edge directions
        var u0 = (p0 - p).normalized;
        var u1 = (p1 - p).normalized;
        var u2 = (p2 - p).normalized;

        //outward face normals
        //lop face's normal needs to be a nonnegative combination of these in order to stay above neighbor vertices
        var n0 = Vector3.Cross(u1, u0);
        var n1 = Vector3.Cross(u2, u1);
        var n2 = Vector3.Cross(u0, u2);

        //there may be one normal with n.z < 0 (if one of the faces is the underside)
        //we need to make sure the lop face's normal has z > 0
        var t0 = MathTools.RandomFloat(0.25f, 1f);
        var t1 = MathTools.RandomFloat(0.25f, 1f);
        var t2 = MathTools.RandomFloat(0.25f, 1f);

        if (n0.z < 0)
        {
            var nz = Mathf.Min(n0.z, -MathTools.o41);
            t0 = Mathf.Min(t0, 0.9f * (-t1 * n1.z - t2 * n2.z) / nz);
            t0 = Mathf.Min(t0, 0.25f);//keep t0 smaller or else get mostly very steep faces near boundary
        }
        else if (n1.z < 0)
        {
            var nz = Mathf.Min(n1.z, -MathTools.o41);
            t1 = Mathf.Min(t1, 0.95f * (-t0 * n0.z - t2 * n2.z) / nz);
            t1 = Mathf.Min(t1, 0.25f);
        }
        else if (n2.z < 0)
        {
            var nz = Mathf.Min(n2.z, -MathTools.o41);
            t2 = Mathf.Min(t2, 0.9f * (-t0 * n0.z - t1 * n1.z) / nz);
            t2 = Mathf.Min(t2, 0.25f);
        }

        //lop face's normal
        var n = (t0 * n0 + t1 * n1 + t2 * n2).normalized;
        // Debug.Log($"n: {n}");

        //our plane will be centered at p - d * n, where d does not exceed minDist to a neighbor
        var dist0 = Vector3.Dot(p - p0, n);
        var dist1 = Vector3.Dot(p - p1, n);
        var dist2 = Vector3.Dot(p - p2, n);
        var minDist = Mathf.Min(dist0, Mathf.Min(dist1, dist2));
        var d = MathTools.RandomFloat(0.5f * minDist, 0.95f * minDist);
        // Debug.Log($"minDist {minDist}");

        //new vertices replacing p
        var q0 = Vector3.Lerp(p, p0, d / dist0);
        var q1 = Vector3.Lerp(p, p1, d / dist1);
        var q2 = Vector3.Lerp(p, p2, d / dist2);

        // Debug.Log($"Lopped vertex {p} with neighbors {p0}, {p1}, {p2}");
        // Debug.Log($"New face {q0}, {q1}, {q2}");

        //now the hard part -- entering the data :P
        var w0 = v;
        var w1 = vertex.Length;
        var w2 = w1 + 1;
        vertex[v] = q0;//stick q0 in here rather than remove, so other vertices stay at same index
        vertex.Add(q1);//add q1, q2 at the end of the list
        vertex.Add(q2);

        //update neighbors of p1, p2 (p0's nbrs already accurate, since q0 went in the old vert's slot)
        ReplaceNeighbor(v1, v, w1);
        ReplaceNeighbor(v2, v, w2);

        //neighbors of q0
        neighbor[3 * w0] = v0;
        neighbor[3 * w0 + 1] = w1;
        neighbor[3 * w0 + 2] = w2;

        //neighbors of q1
        neighbor.Add(v1);
        neighbor.Add(w2);
        neighbor.Add(w0);

        //neighbors of w2
        neighbor.Add(v2);
        neighbor.Add(w0);
        neighbor.Add(w1);

        for (int i = 0; i < faceStart.Length - 1; i++)
        {
            var start = faceStart[i];
            var end = faceStart[i + 1];
            for (int j = start; j < end; j++)
            {
                if (face[j] == v)
                {
                    int wA, wB;
                    var next = face[NextIndexInFace(j, start, end)];
                    if (next == v0)
                    {
                        wA = w1;
                        wB = w0;
                    }
                    else if (next == v1)
                    {
                        wA = w2;
                        wB = w1;
                    }
                    else//next = v2
                    {
                        wA = w0;
                        wB = w2;
                    }

                    face.InsertRange(j + 1, 1);
                    face[j] = wA;
                    face[j + 1] = wB;
                    for (int k = i + 1; k < faceStart.Length; k++)
                    {
                        faceStart[k]++;
                    }

                    break;
                }
            }
        }

        face.Add(w0);
        face.Add(w1);
        face.Add(w2);
        faceStart.Add(face.Length);

        void ReplaceNeighbor(int vert, int nbr, int newNbr)
        {
            for (int i = 0; i < 3; i++)
            {
                if (neighbor[3 * vert + i] == nbr)
                {
                    neighbor[3 * vert + i] = newNbr;
                    return;
                }
            }
        }
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