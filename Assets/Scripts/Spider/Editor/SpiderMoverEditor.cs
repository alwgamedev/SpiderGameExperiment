using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpiderMover))]
public class SpiderMoverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Center Physics Bodies"))
        {
            ((SpiderMover)target).SpideyPhysics.CenterRootTransforms();
        }
    }
}