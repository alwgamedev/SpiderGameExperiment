using System;
using UnityEngine;

[Serializable]
public class ThrusterFlame
{
    [SerializeField] Transform hook;
    [SerializeField] float widthMin;
    [SerializeField] float widthMax;
    [SerializeField] float intensityMin;
    [SerializeField] float intensityMax;
    [SerializeField] float intensityLerpRate;
    [SerializeField] float disengageLerpRate;
    [SerializeField] SpriteRenderer sr;

    Material material;

    const string intensityProperty = "_BodyNoiseMax";

    public void Initialize()
    {
        material = new Material(sr.material);
        sr.material = material;
        material.SetFloat(intensityProperty, 0);
    }

    //pass bodySpeed < 0 when thrusters are off (so lerps toward zero)
    public void Update(float bodySpeed, float dt)
    {
        var cur = material.GetFloat(intensityProperty);
        var goal = bodySpeed < 0 ? 0 : intensityMax;
        if (goal > 0)
        {
            cur = Mathf.Max(intensityMin, cur);
        }
        if (cur != goal)
        {
            cur = MathTools.LerpAtConstantSpeed(cur, goal, bodySpeed < 0 ? disengageLerpRate : intensityLerpRate, dt);
            material.SetFloat(intensityProperty, cur);
            var p = hook.position;
            var s = sr.transform.localScale;
            s.x = Mathf.Lerp(widthMin, widthMax, (cur - intensityMin) / intensityMax);
            sr.transform.localScale = s;
            sr.transform.position += p - hook.position;
        }
    }
}