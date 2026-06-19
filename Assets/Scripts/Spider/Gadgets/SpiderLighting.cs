using UnityEngine;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;

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

    Material eyeMaterial;
    Material abdomenMaterial;
    Material headMaterial;
    readonly int eyeColorProperty = Shader.PropertyToID("_Color");
    readonly int eyeIntensityProperty = Shader.PropertyToID("_Intensity");
    readonly int bodyColorProperty = Shader.PropertyToID("_PatternColor");

    AnimationTimer eyeColorAnimation;//will also control light color
    AnimationTimer eyeIntensityAnimation;
    AnimationTimer lightIntensityAnimation;
    AnimationTimer abdomenColorAnimation;
    AnimationTimer headColorAnimation;

    public void Initialize()
    {
        eyeMaterial = new(eyeSR.sharedMaterial);
        eyeSR.sharedMaterial = eyeMaterial;
        abdomenMaterial = new(abdomenSR.sharedMaterial);
        abdomenSR.sharedMaterial = abdomenMaterial;
        headMaterial = new(headSR.sharedMaterial);
        headSR.sharedMaterial = headMaterial;
        
        var eyeColor = eyeRestColor.linear;
        eyeColorAnimation.SnapTo(eyeColor, eyeMaterial, eyeColorProperty, false);
        light.color = eyeColor;

        eyeIntensityAnimation.SnapTo(new Vector4(eyeRestIntensity, 0), eyeMaterial, eyeIntensityProperty, true);

        lightIntensityAnimation.SnapTo(Vector4.zero);
        light.intensity = 0;
        light.enabled = false;

        var bodyOffColor = this.bodyOffColor.linear;
        var bodyRestColor = this.bodyRestColor.linear;
        abdomenColorAnimation.SnapTo(bodyOffColor, abdomenMaterial, bodyColorProperty, false);
        headColorAnimation.SnapTo(bodyOffColor, headMaterial, bodyColorProperty, false);
        abdomenColorAnimation.BeginAnimation(bodyOffColor, bodyRestColor, bodyColorAnimationSpeed);
        headColorAnimation.BeginAnimation(bodyOffColor, bodyRestColor, bodyColorAnimationSpeed);
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
        UnityEngine.Object.Destroy(eyeMaterial);
        UnityEngine.Object.Destroy(abdomenMaterial);
        UnityEngine.Object.Destroy(headMaterial);
    }

    public void HurtFlash()
    {
        var eyeHurtColor = this.eyeHurtColor.linear;
        var eyeRestColor = this.eyeRestColor.linear;
        eyeColorAnimation.SnapTo(eyeHurtColor, eyeMaterial, eyeColorProperty, false);
        eyeColorAnimation.BeginAnimation(eyeHurtColor, eyeRestColor, eyeColorAnimationSpeed);

        var bodyHurtColor = this.bodyHurtColor.linear;
        var bodyRestColor = this.bodyRestColor.linear;
        abdomenColorAnimation.SnapTo(bodyHurtColor.linear, abdomenMaterial, bodyColorProperty, false);
        abdomenColorAnimation.BeginAnimation(bodyHurtColor, bodyRestColor, bodyColorAnimationSpeed);
        headColorAnimation.SnapTo(bodyHurtColor.linear, headMaterial, bodyColorProperty, false);
        headColorAnimation.BeginAnimation(bodyHurtColor, bodyRestColor, bodyColorAnimationSpeed);
    }

    public void TurnLightOn()
    {
        lightIntensityAnimation.BeginAnimation(new Vector4(light.intensity, 0), new Vector4(lightMaxIntensity, 0), 
            lightIntensityAnimationSpeed);
        var curEyeIntensity = eyeMaterial.GetFloat(eyeIntensityProperty);
        eyeIntensityAnimation.BeginAnimation(new Vector4(curEyeIntensity, 0), new Vector4(eyeMaxIntensity, 0), 
            eyeIntensityAnimationSpeed);
        light.enabled = true;
    }

    public void TurnLightOff()
    {
        eyeIntensityAnimation.SnapTo(new Vector4(eyeRestIntensity, 0), eyeMaterial, eyeIntensityProperty, true);
        lightIntensityAnimation.SnapTo(Vector4.zero);
        light.intensity = 0;
        light.enabled = false;
    }

    private void Animate(float dt)
    {
        eyeColorAnimation.Update(dt, eyeMaterial, eyeColorProperty, false);
        light.color = eyeColorAnimation.AnimatedVal;

        eyeIntensityAnimation.Update(dt, eyeMaterial, eyeIntensityProperty, true);

        lightIntensityAnimation.Update(dt);
        light.intensity = lightIntensityAnimation.AnimatedVal.x;

        abdomenColorAnimation.Update(dt, abdomenMaterial, bodyColorProperty, false);
        headColorAnimation.Update(dt, headMaterial, bodyColorProperty, false);
    }
}