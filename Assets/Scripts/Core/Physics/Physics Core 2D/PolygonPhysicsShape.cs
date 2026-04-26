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
    public bool drawGizmo;
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

    public void OnDrawGizmos(Transform transform)
    {
        if (drawGizmo && optimizedPolygon != null && optimizedPolygon.Length > 0)
        {
            Gizmos.color = Color.green;
            for (int i = 1; i < optimizedPolygon.Length; i++)
            {
                Gizmos.DrawLine(transform.TransformPoint(optimizedPolygon[i - 1]), transform.TransformPoint(optimizedPolygon[i]));
            }

            Gizmos.DrawLine(transform.TransformPoint(optimizedPolygon[^1]), transform.TransformPoint(optimizedPolygon[0]));
            //^this is why we check length > 0 (just avoid one more annoying error in the editor)
        }
    }


#if UNITY_EDITOR
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
        Undo.RecordObject(owner, "SubdividePolygon");
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

    public void GetShape(GameObject go)
    {
        Undo.RecordObject(go, "Get Physics Shape");
        switch (shapeSource)
        {
            case ShapeSource.SpriteRenderer:
                GetShapeFromSpriteRenderer(go);
                break;
            case ShapeSource.SpriteShape:
                GetShapeFromSpriteShape(go);
                break;
            case ShapeSource.SpriteShapeMeshGenerator:
                GetShapeFromSpriteShapeMeshGenerator(go);
                break;
        }
        EditorUtility.SetDirty(go);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go);
    }

    private void GetShapeFromSpriteRenderer(GameObject go)
    {
        if (go.TryGetComponent(out SpriteRenderer sr))
        {
            var shape = sr.sprite.GetPhysicsShape(0);
            Array.Resize(ref originalPolygon, shape.Length);
            shape.CopyTo(originalPolygon);
            CopyOriginalToOptimized();
        }
        else
        {
            Debug.LogWarning("No Sprite Renderer.");
        }
    }

    private void GetShapeFromSpriteShape(GameObject go)
    {
        if (go.TryGetComponent(out SpriteShapeController ssc))
        {
            originalPolygon = SplineSampler.SampleSpline(ssc.spline, spriteShapeArcLengthSamplesPerSegment, spriteShapeSamplesPerUnitArcLength)
                .Select(x => (Vector2)x).ToArray();
            CopyOriginalToOptimized();
        }
        else
        {
            Debug.LogWarning("No Sprite Shape Controller.");
        }
    }

    private void GetShapeFromSpriteShapeMeshGenerator(GameObject go)
    {
        if (go.TryGetComponent(out SpriteShapeMeshGenerator ssmg))
        {
            var perimeter = ssmg.GetPerimeter();
            Array.Resize(ref originalPolygon, perimeter.Length);
            perimeter.CopyTo(originalPolygon);
            CopyOriginalToOptimized();
        }
        else
        {
            Debug.LogWarning($"No {nameof(SpriteShapeMeshGenerator)}.");
        }
    }

    private void CopyOriginalToOptimized()
    {
        Array.Resize(ref optimizedPolygon, originalPolygon.Length);
        Array.Copy(originalPolygon, optimizedPolygon, originalPolygon.Length);
    }
#endif
}