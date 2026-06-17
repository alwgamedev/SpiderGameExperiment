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

    public bool Enabled => container.activeSelf;

    public void Enable()
    {
        container.SetActive(true);
    }

    public void Disable()
    {
        container.SetActive(false);
    }

    //we could easily change this to take a float podHealth and lerp between filled and empty colors
    //(to animate the health bar)
    public void UpdatePod(int podHealth, HealthPodColors colors)
    {
        cell3.color = podHealth > 0 ? colors.cellFilledColor : colors.cellEmptyColor;
        cell2.color = podHealth > 1 ? colors.cellFilledColor : colors.cellEmptyColor;
        cell1.color = podHealth > 2 ? colors.cellFilledColor : colors.cellEmptyColor;
        center.color = podHealth switch
        {
            0 => colors.podEmptyColor,
            3 => colors.podFilledColor,
            _ => colors.podHurtColor
        };
    }
}