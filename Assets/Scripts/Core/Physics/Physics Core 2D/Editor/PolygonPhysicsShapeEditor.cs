using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CanEditMultipleObjects]
[CustomEditor(typeof(PolygonPhysicsShapeComponent))]
public class PolygonPhysicsShapeEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();

        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        var getShapeButton = new Button(GetShapes) { text = "Get Shape" };
        var optimizeShapeButton = new Button(OptimizeShapes) { text = "Optimize Shape" };
        var subdivideShapeButton = new Button(SubdivideShapes) { text = "Subdivide Shape" };
        var setPCPointsButton = new Button(SetPolygonColliderPoints) { text = "Set Polygon Collider Points" };

        root.Add(getShapeButton);
        root.Add(optimizeShapeButton);
        root.Add(subdivideShapeButton);
        root.Add(setPCPointsButton);

        return root;
    }

    private void GetShapes()
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var c = targets[i] as PolygonPhysicsShapeComponent;
            if (c)
            {
                c.pps.GetShape(c, c.Source, c.transform.localToWorldMatrix);
            }
        }
    }

    private void OptimizeShapes()
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var c = targets[i] as PolygonPhysicsShapeComponent;
            if (c)
            {
                c.pps.OptimizeShape(c);
            }
        }
    }

    private void SubdivideShapes()
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var c = targets[i] as PolygonPhysicsShapeComponent;
            if (c)
            {
                c.pps.SubdividePolygon(c);
            }
        }
    }

    private void SetPolygonColliderPoints()
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var c = targets[i] as PolygonPhysicsShapeComponent;
            if (c)
            {
                c.pps.SetPolygonColliderPoints(c.gameObject);
            }
        }
    }

    //public override void OnInspectorGUI()
    //{
    //    DrawDefaultInspector();

    //    if (GUILayout.Button("Get Shape"))
    //    {
    //        foreach (var t in targets)
    //        {
    //            var comp = (PolygonPhysicsShapeComponent)t;
    //            comp.pps.GetShape(comp, comp.gameObject);
    //        }
    //    }

    //    if (GUILayout.Button("Optimize Shape"))
    //    {
    //        foreach (var t in targets)
    //        {
    //            var comp = (PolygonPhysicsShapeComponent)t;
    //            comp.pps.OptimizeShape(comp);
    //        }
    //    }

    //    if (GUILayout.Button("Subdivide"))
    //    {
    //        foreach (var t in targets)
    //        {
    //            var comp = (PolygonPhysicsShapeComponent)t;
    //            comp.pps.SubdividePolygon(comp);
    //        }
    //    }

    //    if (GUILayout.Button("Set Polygon Collider Points"))
    //    {
    //        foreach (var t in targets)
    //        {
    //            var comp = (PolygonPhysicsShapeComponent)t;
    //            comp.pps.SetPolygonColliderPoints(comp.gameObject);
    //        }
    //    }
    //}
}
