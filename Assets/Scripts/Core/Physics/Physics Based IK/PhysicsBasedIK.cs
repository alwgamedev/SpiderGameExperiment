using UnityEngine;

public static class PhysicsBasedIK
{
    const float PIInverse = 1 / Mathf.PI;

    //apply force to joint and disappate parallel component up the chain (towards "hip")
    public static void ApplyForceUpChain(Transform[] chain, float[] inverseLength, float[] angularVelocity, Vector2 acceleration, int joint,
        float[] poseWeight = null, bool applyUniformly = false)
    {
        for (int i = joint - 1; i > -1; i--)
        {
            Vector2 u = chain[i + 1].position - chain[i].position;
            u *= inverseLength[i];
            var n = u.CCWPerp();
            var aPerp = Vector2.Dot(acceleration, n);
            if (poseWeight != null)
            {
                aPerp *= 1 - poseWeight[i];
            }
            angularVelocity[i] += aPerp * inverseLength[i];

            if (!applyUniformly)
            {
                acceleration -= aPerp * n;//component of acceleration parallel to that arm will get transferred to next joint
            }
        }
    }

    //apply force to joint and disappate parallel component down the chain (towards "foot")
    public static void ApplyForceDownChain(Transform[] chain, float[] inverseLength, float[] angularVelocity, Vector2 acceleration, int joint,
        float[] poseWeight = null, bool applyUniformly = false)
    {
        for (int i = joint - 1; i < chain.Length - 1; i++)
        {
            Vector2 u = chain[i + 1].position - chain[i].position;
            u *= inverseLength[i];
            var n = u.CCWPerp();
            var aPerp = Vector2.Dot(acceleration, n);
            if (poseWeight != null)
            {
                aPerp *= 1 - poseWeight[i];
            }
            angularVelocity[i] += aPerp * inverseLength[i];
            if (!applyUniformly)
            {
                acceleration -= aPerp * n;//component of acceleration parallel to that arm will get transferred to next joint
            }
        }
    }

    public static void ApplyForceDownChain(Transform[] chain, float[] inverseLength, float[] angularVelocity, Vector2 acceleration, int joint,
        int maxIterations, float tolerance2)
    {
        var a0 = acceleration;
        tolerance2 *= acceleration.sqrMagnitude;//good, so we will go to a fraction of the original magnitude (meaning small forces don't get ignored)
        for (int j = 0; j < maxIterations; j++)
        {
            for (int i = joint - 1; i < chain.Length - 1; i++)
            {
                if (Vector2.Dot(acceleration, a0) < 0 || acceleration.sqrMagnitude < tolerance2)
                {
                    return;
                }

                Vector2 u = chain[i + 1].position - chain[i].position;
                u *= inverseLength[i];
                var n = u.CCWPerp();
                var aPerp = Vector2.Dot(acceleration, n);
                angularVelocity[i] += aPerp * inverseLength[i];
                acceleration -= aPerp * n;//component of acceleration parallel to that arm will get transferred to next joint
            }
        }
    }

    public static void ReachForTargetWithAngleBounds(Transform[] chain, float[] length, float[] inverseLength, float[] angularVelocity,
        float[] minAngle, float[] maxAngle, bool[] angleBranch,
        float orientation, Vector2 target, float reachForce, float toleranceSqrd, float dt)
    {
        target -= (Vector2)chain[^1].position;
        reachForce *= dt;
        var c0 = 0.5f * orientation * dt;
        var lastDv = 0f;

        for (int i = 0; i < angularVelocity.Length; i++)
        {
            if (target.sqrMagnitude < toleranceSqrd)
            {
                return;
            }

            var a = reachForce * target;
            Vector2 u = chain[i + 1].position - chain[i].position;
            u *= inverseLength[i];
            var n = u.CCWPerp();
            var aPerp = Vector2.Dot(a, n);

            if (angleBranch[i])
            {
                var z0 = Mathf.Sign(chain[i].localRotation.z) * chain[i].localRotation.w;

                var c = -c0 * Mathf.Abs(chain[i].localRotation.z);

                if (c == 0)
                {

                    lastDv = inverseLength[i] * aPerp;
                    angularVelocity[i] += lastDv;
                    target += aPerp * dt * n;
                    continue;
                }

                var z1 = z0 + c * (inverseLength[i] * aPerp - lastDv);
                z1 = Mathf.Clamp(z1, minAngle[i], maxAngle[i]);
                z1 = ((z1 - z0) / c + lastDv) * length[i];
                aPerp = Mathf.Clamp(z1, -reachForce, reachForce);

                lastDv = inverseLength[i] * aPerp;
                angularVelocity[i] += lastDv;
                target += aPerp * dt * n;
            }
            else
            {
                var z0 = Mathf.Sign(chain[i].localRotation.w) * chain[i].localRotation.z;
                var c = c0 * Mathf.Abs(chain[i].localRotation.w);

                if (c == 0)
                {
                    lastDv = inverseLength[i] * aPerp;
                    angularVelocity[i] += lastDv;
                    target +=  aPerp * dt * n;
                    continue;
                }

                var z1 = z0 + c * (inverseLength[i] * aPerp - lastDv);
                z1 = Mathf.Clamp(z1, minAngle[i], maxAngle[i]);
                z1 = ((z1 - z0) / c + lastDv) * length[i];
                aPerp = Mathf.Clamp(z1, -reachForce, reachForce);

                lastDv = inverseLength[i] * aPerp;
                angularVelocity[i] += lastDv;
                target += aPerp * dt * n;
            }
        }
    }

    public static void ApplyGravity(Transform[] chain, float[] inverseLength, float[] angularVelocity, float gravityScale, float dt)
    {
        var g = gravityScale * dt * Physics2D.gravity;
        for (int i = 0; i < angularVelocity.Length; i++)
        {
            Vector2 u = chain[i + 1].position - chain[i].position;
            angularVelocity[i] += inverseLength[i] * inverseLength[i] * Vector2.Dot(g, u.CCWPerp());
        }
    }

    public static void IntegrateJoints(Transform[] chain, float[] angularVelocity, float damping, float dt,
    bool rotateJointsIndependently = true)
    {
        for (int i = 0; i < angularVelocity.Length; i++)
        {
            angularVelocity[i] -= damping * angularVelocity[i] * dt;
            var u = MathTools.CheapRotationalLerp(Vector2.right, Vector2.left, dt * angularVelocity[i] * PIInverse, out _);
            var q = MathTools.QuaternionFrom2DUnitVector(u);
            chain[i].rotation *= q;
            if (rotateJointsIndependently && i < chain.Length - 2)
            {
                chain[i + 1].rotation *= MathTools.InverseOfUnitQuaternion(q);
                //if chain transforms nested, this allows joints to rotate independently
                //(i.e. when joint i rotates, all later joints maintain their world rotation)
            }
        }
    }

    public static void ApplyCollisionForces(Transform[] chain, float[] length, float[] inverseLength, float[] armHalfWidth, float[] angularVelocity,
        LayerMask collisionMask, float collisionResponse, float horizontalRaycastSpacing, float tunnelInterval, float tunnelMax)
    {
        for (int i = chain.Length - 2; i > -1; i--)
        {
            Vector2 p0 = chain[i].position;
            Vector2 p1 = chain[i + 1].position;
            Vector2 u = (p1 - p0) * inverseLength[i];
            var n = u.CCWPerp();
            var h = armHalfWidth[i] * n;
            var r = Physics2D.Linecast(p0 + h, p1 + h, collisionMask);
            bool initialHitOnUpSide = true;

            if (!r)
            {
                initialHitOnUpSide = false;
                r = Physics2D.Linecast(p0 - h, p1 - h, collisionMask);
            }

            if (r)
            {
                var q0 = r.point;
                var y0 = initialHitOnUpSide ? armHalfWidth[i] : -armHalfWidth[i];
                var qMid = q0 - y0 * n;

                float correction;

                if (initialHitOnUpSide)
                {
                    var s = Physics2D.Linecast(r.point - 2 * h, r.point, collisionMask);
                    if (s && s.distance > 0)
                    {
                        //then we know obstacle is on + side
                        correction = EscapeYValue(false) - armHalfWidth[i];

                    }
                    else
                    {
                        var escapeYUp = EscapeYValue(true);
                        var escapeYDown = EscapeYValue(false);
                        var escapingUp = escapeYUp < escapeYDown;
                        var escapeY = Mathf.Min(escapeYUp, escapeYDown);
                        correction = escapingUp ? escapeY + armHalfWidth[i] : escapeY - armHalfWidth[i];
                    }
                }
                else
                {
                    //we know obstacle is on - side, since initial cast on + side did not hit
                    correction = EscapeYValue(true) + armHalfWidth[i];
                }


                var a = correction * collisionResponse * inverseLength[i] * n;
                ApplyForceUpChain(chain, inverseLength, angularVelocity, a, i);
                ApplyForceUpChain(chain, inverseLength, angularVelocity, a, i + 1);

                float EscapeYValue(bool escapingUp)
                {
                    var peak = 0f;
                    var x = 0f;

                    while (x < length[i])
                    {
                        var y = EscapeYValueAtX(x, escapingUp);
                        if (escapingUp && y > peak)
                        {
                            peak = y;
                            if (!(peak < tunnelMax))
                            {
                                break;
                            }
                        }
                        else if (!escapingUp && y < peak)
                        {
                            peak = y;
                            if (!(peak > -tunnelMax))
                            {
                                break;
                            }
                        }

                        x += horizontalRaycastSpacing;
                    }

                    return peak;
                }

                float EscapeYValueAtX(float x, bool escapingUp)
                {
                    var shiftInterval = escapingUp ? tunnelInterval : -tunnelInterval;
                    var y = 0f;

                    bool YInBounds()
                    {
                        y += shiftInterval;
                        return y < tunnelMax && y > -tunnelMax;
                    }

                    var q1 = qMid + x * u;

                    while (YInBounds())
                    {
                        var r = Physics2D.Linecast(q1 + y * n, q1, collisionMask);
                        if (r && r.distance > 0)
                        {
                            return Vector2.Dot(r.point - p0, n);
                        }
                    }

                    return escapingUp ? tunnelMax : -tunnelMax;
                }
            }
        }
    }
}