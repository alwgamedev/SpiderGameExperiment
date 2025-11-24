using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.U2D;

public class ChildColor : MonoBehaviour
{
    [SerializeField] ColorControl colorControl;
    [SerializeField] Color colorShift = Color.clear;
    [SerializeField] Color colorMultiplier = Color.white;

    Renderer _renderer;
    internal ColorControl subscription;
    //internal gets auto-serialized like public but is not accessible by editor assembly so stays hidden from inspector unless in debug mode
    //([HideInInspector] seems to also hide in debug mode)

    Renderer Renderer
    {
        get
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
            }
            return _renderer;
        }
    }

    private void OnValidate()
    {
        if (!subscription || subscription != colorControl)
        {
            HookupControl();
        }
    }

    public void HookupControl()
    {
        UnhookControl();

        if (colorControl)
        {
            subscription = colorControl;
            UnityEventTools.AddPersistentListener(colorControl.ColorChanged, UpdateColor);
            colorControl.ColorChanged.SetPersistentListenerState(colorControl.ColorChanged.GetPersistentEventCount() - 1, 
                UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
            UnityEventTools.AddPersistentListener(colorControl.AutoDetermineChildData, AutoDetermineShiftAndMult);
            colorControl.AutoDetermineChildData.SetPersistentListenerState(colorControl.AutoDetermineChildData.GetPersistentEventCount() - 1, 
                UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
            UnityEventTools.AddPersistentListener(colorControl.Destroyed, OnControlDestroyed);
            colorControl.Destroyed.SetPersistentListenerState(colorControl.Destroyed.GetPersistentEventCount() - 1, 
                UnityEngine.Events.UnityEventCallState.EditorAndRuntime);

        }
    }

    //useful in case you have already set up the colors manually
    //and are installing the ColorControl system afterwards
    //(so you can just have it figure out what shift and multiplier need to be)
    public void AutoDetermineShiftAndMult()
    {
        if (colorControl)
        {
            AutoDetermineShiftAndMult(colorControl.Color);
        }
    }

    public void UpdateColor()
    {
        Debug.Log($"{GetType().Name} {name} updating color");
        if (colorControl)
        {
            var color = (colorControl.Color + colorShift) * colorMultiplier;
            SetRendererColor(color);
        }
        else
        {
            Debug.Log("no private _colorControl!");
        }
    }

    private bool TryGetRendererColor(out Color color)
    {
        var r = Renderer;
        if (r != null)
        {
            if (r is SpriteRenderer s)
            {
                color = s.color;
                return true;
            }
            else if (r is SpriteShapeRenderer t)
            {
                color = t.color;
                return true;
            }
        }

        color = Color.white;
        return false;
    }

    private void AutoDetermineShiftAndMult(Color controlColor)
    {
        if (TryGetRendererColor(out Color childColor))
        {
            Color shift = Color.clear;
            Color mult = Color.white;
            if (controlColor.r != 0)
            {
                mult.r = childColor.r / controlColor.r;
            }
            else if (childColor.r != 0)
            {
                shift.r = childColor.r;
            }
            if (controlColor.g != 0)
            {
                mult.g = childColor.g / controlColor.g;
            }
            else if (childColor.g != 0)
            {
                shift.g = childColor.g;
            }
            if (controlColor.b != 0)
            {
                mult.b = childColor.b / controlColor.b;
            }
            else if (childColor.b != 0)
            {
                shift.b = childColor.b;
            }
            if (controlColor.a != 0)
            {
                mult.a = childColor.a / controlColor.a;
            }
            else if (childColor.a != 0)
            {
                shift.a = childColor.a;
            }

#if UNITY_EDITOR
            Undo.RecordObject(this, "Set Child Color Shift & Multiplier");
#endif
            colorShift = shift;
            colorMultiplier = mult;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
#endif
        }
    }

    private void SetRendererColor(Color color)
    {
        var r = Renderer;
        if (r != null)
        {
            if (r is SpriteRenderer s)
            {
#if UNITY_EDITOR
                Undo.RecordObject(s, "Set Renderer Color");
#endif
                s.color = color;
#if UNITY_EDITOR
                EditorUtility.SetDirty(s);
                PrefabUtility.RecordPrefabInstancePropertyModifications(s);
#endif
            }
            else if (r is SpriteShapeRenderer t)
            {
#if UNITY_EDITOR
                Undo.RecordObject(t, "Set Renderer Color");
#endif
                t.color = color;
#if UNITY_EDITOR
                EditorUtility.SetDirty(t);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
#endif
            }
        }
    }

    private void OnControlDestroyed(ColorControl c)
    {
        if (!subscription || colorControl == c)
        {
            UnhookControl();
        }
    }

    private void UnhookControl()
    {
        if (subscription)
        {
            UnityEventTools.RemovePersistentListener(subscription.ColorChanged, UpdateColor);
            UnityEventTools.RemovePersistentListener(subscription.AutoDetermineChildData, AutoDetermineShiftAndMult);
            UnityEventTools.RemovePersistentListener<ColorControl>(subscription.Destroyed, OnControlDestroyed);
        }
    }

    private void OnDestroy()
    {
        UnhookControl();
    }
}