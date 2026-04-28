using Collider2DOptimization;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using Unity.U2D.Physics;

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
                if (geometry[i].isValid)
                {
                    var vertices = geometry[i].AsReadOnlySpan();
                    var ct = geometry[i].count;
                    for (int j = 0; j < ct - 1; j++)
                    {
                        Handles.DrawLine(vertices[j], vertices[j + 1]);
                    }
                    Handles.DrawLine(vertices[ct - 1], vertices[0]);
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
        //if (optimizedPolygon != null && optimizedPolygon.Length > 0)
        //{
        //    Gizmos.color = Color.green;
        //    for (int i = 1; i < optimizedPolygon.Length; i++)
        //    {
        //        Gizmos.DrawLine(transform.TransformPoint(optimizedPolygon[i - 1]), transform.TransformPoint(optimizedPolygon[i]));
        //    }

        //    Gizmos.DrawLine(transform.TransformPoint(optimizedPolygon[^1]), transform.TransformPoint(optimizedPolygon[0]));
        //    //^this is why we check length > 0 (just avoid one more annoying error in the editor)
        //}
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
        EditorUtility.SetDirty(owner);
        PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
    }

    public void GetShapeFromSpriteRenderer(SpriteRenderer sr)
    {
        var shape = sr.sprite.GetPhysicsShape(0);
        Array.Resize(ref originalPolygon, shape.Length);
        shape.CopyTo(originalPolygon);
        CopyOriginalToOptimized();
    }

    public void GetShapeFromSpriteShape(SpriteShapeController ssc)
    {
        originalPolygon = SplineSampler.SampleSpline(ssc.spline, spriteShapeArcLengthSamplesPerSegment, spriteShapeSamplesPerUnitArcLength)
            .Select(x => (Vector2)x).ToArray();
        CopyOriginalToOptimized();
    }

    public void GetShapeFromSpriteShapeMeshGenerator(SpriteShapeMeshGenerator ssmg)
    {
        var perimeter = ssmg.GetPerimeter();
        Array.Resize(ref originalPolygon, perimeter.Length);
        perimeter.CopyTo(originalPolygon);
        CopyOriginalToOptimized();
    }

    private void CopyOriginalToOptimized()
    {
        Array.Resize(ref optimizedPolygon, originalPolygon.Length);
        Array.Copy(originalPolygon, optimizedPolygon, originalPolygon.Length);
    }
}