using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class Light2DIntensityScaler : MonoBehaviour
{
    public bool scaleMode;
    public float goalIntensity;

    Light2D light2D;

    Light2D Light
    {
        get
        {
            if (light2D == null)
            {
                light2D = GetComponent<Light2D>();
            }
            return light2D;
        }
    }

    private void Update()
    {
        if (scaleMode)
        {
            ScaleIntensity();
        }
    }

    public void UpdateIntensity()
    {
        if (scaleMode)
        {
            ScaleIntensity();
        }
        else if (Light)
        {
            light2D.intensity = goalIntensity;
        }
    }

    private void ScaleIntensity()
    {
        if (Light && Camera.main)
        {
            var d = Vector3.SqrMagnitude(transform.position - Camera.main.transform.position);
            if (d != 0)
            {
                light2D.intensity = goalIntensity / d;
            }
        }
    }
}