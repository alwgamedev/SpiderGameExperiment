using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable] 
public struct HealthPodColors
{
    public Color cellFilledColor;
    public Color cellEmptyColor;
    public Color podFilledColor;//colors for center dot
    public Color podHurtColor;
    public Color podEmptyColor;
}

[Serializable]
public struct HealthPodUI
{
    //have images for cell1-3 and center
    //have enable/disable that i think i'd like to just activate deactivate the gameobject (doesn't matter, it'll very rarely happen)
    public GameObject container;
    public Image cell1;
    public Image cell2;
    public Image cell3;
    public Image center;

    public readonly bool Enabled => container.activeSelf;

    public readonly void Enable()
    {
        container.SetActive(true);
    }

    public readonly void Disable()
    {
        container.SetActive(false);
    }

    //we could easily change this to take a float podHealth and lerp between filled and empty colors
    //(to animate the health bar)
    public readonly void UpdatePod(float podHealth, HealthPodColors colors)
    {
        float t3 = Mathf.Clamp(podHealth, 0, 1);
        float t2 = Mathf.Clamp(podHealth - 1, 0, 1);
        float t1 = Mathf.Clamp(podHealth - 2, 0, 1);
        cell3.color = Color.Lerp(colors.cellEmptyColor, colors.cellFilledColor, t3);//podHealth > 0 ? colors.cellFilledColor : colors.cellEmptyColor;
        cell2.color = Color.Lerp(colors.cellEmptyColor, colors.cellFilledColor, t2);
        cell1.color = Color.Lerp(colors.cellEmptyColor, colors.cellFilledColor, t1);

        center.color = t1 > 0 ? Color.Lerp(colors.podHurtColor, colors.podFilledColor, t1)
            : Color.Lerp(colors.podEmptyColor, colors.podHurtColor, t3);
    }
}