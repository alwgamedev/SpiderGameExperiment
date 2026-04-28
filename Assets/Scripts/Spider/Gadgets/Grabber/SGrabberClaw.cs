using System;
using Unity.U2D.Physics;
using UnityEngine;

[Serializable]
public struct GrabberClawDefinition
{
    public PhysicsHingeJointDefinition jointDef;
    public PhysicsShapeDefinition shapeDef;
    public PhysicsBodyDefinition bodyDef;
    public Vector2[] upperWidth;
    public Vector2[] lowerWidth;
}

[Serializable]
public struct SGrabberClaw
{
    PhysicsBody upperArm;
    PhysicsHingeJoint upperArmJoint;
    PhysicsBody lowerArm;
    PhysicsHingeJoint lowerArmJoint;
    PhysicsBody grabTarget;

    PhysicsRotate upperTarget;
    PhysicsRotate lowerTarget;

    [SerializeField] float upperArmOpen;
    [SerializeField] float upperArmClosed;
    [SerializeField] float lowerArmOpen;
    [SerializeField] float lowerArmClosed;

    [SerializeField] float rotationTolerance;
    [SerializeField] float grabTolerance;
    [SerializeField] float dropTolerance;

    Mode mode;
    bool targetReached;

    public readonly bool Enabled => upperArm.enabled && lowerArm.enabled;

    enum Mode
    {
        off, standard, grabbingTarget, holdingTarget
    }

    public static void DrawBodyGizmos(Transform[] bone, Vector2[] width)
    {
        if (bone != null && width != null && bone.Length == width.Length + 1)
        {
            Gizmos.color = Color.orange;
            for (int i = 0; i < width.Length; i++)
            {
                Vector2 p0 = bone[i].position;
                Vector2 p1 = bone[i + 1].position;
                var n = (p1 - p0).normalized.CCWPerp();

                var startN = 0.5f * width[i].x * n;
                var endN = 0.5f * width[i].y * n;

                Gizmos.DrawLine(p0 - startN, p0 + startN);
                Gizmos.DrawLine(p0 + startN, p1 + endN);
                Gizmos.DrawLine(p1 + endN, p1 - endN);
                Gizmos.DrawLine(p1 - endN, p0 - startN);
            }
        }
    }

    //geometry should be local to physics transforms
    public void Initialize(PhysicsBody anchorBody, GrabberClawDefinition def, 
        Transform upperArmPhysTransform, Transform[] upperArmBone,
        Transform lowerArmPhysTransform, Transform[] lowerArmBone)
    {
        Span<PolygonGeometry> upperArmGeometry = stackalloc PolygonGeometry[upperArmBone.Length - 1];
        Span<PolygonGeometry> lowerArmGeometry = stackalloc PolygonGeometry[lowerArmBone.Length - 1];

        CreateArmGeometry(upperArmGeometry, upperArmPhysTransform, upperArmBone, def.upperWidth);
        CreateArmGeometry(lowerArmGeometry, lowerArmPhysTransform, lowerArmBone, def.lowerWidth);

        upperArm = CreateArmBody(def.bodyDef, def.shapeDef, anchorBody, upperArmPhysTransform, upperArmGeometry);
        lowerArm = CreateArmBody(def.bodyDef, def.shapeDef, anchorBody, lowerArmPhysTransform, lowerArmGeometry);

        upperArmJoint = CreateArmJoint(def.jointDef, anchorBody, upperArm, upperArmBone[0].position);
        lowerArmJoint = CreateArmJoint(def.jointDef, anchorBody, lowerArm, lowerArmBone[0].position);

        static void CreateArmGeometry(Span<PolygonGeometry> geometry, Transform physTransform, Transform[] bone, Vector2[] width)
        {
            for (int i = 0; i < geometry.Length; i++)
            {
                Vector2 p0 = bone[i].position;
                Vector2 p1 = bone[i + 1].position;
                var n = (p1 - p0).normalized.CCWPerp();

                var startN = 0.5f * width[i].x * n;
                var endN = 0.5f * width[i].y * n;
                Span<Vector2> vertices = stackalloc Vector2[4];
                vertices[0] = p0 - startN;
                vertices[1] = p0 + startN;
                vertices[2] = p1 + endN;
                vertices[3] = p1 - endN;

                var polygon = PolygonGeometry.Create(vertices);//world space
                geometry[i] = polygon.InverseTransform(physTransform.localToWorldMatrix, false);
            }
        }

        static PhysicsBody CreateArmBody(PhysicsBodyDefinition bodyDef, PhysicsShapeDefinition shapeDef, PhysicsBody anchorBody, 
            Transform physTransform, Span<PolygonGeometry> geometry)
        {
            bodyDef.position = physTransform.position;
            bodyDef.rotation = new PhysicsRotate(physTransform.rotation, PhysicsWorld.TransformPlane.XY);
            var body = PhysicsCoreHelper.CreatePolygonBody(anchorBody.world, bodyDef, shapeDef, physTransform.localToWorldMatrix, geometry);
            body.transformObject = physTransform;
            return body;
        }

        static PhysicsHingeJoint CreateArmJoint(PhysicsHingeJointDefinition jointDef, PhysicsBody anchorBody, PhysicsBody armBody, Vector2 anchorWorldPosition)
        {
            jointDef.bodyA = anchorBody;
            jointDef.bodyB = armBody;
            var posA = anchorBody.transform.InverseTransformPoint(anchorWorldPosition);
            var posB = armBody.transform.InverseTransformPoint(anchorWorldPosition);
            jointDef.localAnchorA = new PhysicsTransform(posA, PhysicsRotate.identity);
            jointDef.localAnchorB = new PhysicsTransform(posB, PhysicsRotate.identity);
            return PhysicsHingeJoint.Create(anchorBody.world, jointDef);
        }
    }

    public void Enable()
    {
        if (upperArm.isValid)
        {
            upperArm.enabled = true;
        }
        if (lowerArm.isValid)
        {
            lowerArm.enabled = true;
        }
    }

    public void Disable(bool forgetState)
    {
        if (forgetState)
        {
            mode = Mode.off;
            grabTarget = default;
            targetReached = false;
        }
        if (upperArm.isValid)
        {
            upperArm.enabled = false;
            if (forgetState)
            {
                upperArm.linearVelocity = Vector2.zero;
                upperArm.angularVelocity = 0;
            }
        }
        if (lowerArm.isValid)
        {
            lowerArm.enabled = false;
            if (forgetState)
            {
                lowerArm.linearVelocity = Vector2.zero;
                lowerArm.angularVelocity = 0;
            }
        }
    }

    public readonly void Destroy()
    {
        if (upperArm.isValid)
        {
            upperArm.Destroy();
        }
        if (lowerArm.isValid)
        {
            lowerArm.Destroy();
        }
    }

    public readonly void SyncTransforms()
    {
        upperArm.SyncTransform();
        lowerArm.SyncTransform();
    }

    public void SetSpringTarget(float upperTarget, float lowerTarget)
    {
        upperArmJoint.springTargetAngle = upperTarget;
        lowerArmJoint.springTargetAngle = lowerTarget;
        this.upperTarget = PhysicsRotate.FromDegrees(upperTarget);
        this.lowerTarget = PhysicsRotate.FromDegrees(lowerTarget);
    }

    /// <summary> upperTarget, lowerTarget are relative to joint bodyA rotation. This does not set spring targets. </summary>
    public readonly void SnapToPose(float upperTarget, float lowerTarget)
    {
        var upperPos = upperArmJoint.bodyA.transform.TransformPoint(upperArmJoint.localAnchorA.position);
        var upperRot = upperArmJoint.bodyA.rotation.MultiplyRotation(upperArmJoint.localAnchorA.rotation).MultiplyRotation(PhysicsRotate.FromDegrees(upperTarget));
        upperArm.transform = new PhysicsTransform(upperPos, upperRot);

        var lowerPos = lowerArmJoint.bodyA.transform.TransformPoint(lowerArmJoint.localAnchorA.position);
        var lowerRot = lowerArmJoint.bodyA.rotation.MultiplyRotation(lowerArmJoint.localAnchorA.rotation).MultiplyRotation(PhysicsRotate.FromDegrees(lowerTarget));
        lowerArm.transform = new PhysicsTransform(lowerPos, lowerRot);
    }

    public void Open()
    {
        SetSpringTarget(upperArmOpen, lowerArmOpen);
        mode = Mode.standard;
        targetReached = false;
    }

    public readonly void SnapOpen()
    {
        SnapToPose(upperArmOpen, lowerArmOpen);
    }

    public void Close()
    {
        SetSpringTarget(upperArmClosed, lowerArmClosed);
        mode = Mode.standard;
        targetReached = false;
    }

    public readonly void SnapClosed()
    {
        SnapToPose(upperArmClosed, lowerArmClosed);
    }

    public void BeginGrab(PhysicsBody grabTarget)
    {
        this.grabTarget = grabTarget;
        mode = Mode.grabbingTarget;
        targetReached = false;
    }

    public void BeginHold(PhysicsBody grabTarget)
    {
        this.grabTarget = grabTarget;
        mode = Mode.holdingTarget;
        targetReached = false;
    }

    public bool Update()
    {
        return mode switch
        {
            Mode.standard => StandardBehavior(),
            Mode.grabbingTarget => GrabBehavior(),
            Mode.holdingTarget => HoldBehavior(),
            _ => false
        };
    }

    //check whether spring target has been reached
    private bool StandardBehavior()
    {
        if (targetReached)
        {
            return false;
        }

        //we should also check angular speed is low to make sure joint is actually settling at target angle,
        //but joints/springs will be heavily damped so they won't really be flying past their target

        var err1 = upperTarget.InverseMultiplyRotation(upperArmJoint.bodyA.rotation.InverseMultiplyRotation(upperArm.rotation)).direction;
        if (err1.x < 0 || Mathf.Abs(err1.y) > rotationTolerance)
        {
            return false;
        }

        var err2 = lowerTarget.InverseMultiplyRotation(lowerArmJoint.bodyA.rotation.InverseMultiplyRotation(lowerArm.rotation)).direction;
        if (err2.x < 0 || Mathf.Abs(err2.y) > rotationTolerance)
        {
            return false;
        }

        targetReached = true;
        return true;

    }

    private bool GrabBehavior()
    {
        if (targetReached)
        {
            return false;
        }

        if (grabTarget.isValid)
        {
            if (MaxDistance(grabTarget) < grabTolerance)
            {
                targetReached = true;
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            targetReached = true;
            return true;
        }
    }

    //will tighten grip whenever distance from grab arms to target is > grabThreshold
    //and will invoke "TargetReached" if target is DROPPED
    private bool HoldBehavior()
    {
        if (targetReached)
        {
            return false;
        }

        if (grabTarget.isValid)
        {
            if (MaxDistance(grabTarget) > dropTolerance)
            {
                targetReached = true;
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            targetReached = true;
            return true;
        }
    }

    private readonly float MaxDistance(PhysicsBody body)
    {
        return Mathf.Max(body.Distance(upperArm.GetShapes()[0]).distance, body.Distance(lowerArm.GetShapes()[0]).distance);
    }
}