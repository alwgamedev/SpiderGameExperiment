using UnityEngine;
using System;
using Unity.U2D.Physics;
using UnityEngine.VFX;
using Unity.Mathematics;
using Unity.Collections;

[Serializable]
public class GrappleShootPreview
{
    [SerializeField] VisualEffect visualEffect;
    [SerializeField] int spawnCount;
    [SerializeField] float arcLengthStep;
    [SerializeField] int spacing;//how many arc length steps between particles
    [SerializeField] PhysicsQuery.QueryFilter terminationFilter;

    GraphicsBuffer positionGB;
    PhysicsWorld world;

    readonly int positionGBProperty = Shader.PropertyToID("Position");
    readonly int endProperty = Shader.PropertyToID("End");//particles at index >= end are invisible
    readonly int spawnCountProperty = Shader.PropertyToID("SpawnCount");
    readonly int boundsCenterProperty = Shader.PropertyToID("BoundsCenter");

    public void OnValidate()
    {
        if (visualEffect)
        {
            spawnCount = visualEffect.GetInt(spawnCountProperty);
        }
    }

    public void Start(PhysicsWorld world)
    {
        this.world = world;

        positionGB = new(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, spawnCount, 16);
        visualEffect.SetGraphicsBuffer(positionGBProperty, positionGB);
        // visualEffect.SetInt(spawnCountProperty, spawnCount);
        visualEffect.enabled = false;
        // visualEffect.Play();//make sure vfx doesn't play on awake (initial spawn event is empty)
    }

    public void OnDestroy()
    {
        positionGB?.Release();
    }

    public void LateUpdate(GrappleCannon grapple)
    {
        if (grapple.PoweringUp)
        {
            if (!visualEffect.enabled)
            {
                visualEffect.enabled = true;
            }
            UpdateVFXData(grapple);
        }
        else if (visualEffect.enabled)
        {
            visualEffect.enabled = false;
        }
    }

    //we could move this to job
    private void UpdateVFXData(GrappleCannon grapple)
    {
        var position = positionGB.LockBufferForWrite<Vector4>(0, positionGB.count);

        var p = grapple.SourcePosition;
        var v0 = grapple.ShootSpeed * grapple.ShootDirection;//initial velocity
        var g = world.gravity;
        var l = 0.5f * g;

        var p0 = p;
        position[0] = p;
        visualEffect.SetVector3(boundsCenterProperty, p0);

        float t = 0;
        int i = 0;
        int j = spacing / 2;
        var v = v0 + t * g;//curve velocity
        var speedInverse = math.rsqrt(v.x * v.x + v.y * v.y);
        while (i < position.Length - 1)
        {
            t += arcLengthStep * speedInverse;//increases arc length by ~arcLengthStep
            var p1 = new Vector2(p.x + t * v0.x + t * t * l.x, p.y + t * v0.y + t * t * l.y);//evaluate shoot curve at time t
            v = v0 + t * g;//ready for next iteration, or if we need tangentDir to add point
            speedInverse = math.rsqrt(v.x * v.x + v.y * v.y);

            var cast = PhysicsWorld.defaultWorld.CastRay(p0, p1 - p0, terminationFilter);
            if (cast.Length > 0)
            {
                if (j > 0.25f * spacing)
                {
                    AddPoint(position, ref i, ref p0, cast[0].point, speedInverse * v);
                }
                break;
            }

            if (j == spacing)
            {
                j = 0;
                AddPoint(position, ref i, ref p0, p1, speedInverse * v);
            }

            j++;
        }

        positionGB.UnlockBufferAfterWrite<Vector4>(i);

        visualEffect.SetInt(endProperty, i);

        static void AddPoint(NativeArray<Vector4> position, ref int i, ref Vector2 p0, Vector2 p1, Vector2 tangentDir)
        {
            i++;
            position[i] = new Vector4(p1.x, p1.y, tangentDir.x, tangentDir.y);
            p0 = p1;
        }
    }
}