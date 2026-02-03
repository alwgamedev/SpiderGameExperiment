using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(DoubleDoor))]
public class DoubleDoorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Capture Door 1 Open Angle"))
        {
            foreach (var t in targets)
            {
                ((DoubleDoor)t).CaptureDoor1Open();
            }
        }
        
        if (GUILayout.Button("Capture Door 1 Closed Angle"))
        {
            foreach (var t in targets)
            {
                ((DoubleDoor)t).CaptureDoor1Closed();
            }
        }

        if (GUILayout.Button("Capture Door 2 Open Angle"))
        {
            foreach (var t in targets)
            {
                ((DoubleDoor)t).CaptureDoor2Open();
            }
        }

        if (GUILayout.Button("Capture Door 2 Closed Angle"))
        {
            foreach (var t in targets)
            {
                ((DoubleDoor)t).CaptureDoor2Closed();
            }
        }
    }
}