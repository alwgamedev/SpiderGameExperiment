using Unity.U2D.Physics;
using UnityEngine;

public class Slug : MonoBehaviour
{
    const int shootFrame = 3;

    [SerializeField] SlugRenderer renderer;
    [SerializeField] SlugPose testPose;
    [SerializeField] PhysicsRotate testAim;
    [SerializeField] SlugPose[] pose;//dormant, idle, prepareShoot, shoot
    [SerializeField] int[] animationPose;//0, 1, 2, 3, 1, 0 (poses)
    [SerializeField] float[] animationSpeed;//of length animation.Length - 1

    AnimationTimer<SlugPose, SlugAnimationUtility> animationTimer;
    int animFrame;
    //animFrame = i means we're moving tweening from animation[i] to animation[i + 1], with speed animationSpeed[i]
    //or set animFrame = animationSpeed.Length for dormant

    float Orientation
    {
        get => transform.localScale.x;
        set
        {
            var s = transform.localScale;
            s.x = value;
            transform.localScale = s;
            renderer.SetOrientation(value);
        }
    }

    void OnValidate()
    {
        renderer.OnValidate();
    }

    void Start()
    {
        renderer.Initialize();
        Orientation = Orientation;
        testAim = PhysicsRotate.identity;

        // animationTimer.SnapTo(pose[0]);
        // animFrame = animationSpeed.Length;
    }

    void OnDestroy()
    {
        renderer.OnDestroy();
    }

    void Update()
    {
        SetPose(testPose);

        //we would also need to update the pose if transform changes,
        //but plan is to keep the transform planted once we spawn
        // if (animationTimer.Update(Time.deltaTime))
        // {
        //     SetPose(animationTimer.AnimatedValue);
        // }
        // else if (animFrame < animationSpeed.Length)
        // {
        //     BeginAnimation(++animFrame);
        //     if (animFrame == shootFrame)
        //     {
        //         Debug.Log("Shoot!");
        //     }

        //     animationTimer.Update(Time.deltaTime);
        //     SetPose(animationTimer.AnimatedValue);
        // }
    }

    void SetPose(SlugPose pose)
    {
        renderer.SetPose(pose, transform.localToWorldMatrix, testAim);
    }

    void BeginAnimation(int i)
    {
        animFrame = i;
        animationTimer.BeginAnimation(animationTimer.AnimatedValue, pose[animationPose[i + 1]], animationSpeed[i]);
    }
}