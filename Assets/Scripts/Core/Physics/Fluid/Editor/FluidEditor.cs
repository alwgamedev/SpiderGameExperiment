using UnityEngine;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditor(typeof(Fluid))]
public class FluidEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Create Mesh"))
        {
            foreach (var t in targets)
            { 
                ((Fluid)t).CreateMesh();
            }
        }
    }
}
