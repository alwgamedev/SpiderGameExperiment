using Collider2DOptimization;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using Unity.U2D.Physics;
using Unity.Collections;

[Serializable]
public struct PolygonPhysicsShape
{
    public ShapeSource shapeSource;
    public float optimizationTolerance;
    public Vector2[] originalPolygon;
    public Vector2[] optimizedPolygon;
    public PolygonGeometry[] subdividedPolygon;//the final product that you want to use for creating physics bodies
    public int spriteShapeArcLengthSamplesPerSegment;
    public float spriteShapeSamplesPerUnitArcLength;

    public enum ShapeSource
    {
        SpriteRenderer, SpriteShape, SpriteShapeMeshGenerator
    }

    public static void OnDrawGizmos(ReadOnlySpan<PolygonGeometry> geometry, Matrix4x4 transformMatrix)
    {
        using (new Handles.DrawingScope(Color.green, transformMatrix))
        {
            for (int i = 0; i < geometry.Length; i++)
            {
                var geom = geometry[i];
                if (geom.isValid)
                {
                    var vertices = geom.AsReadOnlySpan();
                    var ct = geom.count;
                    for (int j = 0; j < ct - 1; j++)
                    {
                        var v0 = vertices[j];
                        var v1 = vertices[j + 1];
                        Handles.DrawLine(v0, v1);
                    }

                    var w0 = vertices[ct - 1];
                    var w1 = vertices[0];
                    Handles.DrawLine(w0, w1);
                }
            }
        }
    }

    public void OnDrawGizmos(Transform transform)
    {
        if (subdividedPolygon != null)
        {
            OnDrawGizmos(subdividedPolygon, transform.localToWorldMatrix);
        }
    }

    public void SetPolygonColliderPoints(GameObject go)
    {
        var pc = go.GetComponent<PolygonCollider2D>();
        Undo.RecordObject(pc, "Set Poly Collider Points");
        pc.points = optimizedPolygon;
        EditorUtility.SetDirty(pc);
        PrefabUtility.RecordPrefabInstancePropertyModifications(pc);
    }

    public void SubdividePolygon(UnityEngine.Object owner)
    {
        Undo.RecordObject(owner, "Subdivide Polygon");
        var temp = PolygonGeometry.CreatePolygons(optimizedPolygon, PhysicsTransform.identity, Vector2.one);
        subdividedPolygon = temp.ToArray();
        EditorUtility.SetDirty(owner);
        PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
    }

    public void OptimizeShape(UnityEngine.Object owner)
    {
        if (optimizationTolerance > 0)
        {
            Undo.RecordObject(owner, "Optimize Physics Shape");
            optimizedPolygon = ShapeOptimizationHelper.DouglasPeuckerReduction(originalPolygon.ToList(), optimizationTolerance).ToArray();
            EditorUtility.SetDirty(owner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        }
    }

    public void GetShape(UnityEngine.Object owner, GameObject source)
    {
        Undo.RecordObject(owner, "Get Physics Shape");
        switch (shapeSource)
        {
            case ShapeSource.SpriteRenderer:
                GetShapeFromSpriteRenderer(source.GetComponent<SpriteRenderer>());
                break;
            case ShapeSource.SpriteShape:
                GetShapeFromSpriteShape(source.GetComponent<SpriteShapeController>());
                break;
            case ShapeSource.SpriteShapeMeshGenerator:
                GetShapeFromSpriteShapeMeshGenerator(source.GetComponent<SpriteShapeMeshGenerator>());
                break;
        }

        CopyOriginalToOptimized();
        EditorUtility.SetDirty(owner);
        PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
    }

    public void GetShapeFromSpriteRenderer(SpriteRenderer sr)
    {
        var shape = sr.sprite.GetPhysicsShape(0);
        Array.Resize(ref originalPolygon, shape.Length);
        shape.CopyTo(originalPolygon);
    }

    public void GetShapeFromSpriteShape(SpriteShapeController ssc)
    {
        var splineSample = new NativeList<Vector2>(Allocator.Temp);
        SplineSampler.SampleSpline(ssc.spline, spriteShapeArcLengthSamplesPerSegment, spriteShapeSamplesPerUnitArcLength, splineSample);
        originalPolygon = splineSample.ToArray();
    }

    public void GetShapeFromSpriteShapeMeshGenerator(SpriteShapeMeshGenerator ssmg)
    {
        var perimeter = ssmg.GetPerimeter();
        Array.Resize(ref originalPolygon, perimeter.Length);
        perimeter.CopyTo(originalPolygon);
    }

    private void CopyOriginalToOptimized()
    {
        Array.Resize(ref optimizedPolygon, originalPolygon.Length);
        Array.Copy(originalPolygon, optimizedPolygon, originalPolygon.Length);
    }

    //Sven Skyum, "A simple algorithm for computing the smallest enclosing circle" (1990)
    public static CircleGeometry SmallestEnclosingCircle(ReadOnlySpan<Vector2> vertex)
    {
        int excluded = 0;
        int curCount = vertex.Length;
        int originalCount = curCount;

        int bestIndex = 0;
        int bestIndexPrev = vertex.Length - 1;
        int bestIndexNext = 1;

        var maxRadius2 = Mathf.NegativeInfinity;
        var minCos = Mathf.Infinity;//min cos <-> max angle

        int prev = vertex.Length - 1;
        int cur = 0;
        int next = 1;
        int seen = 0;

        while (curCount > 3)
        {
            //loop through vertices and find max (radius, angle)
            seen++;
            var p = vertex[cur];
            var pPrev = vertex[prev];
            var pNext = vertex[next];

            var rad2 = MathTools.CircumradiusSquared(p, pPrev, pNext);
            var cos = Vector2.Dot(pPrev - p, pNext - p);

            if (rad2 > maxRadius2 || (rad2 == maxRadius2 && cos < minCos))
            {
                bestIndex = cur;
                bestIndexPrev = prev;
                bestIndexNext = next;

                maxRadius2 = rad2;
                minCos = cos;
            }

            if (seen == curCount)//we've finished looking through all the remaining vertices and found the highest (radius, angle)
            {
                if (minCos >= 0)
                {
                    return Circumcircle(vertex[bestIndex], vertex[bestIndexPrev], vertex[bestIndexNext]);
                }

                //remove vertex[bestIndex]
                excluded |= 1 << bestIndex;
                curCount--;
                cur = Next(cur, excluded, originalCount);
                next = Next(cur, excluded, originalCount);
                prev = Prev(cur, excluded, originalCount);
                seen = 0;

                //reset bests
                maxRadius2 = Mathf.NegativeInfinity;
                minCos = Mathf.Infinity;
            }
            else
            {
                //increment cur
                prev = cur;
                cur = next;
                next = Next(cur, excluded, originalCount);
            }
        }

        //3 vertices remaining
        return Circumcircle(vertex[cur], vertex[prev], vertex[next]);

        static CircleGeometry Circumcircle(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            var circumcenter = MathTools.Circumcenter(p0, p1, p2);
            var radius = Vector2.Distance(p0, circumcenter);
            return new() { center = circumcenter, radius = radius };
        }

        static int Next(int i, int excluded, int count)
        {
            int next = i < count - 1 ? i + 1 : 0;
            while (Excluded(next, excluded))
            {
                next = next < count - 1 ? next + 1 : 0;
            }

            return next;
        }

        static int Prev(int i, int excluded, int count)
        {
            int prev = i > 0 ? i - 1 : count - 1;
            while (Excluded(prev, excluded))
            {
                prev = prev > 0 ? prev - 1 : count - 1;
            }

            return prev;
        }

        static bool Excluded(int i, int excluded)
        {
            return (excluded & 1 << i) != 0;
        }
    }
}