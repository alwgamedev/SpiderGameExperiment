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
    [SerializeField] float eyeColorAnimationSpeed;
    [SerializeField] float eyeIntensityAnimationSpeed;
    [SerializeField] float lightIntensityAnimationSpeed;

    Material material;
    readonly int eyeColorProperty = Shader.PropertyToID("_Color");
    readonly int eyeIntensityProperty = Shader.PropertyToID("_Intensity");

    Color startEyeColor;
    Color goalEyeColor;
    // Color nextEyeColor;
    float eyeColorTimer;

    float startEyeIntensity;
    float goalEyeIntensity;
    // float nextEyeIntensity;
    float eyeIntensityTimer;
    
    float startLightIntensity;
    float goalLightIntensity;
    // float nextLightIntensity;
    float lightIntensityTimer;
    

    public void Initialize()
    {
        material = new(sr.sharedMaterial);
        sr.sharedMaterial = material;

        SnapEyeColor(eyeRestColor);
        SnapEyeIntensity(eyeOffIntensity);
        SnapLightIntensity(0);
        light.enabled = false;

        SetGoalEyeColor(eyeRestColor);
        SetGoalEyeIntensity(eyeRestIntensity);
        // TurnLightOn();
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
        startEyeColor = color;
        goalEyeColor = color;
        // nextEyeColor = color;
        eyeColorTimer = 1;
    }

    public void SetGoalEyeColor(Color color)
    {
        var curColor = material.GetColor(eyeColorProperty);
        goalEyeColor = color;
        // nextEyeColor = color;
        BeginColorAnimation(curColor, color, ref startEyeColor, ref eyeColorTimer, eyeColorAnimationSpeed);
    }

    public void FlashEyeColor(Color color, Color nextColor)
    {
        SnapEyeColor(color);
        // var curColor = material.GetColor(eyeColorProperty);
        goalEyeColor = nextColor;
        // nextEyeColor = nextColor;
        BeginColorAnimation(color, nextColor, ref startEyeColor, ref eyeColorTimer, eyeColorAnimationSpeed);
    }

    public void SnapEyeIntensity(float intensity)
    {
        material.SetFloat(eyeIntensityProperty, intensity);
        startEyeIntensity = intensity;
        goalEyeIntensity = intensity;
        // nextEyeIntensity = intensity;
        eyeIntensityTimer = 1;
    }

    public void SetGoalEyeIntensity(float intensity)
    {
        var curIntensity = material.GetFloat(eyeIntensityProperty);
        goalEyeIntensity = intensity;
        // nextEyeIntensity = intensity;
        BeginFloatAnimation(curIntensity, intensity, ref startEyeIntensity, ref eyeIntensityTimer, eyeIntensityAnimationSpeed);
    }

    // public void FlashEyeIntensity(float intensity, float nextIntensity)
    // {
    //     var curIntensity = material.GetFloat(eyeIntensityProperty);
    //     goalEyeIntensity = intensity;
    //     nextEyeIntensity = nextIntensity;
    //     BeginFloatAnimation(curIntensity, intensity, ref startEyeIntensity, ref eyeIntensityTimer, eyeIntensityAnimationSpeed);
    // }


    public void SnapLightIntensity(float intensity)
    {
        light.intensity = intensity;
        startLightIntensity = intensity;
        goalLightIntensity = intensity;
        // nextLightIntensity = intensity;
        lightIntensityTimer = 1;
    }

    public void SetGoalLightIntensity(float intensity)
    {
        goalLightIntensity = intensity;
        // nextLightIntensity = intensity;
        BeginFloatAnimation(light.intensity, intensity, ref startLightIntensity, ref lightIntensityTimer, lightIntensityAnimationSpeed);
    }

    // public void FlashLightIntensity(float intensity, float nextIntensity)
    // {
    //     goalLightIntensity = intensity;
    //     nextLightIntensity = nextIntensity;
    //     BeginFloatAnimation(light.intensity, intensity, ref startLightIntensity, ref lightIntensityTimer, lightIntensityAnimationSpeed);
    // }

    private static void BeginColorAnimation(Color curColor, Color goalColor, 
        ref Color startColor, ref float timer, float animationSpeed)
    {
        var diff = MaxDifference(curColor, goalColor);
        var t = Mathf.Clamp(diff / animationSpeed, 0, 1);
        timer = 1 - t;
        startColor = curColor;
    }

    private static void BeginFloatAnimation(float curVal, float goalVal,
        ref float startVal, ref float timer, float animationSpeed)
    {
        var diff = Mathf.Abs(goalVal - curVal);
        var t = Mathf.Clamp(diff / animationSpeed, 0, 1);
        timer = 1 - t;
        startVal = curVal;
    }

    private void Animate(float dt)
    {
        //2do: use timers instead (lerp from cached initial val to goal val)
        //just more reliable
        if (eyeColorTimer < 1)
        {
            eyeColorTimer += eyeColorAnimationSpeed * dt;
            var eyeColor = Color.Lerp(startEyeColor, goalEyeColor, eyeColorTimer);
            material.SetColor(eyeColorProperty, eyeColor);
            light.color = eyeColor;

            // if (eyeColorTimer >= 1 && nextEyeColor != goalEyeColor)
            // {
            //     startEyeColor = goalEyeColor;
            //     goalEyeColor = nextEyeColor;
            //     BeginColorAnimation(startEyeColor, goalEyeColor, ref startEyeColor, ref eyeColorTimer, eyeColorAnimationSpeed);
            // }
        }

        if (eyeIntensityTimer < 1)
        {
            eyeIntensityTimer += eyeIntensityAnimationSpeed * dt;
            var eyeIntensity = Mathf.Lerp(startEyeIntensity, goalEyeIntensity, eyeIntensityTimer);
            material.SetFloat(eyeIntensityProperty, eyeIntensity);
            
            // if (eyeIntensityTimer >= 1 && nextEyeIntensity != goalEyeIntensity)
            // {
            //     startEyeIntensity = goalEyeIntensity;
            //     goalEyeIntensity = nextEyeIntensity;
            //     BeginFloatAnimation(startEyeIntensity, goalEyeIntensity, ref startEyeIntensity, 
            //         ref eyeIntensityTimer, eyeIntensityAnimationSpeed);
            // }
        }

        if (lightIntensityTimer < 1)
        {
            lightIntensityTimer += lightIntensityAnimationSpeed * dt;
            light.intensity = Mathf.Lerp(startLightIntensity, goalLightIntensity, lightIntensityTimer);

            // if (lightIntensityTimer >= 1 && nextLightIntensity != goalLightIntensity)
            // {
            //     startLightIntensity = goalLightIntensity;
            //     goalLightIntensity = nextLightIntensity;
            //     BeginFloatAnimation(startLightIntensity, goalLightIntensity, ref startLightIntensity, 
            //         ref lightIntensityTimer, lightIntensityAnimationSpeed);
            // }
        }
    }

    private static float MaxDifference(Color c1, Color c2)
    {
        var r = Mathf.Abs(c1.r - c2.r);
        var g = Mathf.Abs(c1.g - c2.g);
        var b = Mathf.Abs(c1.b - c2.b);
        var a = Mathf.Abs(c1.a - c2.a);
        return Mathf.Max(Mathf.Max(r, g), Mathf.Max(b, a));
    }

    // public bool WithinTolerance(float x, float y)
    // {
    //     return Mathf.Abs(y - x) < 0.1f;
    // }

    // public bool WithinTolerance(Color c1, Color c2)
    // {
    //     return WithinTolerance(c1.r, c2.r) && WithinTolerance(c1.g, c2.g)
    //         && WithinTolerance(c1.b, c2.b) && WithinTolerance(c1.a, c2.a);
    // }
}