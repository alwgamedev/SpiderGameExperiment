using System;
using UnityEngine;

public class ColorControl : MonoBehaviour
{
    //Renderer _renderer;

    //Renderer Renderer
    //{
    //    get
    //    {
    //        if (_renderer == null)
    //        {
    //            _renderer = GetComponent<Renderer>();
    //        }

    //        return _renderer;
    //    }
    //}

    [SerializeField] Color color;

    public Color Color => color;

    public event Action ColorChanged;
    public event Action AutoDetermineChildData;
    public event Action<ColorControl> Destroyed;

    public void SetColorAndUpdateChildren(Color c)
    {
        color = c;
        UpdateChildColors();
    }

    public void UpdateChildColors()
    {
        ColorChanged?.Invoke();
    }

    public void AutoDetermineChildShiftAndMult()
    {
        AutoDetermineChildData?.Invoke();
    }

    private void OnDestroy()
    {
        Destroyed?.Invoke(this);
        //Destroyed = null;
        //ColorChanged = null;
        //AutoDetermineChildData = null;
    }
}
