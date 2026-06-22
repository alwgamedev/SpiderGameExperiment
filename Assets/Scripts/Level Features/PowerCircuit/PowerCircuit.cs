using System;
using Unity.U2D.Physics;
using UnityEngine;

public class PowerCircuit : MonoBehaviour
{
    [Serializable]
    public struct PowerPort
    {
        public PowerBeam beam;
        public int platform;
        public int powerRequired;//platform must have this many inlets with power to activate beam

        public readonly Vector2 Position => beam.transform.position;
        public readonly Vector2 Direction => beam.transform.right;
        public readonly bool BeamActive => beam.visualEffect.enabled;

        public readonly void ActivateBeam()
        {
            beam.length = 0;
            beam.goalLength = beam.maxLength;
            beam.visualEffect.enabled = true;
        }

        public readonly void DeactivateBeam()
        {
            beam.length = 0;
            beam.goalLength = 0;
            beam.visualEffect.enabled = false;
        }

        public readonly bool HitsTarget(Vector2 target, float distanceTolerance, float verticalTolerance)
        {
            Vector2 beamDir = Direction;
            Vector2 beamPos = Position;
            var d = target - beamPos;//not yet normalized

            if (Vector2.Dot(beamDir, d) < 0)
            {
                return false;
            }

            var d2 = d.sqrMagnitude;
            var l = beam.length;
            var lTolerant = l + distanceTolerance;
            if (lTolerant * lTolerant < d.sqrMagnitude)
            {
                return false;
            }

            return Mathf.Abs(MathTools.Cross2D(d, beamDir)) < verticalTolerance;
        }
    }

    [SerializeField] FixedJointKit[] platform;
    [SerializeField] PowerPort[] port;/*//sorted by platform*/ //that's a commented out comment
    [SerializeField] float horizontalTolerance;
    [SerializeField] float verticalTolerance;
    [SerializeField] float looseLinearFrequency;
    [SerializeField] float lockedLinearFrequency;
    [SerializeField] float looseAngularFrequency;
    [SerializeField] float lockedAngularFrequency;

    int[] powerIn;//[platform i] -> how many of its ports are receiving power
    int[] lockingPort;//index of outgoing port that was used to lock the platform, or -1 if not locked
    int[] connection;//[port i] = j if beam on port i is shooting into port j (or vice versa); conn[i] = -1 if no connection

    void OnValidate()
    {
        if (lockingPort != null)
        {
            for (int i = 0; i < platform.Length; i++)
            {
                var p = platform[i];
                if (p && p.joint.isValid)
                {
                    if (lockingPort[i] < 0)
                    {
                        p.joint.linearFrequency = looseLinearFrequency;
                        p.joint.angularFrequency = looseAngularFrequency;
                    }
                    else
                    {
                        p.joint.linearFrequency = lockedLinearFrequency;
                        p.joint.angularFrequency = lockedAngularFrequency;
                    }
                }
            }
        }
    }

    private void Start()
    {
        powerIn = new int[platform.Length];
        lockingPort = new int[platform.Length];
        Array.Fill(lockingPort, -1);
        connection = new int[port.Length];
        Array.Fill(connection, -1);

        for (int i = 0; i < platform.Length; i++)
        {
            var p = platform[i];
            if (!p)
            {
                continue;
            }
            if (!p.joint.isValid)
            {
                p.CreateJoint();
            }
            UnlockPlatform(p.joint);
        }
    }

    private void Update()
    {
        SyncTransforms();
        CheckForLostConnections();
        UpdateBeamActivation();
        CheckForNewConnections();
        UpdatePlatformLocking();
    }

    private void SyncTransforms()
    {
        for (int i = 0; i < platform.Length; i++)
        {
            //make sure port transforms are accurate
            if (platform[i])
            {
                platform[i].joint.bodyB.SyncTransform();
            }
        }
    }

    private void CheckForLostConnections()
    {
        for (int i = 0; i < connection.Length; i++)
        {
            var j = connection[i];
            if (j < 0)
            {
                continue;
            }

            var portA = port[i];
            var portB = port[j];

            ref var portOut = ref portA.BeamActive ? ref portA : ref portB;
            ref var portIn = ref portA.BeamActive ? ref portB : ref portA;

            if (!portOut.HitsTarget(portIn.Position, horizontalTolerance, verticalTolerance))
            {
                connection[i] = -1;
                connection[j] = -1;
                if (!(portIn.platform < 0))
                {
                    powerIn[portIn.platform]--;
                }
            }
        }
    }

    private void UpdateBeamActivation()
    {
        var check = true;
        while (check)
        {
            check = false;
            for (int i = 0; i < port.Length; i++)
            {
                var p = port[i];
                var conn = connection[i];
                var power = p.platform < 0 ? 0 : powerIn[p.platform];
                var hasPower = !(power < p.powerRequired);
                if (p.BeamActive && !hasPower)
                {
                    //if beam is active and doesn't have enough power coming in,
                    //deactivate the beam and kill any connections.
                    p.DeactivateBeam();
                    if (!(conn < 0))
                    {
                        connection[i] = -1;
                        connection[conn] = -1;
                        if (!(port[conn].platform < 0))
                        {
                            powerIn[port[conn].platform]--;
                            check = true;//we reduced power to another platform, so break and start the check again
                            break;
                        }
                    }
                }
                else if (!p.BeamActive && conn < 0 && hasPower)
                {
                    //if beam has power and isn't being used as an inlet,
                    //it's beam should be activated. note that this doesn't create any new connections or
                    //affect power available to any other platforms, because the beam starts at length zero and can't reach anything yet.
                    p.ActivateBeam();
                }
            }
        }
    }

    private void CheckForNewConnections()
    {
        for (int i = 0; i < port.Length; i++)
        {
            var p = port[i];
            if (!(connection[i] < 0))
            {
                continue;
            }

            for (int j = i + 1; j < port.Length; j++)
            {
                var q = port[j];
                if (!(connection[j] < 0) || !(p.BeamActive ^ q.BeamActive))
                {
                    continue;
                }

                ref var portOut = ref p.BeamActive ? ref p : ref q;
                ref var portIn = ref p.BeamActive ? ref q : ref p;

                if (portOut.HitsTarget(portIn.Position, horizontalTolerance, verticalTolerance))
                {
                    connection[i] = j;
                    connection[j] = i;
                    if (!(portIn.platform < 0))
                    {
                        powerIn[portIn.platform]++;
                    }
                    break;
                }
            }
        }
    }

    private void UpdatePlatformLocking()
    {
        //unlock platforms whose locking port has lost connection
        for (int i = 0; i < platform.Length; i++)
        {
            if (!platform[i])
            {
                continue;
            }

            var j = lockingPort[i];
            if (j < 0)
            {
                continue;
            }

            if (connection[j] < 0 || (!port[j].BeamActive && !port[connection[j]].BeamActive))
            {
                lockingPort[i] = -1;
                UnlockPlatform(platform[i].joint);
            }
        }

        //lock platforms based on first valid connection
        for (int i = 0; i < port.Length; i++)
        {
            var p = port[i];
            if (connection[i] < 0)
            {
                continue;
            }

            var j = p.platform;
            if (j < 0 || !platform[j] || !(lockingPort[j] < 0))
            {
                continue;
            }

            lockingPort[j] = i;
            LockPlatform(platform[j].joint, p, port[connection[i]]);
        }
    }

    private void LockPlatform(PhysicsFixedJoint joint, PowerPort portA, PowerPort portB)
    {
        var dist = Vector2.Distance(portA.Position, portB.Position);
        var solvedPortPos = portB.Position + dist * portB.Direction;
        var solvedPortRotation = new PhysicsRotate() { direction = -portB.Direction };
        var solvedPortTransform = new PhysicsTransform()
        {
            position = solvedPortPos,
            rotation = solvedPortRotation
        };

        var curPortRotation = new PhysicsRotate() { direction = portA.Direction };
        var curPortTransform = new PhysicsTransform(portA.Position, curPortRotation);

        var anchorBody = joint.bodyA;
        var platformBody = joint.bodyB;
        joint.localAnchorA = anchorBody.transform.InverseMultiplyTransform(solvedPortTransform);
        joint.localAnchorB = platformBody.transform.InverseMultiplyTransform(curPortTransform);

        joint.linearFrequency = lockedLinearFrequency;
        joint.angularFrequency = lockedAngularFrequency;
    }

    private void UnlockPlatform(PhysicsFixedJoint platform)
    {
        platform.localAnchorA = PhysicsTransform.identity;
        platform.localAnchorB = PhysicsTransform.identity;
        platform.linearFrequency = looseLinearFrequency;
        platform.angularFrequency = looseAngularFrequency;
    }
}