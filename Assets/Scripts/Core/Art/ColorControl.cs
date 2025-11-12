using System;
using UnityEngine;
using UnityEngine.U2D;

public class ColorControl : MonoBehaviour
{
    Renderer _renderer;

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

    public event Action<Color> ColorChanged;
    public event Action<Color> AutoDetermineChildData;
    public event Action<ColorControl> Destroyed;

    public void UpdateChildColors()
    {
        if (TryGetRendererColor(out Color color))
        {
            ColorChanged?.Invoke(color);
        }
    }

    public void RequestChildrenAutoDetermineData()
    {
        if (TryGetRendererColor(out Color color))
        {
            AutoDetermineChildData?.Invoke(color);
        }
    }

    public bool TryGetRendererColor(out Color color)
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

    private void OnDestroy()
    {
        Destroyed?.Invoke(this);
        //Destroyed = null;
        //ColorChanged = null;
        //AutoDetermineChildData = null;
    }
}
