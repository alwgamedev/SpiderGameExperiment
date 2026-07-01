using System;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.VFX;

[Serializable]
public class VFXShooter
{
    [SerializeField] VisualEffect vfx;
    [SerializeField] PhysicsQuery.QueryFilter collisionFilter;
    [SerializeField] int capacity;
    [SerializeField] float collisionRadius;
    [SerializeField] float lifetime;

    GraphicsBuffer projectileGB;
    NativeArray<Projectile> projectile;//because we can't read from gb (to see current position, etc.) we need to keep data on cpu
    public NativeArray<ProjectileCollision> collision;
    int nextProjectileID;

    readonly int bufferProperty = Shader.PropertyToID("Projectile");
    readonly int projectileIDProperty = Shader.PropertyToID("ProjectileID");
    readonly int shootEventProperty = Shader.PropertyToID("ShootProjectileEvent");

    public void Initialize()
    {
        ReleaseBuffers();

        projectileGB = new(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, capacity, 32);
        vfx.SetGraphicsBuffer(bufferProperty, projectileGB);

        collision = new(capacity, Allocator.Persistent);
        projectile = new(capacity, Allocator.Persistent);

        nextProjectileID = 0;
    }

    public void Shoot(Projectile p)
    {
        if (projectile[nextProjectileID].lifetime > 0)
        {
            //this just means we've circled back to the beginning of queue (first fired projectile)
            //and it's still alive... there could be free slots past it.
            //so we could loop until we find a free id but meh just use a large enough capacity.
            return;
        }

        var id = nextProjectileID;
        p.lifetime = lifetime;
        projectile[id] = p;
        var projSlice = projectileGB.LockBufferForWrite<Projectile>(id, 1);
        projSlice[0] = p;
        projectileGB.UnlockBufferAfterWrite<Projectile>(1);
        
        var shootEvent = vfx.CreateVFXEventAttribute();
        shootEvent.SetInt(projectileIDProperty, id);
        vfx.SendEvent(shootEventProperty, shootEvent);

        nextProjectileID++;
        if (nextProjectileID == projectile.Length)
        {
            nextProjectileID = 0;
        }
    }

    public void Update(float dt)
    {

        var updateJob = new UpdateProjectilesJob(projectile, collision, PhysicsWorld.defaultWorld,
            collisionFilter, dt, collisionRadius);
        updateJob.Run();        
        var proj = projectileGB.LockBufferForWrite<Projectile>(0, projectileGB.count);
        proj.CopyFrom(projectile);
        projectileGB.UnlockBufferAfterWrite<Projectile>(projectileGB.count);

        //then owner can handle collisions 
        //including (optionally) setting collision[i] back to default after handled
    }

    public void ReleaseBuffers()
    {
        projectileGB?.Release();
        if (projectile.IsCreated)
        {
            projectile.Dispose();
        }
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
        public readonly float collisionRadius;

        public UpdateProjectilesJob(NativeArray<Projectile> projectile, NativeArray<ProjectileCollision> collision, 
            PhysicsWorld world, PhysicsQuery.QueryFilter filter, float dt, float collisionRadius)
        {
            this.projectile = projectile;
            this.collision = collision;
            this.world = world;
            this.filter = filter;
            this.dt = dt;
            this.collisionRadius = collisionRadius;
        }

        public void Execute()
        {
            for (int i = 0; i < projectile.Length; i++)
            {
                var p = projectile[i];
                if (!(p.lifetime > 0))
                {
                    continue;
                }
                
                p.lifetime -= dt;
                p.velocity += dt * p.acceleration;
                p.position += dt * p.velocity;

                var circle = new CircleGeometry() { center = p.position, radius = collisionRadius };
                var overlap = world.OverlapGeometry(circle, filter);
                if (Hint.Unlikely(overlap.Length > 0))
                {
                    collision[i] = new() { projectile = p, hitShape = overlap[0].shape };
                    p.lifetime = 0;//kill particle
                }

                projectile[i] = p;
            }
        }
    }
}