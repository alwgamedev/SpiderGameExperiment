using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

public class Slug : MonoBehaviour
{
    const int shootFrame = 3;

    [SerializeField] SlugRenderer renderer;
    [SerializeField] SlugeEye eye;
    [SerializeField] PhysicsRotate testAim;
    [SerializeField] SlugPose[] pose;//dormant, idle, prepareShoot, shoot
    [SerializeField] int[] animationSequence;//0, 1, 2, 3, 1, 0 (poses)
    [SerializeField] float[] animationSpeed;//of length animation.Length - 1
    // [SerializeField] float testAnim;

    AnimationTimer<SlugPose, SlugAnimationUtility> animationTimer;
    int animFrame;
    //animFrame = i means we're moving tweening from animation[i] to animation[i + 1], with speed animationSpeed[i]
    //or set animFrame = animationSpeed.Length for dormant

    float _orientation;
    float Orientation
    {
        get => _orientation;
        set
        {
            if (value != _orientation)
            {
                _orientation = value;
                var s = transform.localScale;
                s.x = value;
                transform.localScale = s;
                renderer.SetOrientation(value);
                eye.OnChangeOrientation(transform.right);
            }
        }
    }

    void OnValidate()
    {
        renderer.OnValidate();
    }

    void Start()
    {
        renderer.Initialize();

        Orientation = transform.localScale.x;
        testAim = PhysicsRotate.identity;
        animFrame = animationSpeed.Length;

        var p = pose[0];
        animationTimer.SnapTo(p);

        p.Transform(transform.localToWorldMatrix);
        p.Aim(testAim);
        eye.SnapToPosition(in p, Orientation);

        //first update will take care of setting renderer control points
    }

    void OnDestroy()
    {
        renderer.OnDestroy();
    }

    void Update()
    {
        // testAnim = Mathf.Clamp(testAnim, 0, animationSequence.Length - 1.001f);
        // var i = (int)testAnim;
        // var t = testAnim - i;
        // var p = SlugPose.Lerp(pose[animationSequence[i]], pose[animationSequence[i + 1]], t);
        // SetPose(p);

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            BeginAnimation(0);
        }

        var dt = Time.deltaTime;
        UpdateAnimation(dt);

        var p = animationTimer.AnimatedValue;
        p.Transform(transform.localToWorldMatrix);
        p.Aim(testAim);

        eye.UpdateSpring(in p, Orientation, dt);
        renderer.SetBodyPose(in p);
        renderer.SetEyePose(eye.P0, eye.V0, eye.P1, eye.V1);
    }

    void UpdateAnimation(float dt)
    {
        if (!animationTimer.Update(dt) && animFrame < animationSpeed.Length - 1)
        {
            BeginAnimation(++animFrame);
            if (animFrame == shootFrame)
            {
                Debug.Log("Shoot!");
            }

            animationTimer.Update(Time.deltaTime);
        }
    }

    void BeginAnimation(int i)
    {
        animFrame = i;
        animationTimer.BeginAnimation(animationTimer.AnimatedValue, pose[animationSequence[i + 1]], animationSpeed[i]);
    }
}