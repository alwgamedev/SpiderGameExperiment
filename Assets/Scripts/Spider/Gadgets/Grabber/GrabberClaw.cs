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
public struct GrabberClaw
{
    PhysicsBody upperArm;
    PhysicsHingeJoint upperArmJoint;
    PhysicsBody lowerArm;
    PhysicsHingeJoint lowerArmJoint;
    PhysicsBody grabTarget;
    Vector2 upperArmInterior0;
    Vector2 upperArmInterior1;
    Vector2 lowerArmInterior0;
    Vector2 lowerArmInterior1;
    //interior of the claw has 4 vertices ordered like this:
    //           |
    //       (grabber)
    //           |
    // (lower0) --- (upper0)
    //    |            |
    //    |            |
    // (lower1) --- (upper1)
    //vertices are stored in local space of their respective arms

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

    public enum TaskResult
    {
        none, complete, failed
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

    public readonly void OnValidate(GrabberClawDefinition def)
    {
        if (upperArm.isValid)
        {
            upperArm.SetBodyDefLive(def.bodyDef);
            upperArm.SetShapeDef(def.shapeDef);
        }
        if (upperArmJoint.isValid)
        {
            upperArmJoint.UpdateSettings(def.jointDef, true, true);
        }

        if (lowerArm.isValid)
        {
            lowerArm.SetBodyDefLive(def.bodyDef);
            lowerArm.SetShapeDef(def.shapeDef);
        }
        if (lowerArmJoint.isValid)
        {
            lowerArmJoint.UpdateSettings(def.jointDef, true, true);
        }
    }

    //geometry should be local to physics transforms
    public void Initialize(PhysicsBody anchorBody, GrabberClawDefinition def,
        Transform upperArmPhysTransform, Transform[] upperArmBone,
        Transform lowerArmPhysTransform, Transform[] lowerArmBone)
    {
        Span<PolygonGeometry> upperArmGeometry = stackalloc PolygonGeometry[upperArmBone.Length - 1];
        Span<PolygonGeometry> lowerArmGeometry = stackalloc PolygonGeometry[lowerArmBone.Length - 1];

        CreateArmGeometry(upperArmGeometry, upperArmPhysTransform, upperArmBone, def.upperWidth, ref upperArmInterior0, ref upperArmInterior1, false);
        CreateArmGeometry(lowerArmGeometry, lowerArmPhysTransform, lowerArmBone, def.lowerWidth, ref lowerArmInterior0, ref lowerArmInterior1, true);

        upperArm = CreateArmBody(def.bodyDef, def.shapeDef, anchorBody, upperArmPhysTransform, upperArmGeometry);
        lowerArm = CreateArmBody(def.bodyDef, def.shapeDef, anchorBody, lowerArmPhysTransform, lowerArmGeometry);

        upperArmJoint = CreateArmJoint(def.jointDef, anchorBody, upperArm, upperArmBone[0].position);
        lowerArmJoint = CreateArmJoint(def.jointDef, anchorBody, lowerArm, lowerArmBone[0].position);

        static void CreateArmGeometry(Span<PolygonGeometry> geometry, Transform physTransform, Transform[] bone, Vector2[] width, 
            ref Vector2 interior0, ref Vector2 interior1, bool interiorIsUp)
        {
            for (int i = 0; i < geometry.Length; i++)
            {
                Vector2 p0 = bone[i].position;
                Vector2 p1 = bone[i + 1].position;
                var n = (p1 - p0).normalized.CCWPerp();

                var startN = 0.5f * width[i].x * n;
                var endN = 0.5f * width[i].y * n;
                Span<Vector2> vertices = stackalloc Vector2[4];
                var v0 = p0 - startN;
                var v1 = p0 + startN;
                var v2 = p1 + endN;
                var v3 = p1 - endN;
                vertices[0] = v0;
                vertices[1] = v1;
                vertices[2] = v2;
                vertices[3] = v3;

                var polygon = PolygonGeometry.Create(vertices);//world space
                geometry[i] = polygon.InverseTransform(physTransform.localToWorldMatrix, false);

                if (i == 0)
                {
                    if (interiorIsUp)
                    {
                        interior0 = physTransform.InverseTransformPoint(v1);
                    }
                    else
                    {
                        interior0 = physTransform.InverseTransformPoint(v0);
                    }
                }
                else if (i == geometry.Length - 1)
                {
                    if (interiorIsUp)
                    {
                        interior1 = physTransform.InverseTransformPoint(v2);
                    }
                    else
                    {
                        interior1 = physTransform.InverseTransformPoint(v3);
                    }
                }
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

    public void SetSpringTargetToCurrentRotation()
    {
        upperArmJoint.springTargetAngle = UpperArmJointRotation().degrees;
        lowerArmJoint.springTargetAngle = LowerArmJointRotation().degrees;
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
        SetSpringTarget(upperArmClosed, lowerArmClosed);
        mode = Mode.grabbingTarget;
        targetReached = false;
    }

    public void BeginHold(PhysicsBody grabTarget)
    {
        this.grabTarget = grabTarget;
        SetSpringTargetToCurrentRotation();
        mode = Mode.holdingTarget;
        targetReached = false;
    }

    public TaskResult Update()
    {
        return mode switch
        {
            Mode.standard => StandardBehavior(),
            Mode.grabbingTarget => GrabBehavior(),
            Mode.holdingTarget => HoldBehavior(),
            _ => TaskResult.none
        };
    }

    public bool EnclosesPoint(Vector2 worldPoint)
    {
        var u0 = upperArm.transform.TransformPoint(upperArmInterior0);
        var u1 = upperArm.transform.TransformPoint(upperArmInterior1);
        var l0 = lowerArm.transform.TransformPoint(lowerArmInterior0);
        var l1 = lowerArm.transform.TransformPoint(lowerArmInterior1);

        if (MathTools.Cross2D(u1 - u0, worldPoint - u0) > 0)
        {
            return false;
        }
        if (MathTools.Cross2D(l1 - u1, worldPoint - u1) > 0)
        {
            return false;
        }
        if (MathTools.Cross2D(l0 - l1, worldPoint - l1) > 0)
        {
            return false;
        }
        if (MathTools.Cross2D(u0 - l0, worldPoint - l0) > 0)
        {
            return false;
        }

        return true;
    }

    //claw arms will not "flip" due to asymmetric shapes. instead the upper and lower arms will swap sides on the anchor body.
    //that means we don't have to flip bones or spring angles :)
    public void OnDirectionChanged(PhysicsTransform reflection, Vector2 postTranslation)
    {
        ReflectClawArm(upperArm, upperArmJoint, reflection, postTranslation);
        ReflectClawArm(lowerArm, lowerArmJoint, reflection, postTranslation);

        static void ReflectClawArm(PhysicsBody armBody, PhysicsHingeJoint joint, PhysicsTransform reflection, Vector2 postTranslation)
        {
            armBody.transform = armBody.transform.Reflect(reflection);
            armBody.linearVelocity = armBody.linearVelocity.ReflectAcrossHyperplane(reflection.rotation.direction);

            //reflect arm over anchor body's x-axis
            var anchorBody = joint.bodyA;
            var reflectionOverAnchorBodyX = new PhysicsTransform(anchorBody.position, new PhysicsRotate(anchorBody.rotation.direction.CCWPerp()));
            armBody.transform = armBody.transform.Reflect(reflectionOverAnchorBodyX);
            armBody.linearVelocity = armBody.linearVelocity.ReflectAcrossHyperplane(reflectionOverAnchorBodyX.rotation.direction);

            //reflect joint anchorA
            var anchorA = joint.localAnchorA;
            joint.localAnchorA = new(-anchorA.position, new PhysicsRotate(-anchorA.rotation.direction));
            //joint.springTargetAngle = -joint.springTargetAngle;

            armBody.position += postTranslation;
            armBody.SyncTransform();
        }
    }

    //check whether spring target has been reached
    private TaskResult StandardBehavior()
    {
        if (targetReached)
        {
            return TaskResult.none;
        }

        var cur1 = UpperArmJointRotation();
        var err1 = upperTarget.InverseMultiplyRotation(UpperArmJointRotation()).direction;
            //upperTarget.InverseMultiplyRotation(upperArmJoint.bodyA.rotation.InverseMultiplyRotation(upperArm.rotation)).direction;
        if (err1.x < 0 || Mathf.Abs(err1.y) > rotationTolerance)
        {
            return TaskResult.none;
        }

        var cur2 = LowerArmJointRotation();
        var err2 = lowerTarget.InverseMultiplyRotation(LowerArmJointRotation()).direction;
            //lowerTarget.InverseMultiplyRotation(lowerArmJoint.bodyA.rotation.InverseMultiplyRotation(lowerArm.rotation)).direction;
        if (err2.x < 0 || Mathf.Abs(err2.y) > rotationTolerance)
        {
            return TaskResult.none;
        }

        targetReached = true;
        return TaskResult.complete;

    }

    private TaskResult GrabBehavior()
    {
        if (targetReached)
        {
            return TaskResult.none;
        }

        if (StandardBehavior() == TaskResult.complete)
        {
            //if claw closes fully, then return task failed
            return TaskResult.failed;
        }

        if (grabTarget.isValid)
        {
            var maxDist = MaxDistance(grabTarget);

            if (maxDist > dropTolerance)
            {
                targetReached = true;
                return TaskResult.failed;
            }

            if (maxDist < grabTolerance)
            {
                targetReached = true;
                return TaskResult.complete;
            }

            return TaskResult.none;
        }
        else
        {
            targetReached = true;
            return TaskResult.failed;
        }
    }

    //will tighten grip whenever distance from grab arms to target is > grabThreshold
    //and will invoke "TargetReached" if target is DROPPED
    private TaskResult HoldBehavior()
    {
        if (targetReached)
        {
            return TaskResult.none;
        }

        if (grabTarget.isValid)
        {
            var dist = MaxDistance(grabTarget);
            if (dist > dropTolerance)//target dropped
            {
                targetReached = true;
                return TaskResult.complete;
            }

            if (dist > grabTolerance)
            {
                //tighten grip
                SetSpringTarget(upperArmClosed, lowerArmClosed);
            }
            else if (upperArmJoint.springTargetAngle == upperArmClosed || upperArmJoint.springTargetAngle == lowerArmClosed)
            {
                //if grip already tight enough, set spring to current rotation to avoid clipping
                SetSpringTargetToCurrentRotation();
            }

            return TaskResult.none;
        }
        else
        {
            targetReached = true;
            return TaskResult.complete;
        }
    }

    private readonly float MaxDistance(PhysicsBody body)
    {
        return Mathf.Max(body.Distance(upperArm.GetShapes()[0]).distance, body.Distance(lowerArm.GetShapes()[0]).distance);
    }

    private readonly PhysicsRotate UpperArmJointRotation()
    {
        var upperRotA = upperArmJoint.bodyA.rotation.MultiplyRotation(upperArmJoint.localAnchorA.rotation);
        var upperRotB = upperArmJoint.bodyB.rotation.MultiplyRotation(upperArmJoint.localAnchorB.rotation);
        return upperRotA.InverseMultiplyRotation(upperRotB);
    }

    private readonly PhysicsRotate LowerArmJointRotation()
    {
        var lowerRotA = lowerArmJoint.bodyA.rotation.MultiplyRotation(lowerArmJoint.localAnchorA.rotation);
        var lowerRotB = lowerArmJoint.bodyB.rotation.MultiplyRotation(lowerArmJoint.localAnchorB.rotation);
        return lowerRotA.InverseMultiplyRotation(lowerRotB);
    }
}