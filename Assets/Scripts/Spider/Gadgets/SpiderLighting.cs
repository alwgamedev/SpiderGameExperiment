using UnityEngine;
using System;
using UnityEngine.Rendering.Universal;

[Serializable]
public class SpiderLighting
{
    [SerializeField] SpriteRenderer eyeSR;
    [SerializeField] SpriteRenderer abdomenSR;
    [SerializeField] SpriteRenderer headSR;
    [SerializeField] Light2D light;
    [ColorUsage(true, true)][SerializeField] Color eyeRestColor;
    [ColorUsage(true, true)][SerializeField] Color eyeHurtColor;
    [ColorUsage(true, true)][SerializeField] Color bodyOffColor;
    [ColorUsage(true, true)][SerializeField] Color bodyRestColor;
    [ColorUsage(true, true)][SerializeField] Color bodyHurtColor;
    [SerializeField] float eyeOffIntensity;
    [SerializeField] float eyeRestIntensity;
    [SerializeField] float eyeMaxIntensity;
    [SerializeField] float lightMaxIntensity;
    [SerializeField] float eyeColorAnimationSpeed;
    [SerializeField] float eyeIntensityAnimationSpeed;
    [SerializeField] float lightIntensityAnimationSpeed;
    [SerializeField] float bodyColorAnimationSpeed;

    SpiderInput spiderInput;

    Material eyeMaterial;
    Material abdomenMaterial;
    Material headMaterial;
    readonly int eyeColorProperty = Shader.PropertyToID("_Color");
    readonly int eyeIntensityProperty = Shader.PropertyToID("_Intensity");
    readonly int bodyColorProperty = Shader.PropertyToID("_PatternColor");

    AnimationTimer<Vector4, Vector4AnimationUtility> eyeColorAnimation;//will also control light color
    AnimationTimer<float, FloatAnimationUtility> eyeIntensityAnimation;
    AnimationTimer<float, FloatAnimationUtility> lightIntensityAnimation;
    AnimationTimer<Vector4, Vector4AnimationUtility> abdomenColorAnimation;
    AnimationTimer<Vector4, Vector4AnimationUtility> headColorAnimation;

    public void Initialize(SpiderInput spiderInput)
    {
        this.spiderInput = spiderInput;

        eyeMaterial = new(eyeSR.sharedMaterial);
        eyeSR.sharedMaterial = eyeMaterial;
        abdomenMaterial = new(abdomenSR.sharedMaterial);
        abdomenSR.sharedMaterial = abdomenMaterial;
        headMaterial = new(headSR.sharedMaterial);
        headSR.sharedMaterial = headMaterial;
        
        var eyeColor = eyeRestColor.linear;
        eyeColorAnimation.SnapTo(eyeColor);
        eyeMaterial.SetVector(eyeColorProperty, eyeColor);
        light.color = eyeColor;
        
        eyeIntensityAnimation.SnapTo(eyeRestIntensity);
        eyeMaterial.SetFloat(eyeIntensityProperty, eyeRestIntensity);

        lightIntensityAnimation.SnapTo(0);
        light.intensity = 0;
        light.enabled = false;

        var bodyOffColor = this.bodyOffColor.linear;
        var bodyRestColor = this.bodyRestColor.linear;
        
        abdomenColorAnimation.SnapTo(bodyOffColor);
        abdomenMaterial.SetVector(bodyColorProperty, bodyOffColor);
        headColorAnimation.SnapTo(bodyOffColor);
        headMaterial.SetVector(bodyColorProperty, bodyOffColor);
        abdomenColorAnimation.BeginAnimation(bodyOffColor, bodyRestColor, bodyColorAnimationSpeed);
        headColorAnimation.BeginAnimation(bodyOffColor, bodyRestColor, bodyColorAnimationSpeed);
    }

    public void Update()
    {
        if (spiderInput.PAction.WasPressedThisFrame())
        {
            TurnLightOn();
        }
        else if (spiderInput.LAction.WasPressedThisFrame())
        {
            TurnLightOff();
        }

        Animate(Time.deltaTime);
    }

    public void OnDestroy()
    {
        UnityEngine.Object.Destroy(eyeMaterial);
        UnityEngine.Object.Destroy(abdomenMaterial);
        UnityEngine.Object.Destroy(headMaterial);
    }

    public void HurtFlash()
    {
        var eyeHurtColor = this.eyeHurtColor.linear;
        var eyeRestColor = this.eyeRestColor.linear;
        
        eyeColorAnimation.SnapTo(eyeHurtColor);
        eyeMaterial.SetVector(eyeColorProperty, eyeHurtColor);
        eyeColorAnimation.BeginAnimation(eyeHurtColor, eyeRestColor, eyeColorAnimationSpeed);

        var bodyHurtColor = this.bodyHurtColor.linear;
        var bodyRestColor = this.bodyRestColor.linear;
    
        abdomenColorAnimation.SnapTo(bodyHurtColor);
        abdomenMaterial.SetVector(bodyColorProperty, bodyHurtColor);
        abdomenColorAnimation.BeginAnimation(bodyHurtColor, bodyRestColor, bodyColorAnimationSpeed);
        
        headColorAnimation.SnapTo(bodyHurtColor);
        headMaterial.SetVector(bodyColorProperty, bodyHurtColor);
        headColorAnimation.BeginAnimation(bodyHurtColor, bodyRestColor, bodyColorAnimationSpeed);
    }

    public void TurnLightOn()
    {
        lightIntensityAnimation.BeginAnimation(light.intensity, lightMaxIntensity, lightIntensityAnimationSpeed);
        var curEyeIntensity = eyeMaterial.GetFloat(eyeIntensityProperty);
        
        eyeIntensityAnimation.BeginAnimation(curEyeIntensity, eyeMaxIntensity, eyeIntensityAnimationSpeed);
        light.enabled = true;
    }

    public void TurnLightOff()
    {
        eyeIntensityAnimation.SnapTo(eyeRestIntensity);
        eyeMaterial.SetFloat(eyeIntensityProperty, eyeRestIntensity);
        
        lightIntensityAnimation.SnapTo(0);
        light.intensity = 0;
        light.enabled = false;
    }

    private void Animate(float dt)
    {
        if (eyeColorAnimation.Update(dt))
        {
            var eyeColor = eyeColorAnimation.AnimatedValue;
            eyeMaterial.SetVector(eyeColorProperty, eyeColor);
            light.color = eyeColor;
        }
        
        if (eyeIntensityAnimation.Update(dt))
        {
            eyeMaterial.SetFloat(eyeIntensityProperty, eyeIntensityAnimation.AnimatedValue);
        }
        
        if (lightIntensityAnimation.Update(dt))
        {
            light.intensity = lightIntensityAnimation.AnimatedValue;
        }

        if (abdomenColorAnimation.Update(dt))
        {
            abdomenMaterial.SetVector(bodyColorProperty, abdomenColorAnimation.AnimatedValue);
        }

        if (headColorAnimation.Update(dt))
        {
            headMaterial.SetVector(bodyColorProperty, headColorAnimation.AnimatedValue);
        }
    }
}