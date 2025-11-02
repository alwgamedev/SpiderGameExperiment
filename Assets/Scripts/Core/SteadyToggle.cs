using System;
using UnityEngine;

[Serializable]
public struct SteadyToggle
{
    [SerializeField] float onThreshold;
    [SerializeField] float offThreshold;
    [SerializeField] float maxBrightnessThreshold;

    bool on;
    float brightness;

    public bool On => on;
    public float Brightness => brightness;
    public bool AtMax => brightness == 1f;

    public void UpdateState(float parameter)
    {
        on = parameter > (on ? offThreshold : onThreshold);
        brightness = on ? parameter < maxBrightnessThreshold ? (parameter - offThreshold) / (maxBrightnessThreshold - offThreshold) : 1 : 0;
    }
}