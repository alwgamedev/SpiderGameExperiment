using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.VFX;

[Serializable]
public class VFXGraphicsBuffer<T> where T : unmanaged
{
    public VisualEffect vfx;
    public GraphicsBufferWrapper<T> bufferWrapper;
    public string bufferPropertyName;
    
    int propertyID;
    int PropertyID()
    {
        if (propertyID == 0 && !string.IsNullOrWhiteSpace(bufferPropertyName))
        {
            propertyID = Shader.PropertyToID(bufferPropertyName);
        }
        return propertyID;
    }

    public void OnValidate()
    {
        bufferWrapper.OnValidate();
    }
    
    public void InitializeBuffer()
    {
        bufferWrapper.InitializeBuffer();
    }

    public void AssignBufferToVFX()
    {
        var id = PropertyID();
        if (id == 0)
        {
            return;
        }

        vfx.SetGraphicsBuffer(id, bufferWrapper.buffer);
    }

    public void OnDisable()
    {
        bufferWrapper.ReleaseBuffer();
    }


    public void SetData(NativeArray<T> data)
    {
        if (!bufferWrapper.BufferValid)
        {
            InitializeBuffer();
            AssignBufferToVFX();
        }

        bufferWrapper.buffer.SetData(data);
    }
}