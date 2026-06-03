using System.IO;
using UnityEditor;
using UnityEngine;

public static class TextureGeneration
{
    public static void GenerateAndSaveTexture(Material material, int texWidth, int texHeight)
    {
        var tex = GenerateTexture(material, texWidth, texHeight);
        if (!tex)
        {
            return;
        }

        var png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        SaveTexture(png);
    }

    /// <summary> Remember to destroy the texture when you're done using it. </summary>
    public static Texture2D GenerateTexture(Material material, int texWidth, int texHeight)
    {
        if (!material || texWidth <= 0 || texHeight <= 0)
        {
            Debug.LogWarning("Invalid texture parameters.");
            return null;
        }

        RenderTexture rt = RenderTexture.GetTemporary(texWidth, texHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(null, rt, material, 0);

        Texture2D tex = new(texWidth, texHeight, TextureFormat.RGBA32, false, true);

        var cur = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, texWidth, texHeight), 0, 0);
        RenderTexture.active = cur;

        RenderTexture.ReleaseTemporary(rt);

        return tex;
    }

    public static void SaveTexture(byte[] pngBytes)
    {
        var path = EditorUtility.SaveFilePanel("Save PNG", "Assets/", "Unnamed PNG", "png");
        //^and this gives you a warning if asset already exists at that path, which is nice
        if (string.IsNullOrWhiteSpace(path))//panel was closed without selecting path
        {
            return;
        }

        File.WriteAllBytes(path, pngBytes);
        AssetDatabase.Refresh();
    }
}