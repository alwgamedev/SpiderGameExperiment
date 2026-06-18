using UnityEngine;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;

[Serializable]
public class SpiderEye
{
    [SerializeField] SpriteRenderer sr;
    [SerializeField] Light2D light;
    [ColorUsage(true, true)][SerializeField] Color eyeRestColor;
    [ColorUsage(true, true)][SerializeField] Color eyeHurtColor;
    [SerializeField] float eyeOffIntensity;
    [SerializeField] float eyeRestIntensity;
    [SerializeField] float eyeMaxIntensity;
    [SerializeField] float lightMaxIntensity;
    [SerializeField] float colorAnimationSpeed;
    [SerializeField] float intensityAnimationSpeed;
    [SerializeField] float lightIntensityAnimationSpeed;

    Material material;
    readonly int eyeColorProperty = Shader.PropertyToID("_Color");
    readonly int eyeIntensityProperty = Shader.PropertyToID("_Intensity");

    Color curEyeColor;
    Color goalEyeColor;
    Color nextEyeColor;

    float curEyeIntensity;
    float goalEyeIntensity;
    float nextEyeIntensity;
    
    float curLightIntensity;
    float goalLightIntensity;
    float nextLightIntensity;
    

    public void Initialize()
    {
        material = new(sr.sharedMaterial);
        sr.sharedMaterial = material;

        SnapEyeColor(eyeRestColor);
        SnapEyeIntensity(eyeRestIntensity);
        SnapLightIntensity(0);
        light.enabled = false;

        SetGoalEyeColor(eyeRestColor);
        TurnLightOn();
    }

    public void Update()
    {
        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            TurnLightOn();
        }
        else if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            TurnLightOff();
        }

        Animate(Time.deltaTime);
    }

    public void OnDestroy()
    {
        if (material)
        {
            UnityEngine.Object.Destroy(material);
        }
    }

    public void HurtFlash()
    {
        FlashEyeColor(eyeHurtColor, eyeRestColor);
    }

    public void TurnLightOn()
    {
        SetGoalLightIntensity(lightMaxIntensity);
        SetGoalEyeIntensity(eyeMaxIntensity);
        light.enabled = true;
    }

    public void TurnLightOff()
    {
        SnapLightIntensity(0);
        SnapEyeIntensity(eyeRestIntensity);
        light.enabled = false;
    }

    //snap sets property immediately and stops animation
    public void SnapEyeColor(Color color)
    {
        material.SetColor(eyeColorProperty, color);
        light.color = color;
        curEyeColor = color;
        goalEyeColor = color;
        nextEyeColor = color;
    }

    public void SetGoalEyeColor(Color color)
    {
        goalEyeColor = color;
        nextEyeColor = color;
    }

    public void FlashEyeColor(Color color, Color nextColor)
    {
        goalEyeColor = color;
        nextEyeColor = nextColor;
    }

    public void SnapEyeIntensity(float intensity)
    {
        material.SetFloat(eyeIntensityProperty, intensity);
        curEyeIntensity = intensity;
        goalEyeIntensity = intensity;
        nextEyeIntensity = intensity;
    }

    public void SetGoalEyeIntensity(float intensity)
    {
        goalEyeIntensity = intensity;
        nextEyeIntensity = intensity;
    }

    public void FlashEyeIntensity(float intensity, float nextIntensity)
    {
        goalEyeIntensity = intensity;
        nextEyeIntensity = nextIntensity;
    }


    public void SnapLightIntensity(float intensity)
    {
        light.intensity = intensity;
        curLightIntensity = intensity;
        goalLightIntensity = intensity;
        nextLightIntensity = intensity;
    }

    public void SetGoalLightIntensity(float intensity)
    {
        goalLightIntensity = intensity;
        nextLightIntensity = intensity;
    }

    public void FlashLightIntensity(float intensity, float nextIntensity)
    {
        goalLightIntensity = intensity;
        nextLightIntensity = nextIntensity;
    }

    private void Animate(float dt)
    {
        //2do: use timers instead (lerp from cached initial val to goal val)
        //just more reliable
        if (!WithinTolerance(curEyeColor, goalEyeColor))
        {
            curEyeColor = Color.Lerp(curEyeColor, goalEyeColor, colorAnimationSpeed * dt);

            if (WithinTolerance(curEyeColor, goalEyeColor))
            {
                curEyeColor = goalEyeColor;
                goalEyeColor = nextEyeColor;
            }

            material.SetColor(eyeColorProperty, curEyeColor);
            light.color = curEyeColor;
        }

        if (!WithinTolerance(curEyeIntensity, goalEyeIntensity))
        {
            curEyeIntensity = Mathf.Lerp(curEyeIntensity, goalEyeIntensity, intensityAnimationSpeed * dt);
            
            if (WithinTolerance(curEyeIntensity, goalEyeIntensity))
            {
                curEyeIntensity = goalEyeIntensity;
                goalEyeIntensity = nextEyeIntensity;
            }

            material.SetFloat(eyeIntensityProperty, curEyeIntensity);
        }

        if (!WithinTolerance(curLightIntensity, goalLightIntensity))
        {
            curLightIntensity = Mathf.Lerp(curLightIntensity, goalLightIntensity, lightIntensityAnimationSpeed * dt);

            if (WithinTolerance(curLightIntensity, goalLightIntensity))
            {
                curLightIntensity = goalLightIntensity;
                goalLightIntensity = nextLightIntensity;
            }

            light.intensity = curLightIntensity;
        }
    }

    public bool WithinTolerance(float x, float y)
    {
        return Mathf.Abs(y - x) < 0.1f;
    }

    public bool WithinTolerance(Color c1, Color c2)
    {
        return WithinTolerance(c1.r, c2.r) && WithinTolerance(c1.g, c2.g)
            && WithinTolerance(c1.b, c2.b) && WithinTolerance(c1.a, c2.a);
    }
}