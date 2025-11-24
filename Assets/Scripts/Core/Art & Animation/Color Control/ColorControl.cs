using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

public class ColorControl : MonoBehaviour
{
    [SerializeField] Color color;

    public Color Color => color;

    //we never lose listeners (even after editing this script)!
    public UnityEvent ColorChanged;
    public UnityEvent AutoDetermineChildData;
    public UnityEvent<ColorControl> Destroyed;

    public void SetColorAndUpdateChildren(Color c, bool incrementUndoGroup = true)
    {
        color = c;
        UpdateChildColors(incrementUndoGroup);
    }

    public void UpdateChildColors(bool incrementUndoGroup = true)
    {
#if UNITY_EDITOR
        if (incrementUndoGroup)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Update Child Colors");
        }
#endif
        ColorChanged.Invoke();
    }

    public void AutoDetermineChildShiftAndMult(bool incrementUndoGroup = true)
    {
#if UNITY_EDITOR
        if (incrementUndoGroup)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Set Child Color Shift & Multiplier");
        }
#endif
        AutoDetermineChildData.Invoke();
    }

    private void OnDestroy()
    {
        Destroyed.Invoke(this);
    }
}
