using System;
using UnityEngine;

[Serializable]
struct SynchronizedLeg
{
    [SerializeField] LegAnimator leg;
    [SerializeField] float timeOffset;

    public LegAnimator Leg => leg;
    public float TimeOffset => timeOffset;
}