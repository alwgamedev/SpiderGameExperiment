using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

public struct Projectile
{
    public float2 position;
    public float2 velocity;
    public float2 acceleration;
    public float damage;
    public float alive;
}

public struct ProjectileCollision
{
    public Projectile projectile;
    public PhysicsShape hitShape;
}

public class VFXGun
{
    [SerializeField] VisualEffect vfx;
    [SerializeField] PhysicsQuery.QueryFilter collisionFilter;
    [SerializeField] int capacity;
    [SerializeField] float radius;
    [SerializeField] float gravityScale;

    GraphicsBuffer projectile;
    NativeArray<ProjectileCollision> collision;
    int nextID;

    readonly int bufferProperty = Shader.PropertyToID("Projectile");

    public void Initialize()
    {
        ReleaseBuffers();

        projectile = new(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, capacity, 32);
        vfx.SetGraphicsBuffer(bufferProperty, projectile);

        collision = new(capacity, Allocator.Persistent);

        nextID = 0;
    }

    public bool Shoot()
    {
        return false;
    }

    public void Update(float dt)
    {
        collision.FillArray(default, 0, collision.Length);
        var proj = projectile.LockBufferForWrite<Projectile>(0, projectile.count);
        var updateJob = new UpdateProjectilesJob(proj, collision, PhysicsWorld.defaultWorld, collisionFilter, dt, radius);
        updateJob.Run();
        projectile.UnlockBufferAfterWrite<Projectile>(projectile.count);

        //then handle collisions
    }

    public void ReleaseBuffers()
    {
        projectile?.Release();
        if (collision.IsCreated)
        {
            collision.Dispose();
        }
    }

    [BurstCompile]
    struct UpdateProjectilesJob : IJob
    {
        public NativeArray<Projectile> projectile;
        public NativeArray<ProjectileCollision> collision;
        [ReadOnly] public readonly PhysicsWorld world;
        public readonly PhysicsQuery.QueryFilter filter;
        public readonly float dt;
        public readonly float radius;

        public UpdateProjectilesJob(NativeArray<Projectile> projectile, NativeArray<ProjectileCollision> collision,
            PhysicsWorld world, PhysicsQuery.QueryFilter filter, float dt, float radius)
        {
            this.projectile = projectile;
            this.collision = collision;
            this.world = world;
            this.filter = filter;
            this.dt = dt;
            this.radius = radius;
        }

        public void Execute()
        {
            for (int i = 0; i < projectile.Length; i++)
            {
                var p = projectile[i];
                if (p.alive == 0)
                {
                    return;
                }

                p.velocity += dt * p.acceleration;
                p.position += dt * p.velocity;

                var circle = new CircleGeometry() { center = p.position, radius = radius };
                var overlap = world.OverlapGeometry(circle, filter);

                if (Hint.Unlikely(overlap.Length > 0))
                {
                    collision[i] = new() { projectile = p, hitShape = overlap[0].shape };
                    p.alive = 0;
                }

                projectile[i] = p;
            }
        }
    }
}