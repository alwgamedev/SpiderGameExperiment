using UnityEngine;

[ExecuteAlways]
public class LightGroup : MonoBehaviour
{
    [SerializeField] Light2DIntensityScaler[] lights;
    [SerializeField] float goalIntensity;

    private void OnValidate()
    {
        UpdateAllLights();
    }

    protected void OnDidApplyAnimationProperties()
    {
        UpdateAllLights();
    }

    private void UpdateAllLights()
    {
        if (lights != null)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i])
                {
                    lights[i].goalIntensity = goalIntensity;
                    lights[i].UpdateIntensity();
                }
            }
        }
    }
}