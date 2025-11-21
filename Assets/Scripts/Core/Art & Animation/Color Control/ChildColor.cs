using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

public class ChildColor : MonoBehaviour
{
    [SerializeField] ColorControl colorControl;
    [SerializeField] Color colorShift = Color.clear;
    [SerializeField] Color colorMultiplier = Color.white;

    Renderer _renderer;
    ColorControl _colorControl;

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
        if (_colorControl != colorControl)
        {
            HookupControl();
        }
    }

    public void HookupControl()
    {
        UnhookControl();

        if (colorControl)
        {
            _colorControl = colorControl;
            colorControl.ColorChanged += UpdateColor;
            colorControl.AutoDetermineChildData += AutoDetermineShiftAndMult;
            colorControl.Destroyed += OnControlDestroyed;
        }
    }

    //useful in case you have already set up the colors manually
    //and are installing the ColorControl system afterwards
    //(so you can just have it figure out what shift and multiplier need to be)
    public void AutoDetermineShiftAndMult()
    {
        if (_colorControl)
        {
            AutoDetermineShiftAndMult(_colorControl.Color);
        }
    }

    public void UpdateColor()
    {
        Debug.Log($"{GetType().Name} {name} updating color");
        if (_colorControl)
        {
            var color = (_colorControl.Color + colorShift) * colorMultiplier;
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
                s.color = color;
#if UNITY_EDITOR
                EditorUtility.SetDirty(s);
                PrefabUtility.RecordPrefabInstancePropertyModifications(s);
#endif
            }
            else if (r is SpriteShapeRenderer t)
            {
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
        if (!_colorControl || _colorControl == c)
        {
            UnhookControl();
        }
    }

    private void UnhookControl()
    {
        if (_colorControl)
        {
            _colorControl.ColorChanged -= UpdateColor;
            _colorControl.AutoDetermineChildData -= AutoDetermineShiftAndMult;
            _colorControl.Destroyed -= OnControlDestroyed;
        }

        _colorControl = null;
    }

    private void OnDestroy()
    {
        UnhookControl();
    }
}