using System;
using UnityEngine;

[Serializable]
public struct GrabberMask
{
    [SerializeField] Transform maskTransform;
    [SerializeField] float maskOnThreshold;

    int maskOrigin;
    int maskDirection;
    int maskThreshold;

    Material material;
    bool enabled;

    public void Initialize(SpriteRenderer[] sr)
    {
        maskOrigin = Shader.PropertyToID("_MaskOrigin");
        maskDirection = Shader.PropertyToID("_MaskDirection");
        maskThreshold = Shader.PropertyToID("_MaskThreshold");

        material = new Material(sr[0].sharedMaterial);

        for (int i = 0; i < sr.Length; i++)
        {
            sr[i].sharedMaterial = material;
        }
    }

    public void Enable()
    {
        enabled = true;
        material.SetFloat(maskThreshold, maskOnThreshold);
    }

    public void Disable()
    {
        enabled = false;
        material.SetFloat(maskThreshold, -Mathf.Infinity);
    }

    public void Destroy()
    {
        UnityEngine.Object.Destroy(material);
    }

    public void Update()
    {
        if (enabled)
        {
            material.SetVector(maskOrigin, maskTransform.position);
            material.SetVector(maskDirection, Mathf.Sign(maskTransform.localScale.x) * maskTransform.right);
        }
    }
}