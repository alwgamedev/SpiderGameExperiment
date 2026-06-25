using System;
using UnityEngine;

[Serializable]
public class GraphicsBufferWrapper<T>
{
    [SerializeField] int bufferCount;

    public GraphicsBuffer buffer;

    public int BufferCount
    {
        get
        {
            bufferCount = Mathf.Max(bufferCount, 1);
            return bufferCount;
        }
    }
    public bool BufferValid => buffer != null && buffer.IsValid() && buffer.count == BufferCount;

    public void OnValidate()
    {
        bufferCount = Mathf.Max(bufferCount, 1);
    }

    public void InitializeBuffer()
    {
        ReleaseBuffer();
        buffer = new(GraphicsBuffer.Target.Structured, bufferCount, MiscTools.Stride<T>());
    }

    public void ReleaseBuffer()
    {
        buffer?.Release();
        buffer = null;
    }
}