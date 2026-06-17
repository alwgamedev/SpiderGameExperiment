using UnityEngine;

public class CrystalSpike : MonoBehaviour
{
    [SerializeField] MeshRenderer meshRenderer;
    [SerializeField] Vector2 maxStretch;
    [SerializeField] float stretchTime;
    [SerializeField] float restTime;

    int stretchProperty = Shader.PropertyToID("Stretch");
    float timer;//will always be 0 - 1
    float stretchRate;
    Vector2 stretchGoal;
    Material material;

    private void Start()
    {
        material = new Material(meshRenderer.sharedMaterial);
        meshRenderer.sharedMaterial = material;
    }

    private void OnDestroy()
    {
        Destroy(material);
        
    }
}