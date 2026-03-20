using Collider2DOptimization;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using Unity.U2D.Physics;

public class PolygonPhysicsShape : MonoBehaviour
{
    [SerializeField] bool drawGizmo;
    [SerializeField] ShapeSource shapeSource;
    [SerializeField] float optimizationTolerance;
    [SerializeField] Vector2[] originalPolygon;
    [SerializeField] Vector2[] optimizedPolygon;
    [SerializeField] PolygonGeometry[] subdividedPolygon;
    [SerializeField] int spriteShapeArcLengthSamplesPerSegment = 25;
    [SerializeField] float spriteShapeSamplesPerUnitArcLength = 12;

    enum ShapeSource
    {
        SpriteRenderer, SpriteShape, SpriteShapeMeshGenerator
    }

    private void OnDrawGizmos()
    {
        if (drawGizmo && optimizedPolygon != null && optimizedPolygon.Length > 0)
        {
            Gizmos.color = Color.green;
            for (int i = 1; i <  optimizedPolygon.Length; i++)
            {
                Gizmos.DrawLine(transform.TransformPoint(optimizedPolygon[i - 1]), transform.TransformPoint(optimizedPolygon[i]));
            }

            Gizmos.DrawLine(transform.TransformPoint(optimizedPolygon[^1]), transform.TransformPoint(optimizedPolygon[0]));
            //^this is why we check length > 0 (just avoid one more annoying error in the editor)
        }
    }

    public PolygonGeometry[] GetSubdividedPolygon() => subdividedPolygon;
    public ReadOnlySpan<Vector2> GetPolygon() => optimizedPolygon;

#if UNITY_EDITOR
    public void SetPolygonColliderPoints()//for use with ShadowCaster2D...
    {
        var pc = GetComponent<PolygonCollider2D>();
        Undo.RecordObject(pc, "Set Poly Collider Points");
        pc.points = optimizedPolygon;
        EditorUtility.SetDirty(pc);
        PrefabUtility.RecordPrefabInstancePropertyModifications(pc);
    }

    public void SubdividePolygon()
    {
        Undo.RecordObject(this, "SubdividePolygon");
        var temp = PolygonGeometry.CreatePolygons(optimizedPolygon, PhysicsTransform.identity, Vector2.one);
        subdividedPolygon = temp.ToArray();
        EditorUtility.SetDirty(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    public void OptimizeShape()
    {
        if (optimizationTolerance > 0)
        {
            Undo.RecordObject(this, "Optimize Physics Shape");
            optimizedPolygon = ShapeOptimizationHelper.DouglasPeuckerReduction(originalPolygon.ToList(), optimizationTolerance).ToArray();
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }
    }

    public void GetShape()
    {
        Undo.RecordObject(this, "Get Physics Shape");
        switch(shapeSource)
        {
            case ShapeSource.SpriteRenderer:
                GetShapeFromSpriteRenderer();
                break;
            case ShapeSource.SpriteShape:
                GetShapeFromSpriteShape();
                break;
            case ShapeSource.SpriteShapeMeshGenerator:
                GetShapeFromSpriteShapeMeshGenerator();
                break;
        }
        EditorUtility.SetDirty(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    private void GetShapeFromSpriteRenderer()
    {
        if (TryGetComponent(out SpriteRenderer sr))
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

    private void GetShapeFromSpriteShape()
    {
        if (TryGetComponent(out SpriteShapeController ssc))
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

    private void GetShapeFromSpriteShapeMeshGenerator()
    {
        if (TryGetComponent(out SpriteShapeMeshGenerator ssmg))
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