using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

public static class TextureGeneration
{
    public static void GenerateAndSaveTexture(Material material, int texWidth, int texHeight, TextureFormat format)
    {
        var tex = GenerateTexture(material, texWidth, texHeight, format);
        if (!tex)
        {
            return;
        }

        SaveTexture(tex, format);
        Object.DestroyImmediate(tex);

        // var png = tex.EncodeToPNG();
        // Object.DestroyImmediate(tex);

        // SaveTexture(png);
    }

    /// <summary> Remember to destroy the texture when you're done using it. </summary>
    public static Texture2D GenerateTexture(Material material, int texWidth, int texHeight, TextureFormat format)
    {
        if (!material || texWidth <= 0 || texHeight <= 0)
        {
            Debug.LogWarning("Invalid texture parameters.");
            return null;
        }

        var graphicsFormat = GraphicsFormatUtility.GetGraphicsFormat(format, false);
        var rtFormat = GraphicsFormatUtility.GetRenderTextureFormat(graphicsFormat);
        RenderTexture rt = RenderTexture.GetTemporary(texWidth, texHeight, 0, rtFormat, RenderTextureReadWrite.Linear);
        Graphics.Blit(null, rt, material, 0);

        Texture2D tex = new(texWidth, texHeight, format, false, true);

        var cur = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, texWidth, texHeight), 0, 0);
        RenderTexture.active = cur;

        RenderTexture.ReleaseTemporary(rt);

        return tex;
    }

    public static void SaveTexture(Texture2D texture, TextureFormat format)
    {
        bool singlePrecision = format == TextureFormat.RFloat || format == TextureFormat.RGFloat || format == TextureFormat.RGBAFloat;
        bool encodetoEXR = singlePrecision || format == TextureFormat.RHalf || format == TextureFormat.R16
            || format == TextureFormat.RGHalf || format == TextureFormat.RG16 || format == TextureFormat.RGBAHalf;

        byte[] bytes;
        if (encodetoEXR)
        {
            var flag = singlePrecision ? Texture2D.EXRFlags.OutputAsFloat : Texture2D.EXRFlags.None;
            bytes = texture.EncodeToEXR(flag);
        }
        else
        {
            bytes = texture.EncodeToPNG();
        }

        var ext = encodetoEXR ? "exr" : "png";
        var path = EditorUtility.SaveFilePanel("Save Texture", "Assets/", "Unnamed Texture", ext);
        //^and this gives you a warning if asset already exists at that path, which is nice
        if (string.IsNullOrWhiteSpace(path))//panel was closed without selecting path
        {
            return;
        }

        File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();
    }
}