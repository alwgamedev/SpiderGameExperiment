using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(SpiderMover))]
public class SpiderMoverEditor : Editor
{
    //public override void OnInspectorGUI()
    //{
    //    DrawDefaultInspector();

    //    if (GUILayout.Button("Center Physics Bodies"))
    //    {
    //        ((SpiderMover)target).SpideyPhysics.CenterRootTransforms();
    //    }
    //}

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();

        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        root.Add(new Button(() => ((SpiderMover)target).CenterPhysicsBodies())
        {
            text = "Center Physics Bodies"
        });

        root.Add(new Button(() => ((SpiderMover)target).CreateLegPhysicsBodies())
        {
            text = "Create Leg Physics Bodies"
        });

        root.Add(new Button(() => ((SpiderMover)target).CenterLegPhysicsBodies())
        {
            text = "Center Leg Physics Bodies"
        });

        return root;
    }
}