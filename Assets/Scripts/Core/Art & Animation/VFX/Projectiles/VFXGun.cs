using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.U2D.Physics;
using UnityEngine;
using UnityEngine.VFX;

public class VFXGun
{
    [SerializeField] VisualEffect vfx;
    [SerializeField] PhysicsQuery.QueryFilter collisionFilter;
    [SerializeField] int capacity;
    [SerializeField] float collisionRadius;

    public GraphicsBuffer projectile;
    public NativeArray<ProjectileCollision> collision;
    NativeArray<bool> alive;//can't read from GraphicsBuffer so need a cpu-side alive tracker
    int nextProjectileID;

    readonly int bufferProperty = Shader.PropertyToID("Projectile");

    public void Initialize()
    {
        ReleaseBuffers();

        projectile = new(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, capacity, 32);
        vfx.SetGraphicsBuffer(bufferProperty, projectile);

        collision = new(capacity, Allocator.Persistent);
        alive = new(alive, Allocator.Persistent);

        nextProjectileID = 0;
    }

    public void Shoot(Projectile p)
    {
        if (alive[nextProjectileID])
        {
            return;
        }

        p.alive = 1;
        var projSlice = projectile.LockBufferForWrite<Projectile>(nextProjectileID, 1);
        projSlice[0] = p;
        projectile.UnlockBufferAfterWrite<Projectile>(1);
        
        alive[nextProjectileID] = true;
        nextProjectileID++;
        if (nextProjectileID == projectile.count)
        {
            nextProjectileID = 0;
        }

        //we still need to send spawn event to vfx (with event attribute for the projectile id)
    }

    public void Update(float dt)
    {
        var proj = projectile.LockBufferForWrite<Projectile>(0, projectile.count);
        var updateJob = new UpdateProjectilesJob(proj, collision, alive, PhysicsWorld.defaultWorld, collisionFilter, dt, collisionRadius);
        updateJob.Run();
        projectile.UnlockBufferAfterWrite<Projectile>(projectile.count);

        //then owner can handle collisions 
        //including (optionally) setting collision[i] back to default after handled
    }

    public void ReleaseBuffers()
    {
        projectile?.Release();
        if (collision.IsCreated)
        {
            collision.Dispose();
        }
        if (alive.IsCreated)
        {
            alive.Dispose();
        }
    }

    [BurstCompile]
    struct UpdateProjectilesJob : IJob
    {
        public NativeArray<Projectile> projectile;
        public NativeArray<ProjectileCollision> collision;
        public NativeArray<bool> alive;
        [ReadOnly] public readonly PhysicsWorld world;
        public readonly PhysicsQuery.QueryFilter filter;
        public readonly float dt;
        public readonly float collisionRadius;

        public UpdateProjectilesJob(NativeArray<Projectile> projectile, NativeArray<ProjectileCollision> collision,
            NativeArray<bool> alive, PhysicsWorld world, PhysicsQuery.QueryFilter filter, float dt, float collisionRadius)
        {
            this.projectile = projectile;
            this.collision = collision;
            this.alive = alive;
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
                if (p.alive == 0)
                {
                    return;
                }

                p.velocity += dt * p.acceleration;
                p.position += dt * p.velocity;

                var circle = new CircleGeometry() { center = p.position, radius = collisionRadius };
                var overlap = world.OverlapGeometry(circle, filter);

                if (Hint.Unlikely(overlap.Length > 0))
                {
                    collision[i] = new() { projectile = p, hitShape = overlap[0].shape };
                    p.alive = 0;
                    alive[i] = false;
                }

                projectile[i] = p;
            }
        }
    }
}