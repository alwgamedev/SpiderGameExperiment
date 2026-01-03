using System;
using System.Runtime.InteropServices;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public static class ShaderTools
{
    public static void SetBuffer(this ComputeShader computeShader, ComputeBuffer computeBuffer, string name, params int[] kernelIndices)
    {
        for (int i = 0; i < kernelIndices.Length; i++)
        {
            computeShader.SetBuffer(kernelIndices[i], name, computeBuffer);
        }
    }

    public static void SetTexture(this ComputeShader computeShader, RenderTexture texture, string name, params int[] kernelIndices)
    {
        for (int i = 0; i < kernelIndices.Length; i++)
        {
            computeShader.SetTexture(kernelIndices[i], name, texture);
        }
    }
}