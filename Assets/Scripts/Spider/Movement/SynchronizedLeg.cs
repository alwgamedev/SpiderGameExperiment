using System;
using UnityEngine;

[Serializable]
public class SynchronizedLeg
{
    [SerializeField] LegAnimator leg;
    [SerializeField] float timeOffset;

    public LegAnimator Leg => leg;
    public float TimeOffset => timeOffset;
}