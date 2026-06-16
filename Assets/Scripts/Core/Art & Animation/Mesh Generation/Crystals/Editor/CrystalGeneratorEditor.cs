using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CanEditMultipleObjects]
[CustomEditor(typeof(CrystalGenerator))]
public class CrystalGeneratorEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        
        var defaultInspector = new VisualElement();
        InspectorElement.FillDefaultInspector(defaultInspector, serializedObject, this);
        root.Add(defaultInspector);

        var button = new Button(GenerateMesh)
        {
            text = "Generate Mesh"
        };
        root.Add(button);

        return root;
    }

    void GenerateMesh()
    {
        foreach (var t in targets)
        {
            ((CrystalGenerator)t).GenerateMesh();
        }
    }
}