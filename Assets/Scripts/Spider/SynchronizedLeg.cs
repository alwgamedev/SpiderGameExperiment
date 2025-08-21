using System;
using UnityEngine;

[Serializable]
struct SynchronizedLeg
{
    [SerializeField] LegAnimator leg;
    [SerializeField] float timeOffset;
    //[SerializeField] float stepTime;
    //[SerializeField] float restTime;

    public LegAnimator Leg => leg;
    public float TimeOffset => timeOffset;
    //public float StepTime => stepTime;
    //public float RestTime => restTime;
}