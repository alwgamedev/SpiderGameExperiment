using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class TextureGeneratorWindow : EditorWindow
{
    [MenuItem("Window/Texture Generator")]
    public static void OpenWindow()
    {
        GetWindow<TextureGeneratorWindow>("Texture Generator");
    }

    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.paddingTop = 25;
        root.style.paddingLeft = 25;

        var matField = new ObjectField("Material") { objectType = typeof(Material), allowSceneObjects = false };
        root.Add(matField);

        var widthField = new IntegerField("Width") { value = 512 };
        root.Add(widthField);

        var heightField = new IntegerField("Height") { value = 512 };
        root.Add(heightField);

        var formatField = new EnumField("Texture Format", TextureFormat.RGBA32);
        root.Add(formatField);

        var button = new Button(() => 
            TextureGeneration.GenerateAndSaveTexture((Material)matField.value, widthField.value, 
            heightField.value, (TextureFormat)formatField.value))
        {
            text = "Generate"
        };
        root.Add(button);
    }
}