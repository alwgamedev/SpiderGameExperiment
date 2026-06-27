using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(Spider))]
public class SpiderEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();

        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        var spider = (Spider)target;

        root.Add(new Button(() => spider.mover.CenterPhysicsBodies())
        {
            text = "Center Physics Bodies"
        });

        root.Add(new Button(() => spider.mover.CreateLegPhysicsBodies(spider))
        {
            text = "Create Leg Physics Bodies"
        });

        root.Add(new Button(() => spider.mover.CenterLegPhysicsBodies())
        {
            text = "Center Leg Physics Bodies"
        });

        root.Add(new Button(() => spider.grabber.CenterArmTransforms())
        {
            text = "Center Grabber Arm Physics Bodies"
        });

        root.Add(new Button(BakeRopeMesh) { text = "Bake Rope Mesh" });

        return root;
    }

    private static void BakeRopeMesh()
    {
        var mesh = RopeRenderer.BakeMesh(GrappleCannon.NUM_GRAPPLE_NODES, GrappleCannon.NUM_ENDCAP_TRIANGLES);
        if (!EditorTools.CreateAndSaveAsset(mesh))
        {
            DestroyImmediate(mesh);
        }
    }
}