using UnityEngine;

public static class PhysicsBasedIK
{
    //acceleration should already by multiplied by deltaTime
    //damping useful for collision (to keep from bouncing off collision repeatedly)
    public static void ApplyForceToJoint(Transform[] chain, float[] length, float[] angularVelocity, Vector2 acceleration, int joint)
    {
        for (int i = joint - 1; i > -1; i--)
        {
            if (acceleration == Vector2.zero)
            {
                break;
            }

            Vector2 u = chain[i + 1].position - chain[i].position;
            u /= length[i];
            var n = u.CCWPerp();
            var a = acceleration.magnitude;
            var dot = Vector2.Dot(acceleration, n);
            if (dot == 0)
            {
                continue;
            }
            var aPerp = a * Mathf.Sign(dot) / length[i];
            //using magnitude of the original acceleration, rather than just normal component helps keep it from getting bogged down when direction of force
            //is nearly parallel to arm
            angularVelocity[i] += aPerp;
            acceleration -= aPerp * n;//component of acceleration parallel to that arm will get transferred to next joint
        }
    }

    public static void IntegrateJoints(Transform[] chain, float[] length, float[] angularVelocity, float damping, float dt)
    {
        for (int i = 0; i < chain.Length - 1; i++)
        {
            angularVelocity[i] -= damping * angularVelocity[i] * dt;
            //chain[i].ApplyCheapRotationBySpeed(angularVelocity[i], dt, out _);
            var dAngle = dt * angularVelocity[i];
            var q = MathTools.QuaternionFrom2DUnitVector(new(Mathf.Cos(dAngle), Mathf.Sin(dAngle)));//honestly worth it to just use trig functions
            chain[i].rotation *= q;
            chain[i + 1].position = chain[i].position + length[i] * chain[i].right;
        }
    }

    //collisionResponse can already be multiplied by dt
    public static void ApplyCollisionForces(Transform[] chain, float[] length, float[] armHalfWidth, float[] angularVelocity,
        int collisionMask, float collisionResponse, float horizontalRaycastSpacing, float tunnelInterval, float tunnelMax
        /*float collisionDamping*/)
    {
        for (int i = chain.Length - 2; i > -1; i--)
        {
            Vector2 p0 = chain[i].position;
            Vector2 p1 = chain[i + 1].position;
            Vector2 u = (p1 - p0) / length[i];
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


                var a = correction * collisionResponse * n;
                ApplyForceToJoint(chain, length, angularVelocity, a, i/*, collisionDamping*/);
                ApplyForceToJoint(chain, length, angularVelocity, a, i + 1/*, collisionDamping*/);

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
                    var q2 = q1 + (escapingUp ? -armHalfWidth[i] : armHalfWidth[i]) * n;

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