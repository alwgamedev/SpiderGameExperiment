using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

public class Slug : MonoBehaviour
{
    const int prepareShootPose = 2;
    const int shootPose = 3;

    [SerializeField] SlugPose[] pose;//dormant, idle, prepareShoot, shoot
    [SerializeField] int[] animationSequence;//0, 1, 2, 3, 1, 0 (poses)
    [SerializeField] float[] animationSpeed;//of length animation.Length - 1
    [SerializeField] SlugRenderer renderer;
    [SerializeField] SlugeEye eye;
    [SerializeField] float aimSpeed;
    [SerializeField] VFXShooter shooter;
    [SerializeField] float shootTime;
    [SerializeField] float shootPosition;
    [SerializeField] float shootSpeed;
    [SerializeField] float gravityScale;
    [SerializeField] float damage;

    AnimationTimer<SlugPose, SlugAnimationUtility> animationTimer;
    int animFrame;
    int NextFrame => Mathf.Min(animFrame + 1, animationSequence.Length - 1);
    bool shoot;
    //animFrame = i means we're moving tweening from animation[i] to animation[i + 1], with speed animationSpeed[i]
    //or set animFrame = animationSpeed.Length for dormant

    PhysicsRotate aim;
    SlugPose lastWorldPose;

    float _orientation;
    float scheduledOrientation;
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
                lastWorldPose.Reflect(transform.position, transform.right);
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
        shooter.Initialize();

        Orientation = transform.localScale.x;
        scheduledOrientation = Orientation;
        aim = PhysicsRotate.identity;
        animFrame = animationSpeed.Length;

        var p = pose[0];
        animationTimer.SnapTo(p);

        p.Transform(transform.localToWorldMatrix);
        p.Aim(aim);
        lastWorldPose = p;
        eye.SnapToPosition(in p, Orientation);

        //first update will take care of setting renderer control points
    }

    void OnDestroy()
    {
        renderer.OnDestroy();
        shooter.ReleaseBuffers();
    }

    void Update()
    {
        Orientation = scheduledOrientation;

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            BeginAnimation(0);
        }

        var dt = Time.deltaTime;        
        UpdateShooter(dt);
        UpdateAnimation(dt);
        UpdateAim(dt, in lastWorldPose);

        var p = animationTimer.AnimatedValue;
        p.Transform(transform.localToWorldMatrix);
        p.Aim(aim);
        lastWorldPose = p;

        eye.UpdateSpring(in p, Orientation, dt);
        renderer.SetBodyPose(in p);
        renderer.SetEyePose(eye.P0, eye.V0, eye.P1, eye.V1);
    }

    void UpdateAnimation(float dt)
    {
        if (!animationTimer.Update(dt) && animFrame < animationSpeed.Length - 1)
        {
            BeginAnimation(++animFrame);
            if (animationSequence[NextFrame] == shootPose)
            {
                shoot = true;
            }
            
            animationTimer.Update(Time.deltaTime);
        }
    }

    void UpdateShooter(float dt)
    {
        shooter.Update(dt);
        HandleProjectileHits();

        if (shoot && animationTimer.timer > shootTime)
        {
            Shoot(in lastWorldPose);
            shoot = false;
        }
    }

    void Shoot(in SlugPose worldPose)
    {
        var baseRot = new PhysicsRotate() { direction = transform.right };
        var worldAim = aim.MultiplyRotation(baseRot);
        var shootDir = Orientation * worldAim.direction;
        var velocity = shootSpeed * shootDir;
        
        var position = worldPose.p2 + shootPosition * shootDir;

        var acceleration = gravityScale * PhysicsWorld.defaultWorld.gravity;

        var projectile = new Projectile()
        {
            position = position,
            velocity = velocity,
            acceleration = acceleration,
            damage = damage
        };

        shooter.Shoot(projectile);
    }

    void HandleProjectileHits()
    {
        var coll = shooter.collision;
        for (int i = 0; i < coll.Length; i++)
        {
            var c = coll[i];
            var shape = c.hitShape;
            if (!shape.isValid)
            {
                continue;
            }

            shape.TryGetShapeData(out var sd);
            var target = ProjectileTargetRegistry.Target(sd.projectileTarget);
            target?.HandleProjectileHit(c);
            coll[i] = default;
        }
    }

    void BeginAnimation(int i)
    {
        animFrame = i;
        animationTimer.BeginAnimation(pose[i], pose[animationSequence[i + 1]], animationSpeed[i]);
    }

    void UpdateAim(float dt, in SlugPose worldPose)
    {
        var a = aim.direction;
        var g = GoalAim(in worldPose);
        if (g.direction.x < 0)
        {
            scheduledOrientation = -Orientation;
        }

        a = MathTools.CheapRotationBySpeedClamped(a, g, aimSpeed, dt, out _);
        aim.direction = a;
    }

    //note that aim direction is relative to transform 
    PhysicsRotate GoalAim(in SlugPose worldPose)
    {
        if (ActiveAim())
        {
            Vector2 p = worldPose.p2;
            var q = Spider.Player.mover.SpideyBody.VirtualPosition;
            if (Vector2.SqrMagnitude(p - q) < 2.25)//prevent direction change flickering
            {
                return aim;
            }
            var u = (q - p).normalized;
            var worldRot = new PhysicsRotate() { direction = Orientation * u };
            var baseRot = new PhysicsRotate() { direction = transform.right };
            return baseRot.InverseMultiplyRotation(worldRot);
        }
        else
        {
            return PhysicsRotate.identity;
        }
    }

    bool ActiveAim()
    {
        return animationSequence[NextFrame] == prepareShootPose || animationSequence[NextFrame] == shootPose;
    }
}