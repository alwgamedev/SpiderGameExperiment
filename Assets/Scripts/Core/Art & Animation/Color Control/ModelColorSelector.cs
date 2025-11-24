using System;
using UnityEditor;
using UnityEngine;

//just a wrapper for a group of ColorControls (so you can access them all in one place and set them all at once)
public class ModelColorSelector : MonoBehaviour
{
    [SerializeField] ColorCategory[] categories;

    [Serializable]
    struct ColorCategory
    {
        public string name;
        public Color color;
        public ColorControl[] heads;

        public void AutoDetermineChildShiftAndMult()
        {
            foreach (var head in heads)
            {
                if (head)
                {
                    head.AutoDetermineChildShiftAndMult(false);
                }
            }
        }

        public void UpdateColors()
        {
            foreach (var head in heads)
            {
                if (head)
                {
                    head.SetColorAndUpdateChildren(color, false);
                }
            }
        }
    }

    public void AutoDeterminateChildShiftAndMult()
    {
#if UNITY_EDITOR
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Set Model Child Colors' Shift & Multiplier");
#endif
        foreach (var c in categories)
        {
            c.AutoDetermineChildShiftAndMult();
        }
    }

    public void UpdateColors()
    {
#if UNITY_EDITOR
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Update Model Colors");
#endif
        foreach (var c in categories)
        {
            c.UpdateColors();
        }

#if UNITY_EDITOR
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
#endif
    }

    public void SetAllColorFieldsToWhite()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, $"Set all color selector fields to white");
#endif
        for (int i = 0; i < categories.Length; i++)
        {
            categories[i].color = Color.white;
        }
    }
}
