using System.Collections.Generic;
using UnityEngine;
using Unity.U2D.Physics;
using System;
using UnityEngine.VFX;

[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct Projectile
{
    public Vector2 position;
    public Vector2 velocity;
    public Vector2 acceleration;
    public float damage;
    public float lifetime;//remaining lifetime
}

public struct ProjectileCollision
{
    public Projectile projectile;
    public PhysicsShape hitShape;
}

public interface IProjectileTarget
{
    public int ProjectileTargetID { get; }

    public void HandleProjectileHit(ProjectileCollision hit);
}

public static class ProjectileTargetRegistry
{
    static List<IProjectileTarget> targetList;

    static int nextID;

    public static IProjectileTarget Target(int id)
    {
        if (id == 0 || !(id < targetList.Count))
        {
            return null;
        }

        return targetList[id];
    }

    /// <summary> Returns id it was registered with, and IProjectileTarget implementer can decide how/if to store that id.
    /// Make sure to Release when the IProjectileTarget is destroyed.</summary>
    public static int Register(IProjectileTarget target)
    {
        var id = target.ProjectileTargetID;
        if (id != 0)
        {
            Debug.Log("Projectile target already registered");
            return id;
        }

        if (!(nextID > 0))
        {
            Debug.Log("Out of IDs!");
            return 0;
        }

        id = nextID;
        if (!(id < targetList.Count))
        {
            for (int i = targetList.Count; i < id + 1; i++)
            {
                targetList.Add(null);
            }
        }

        targetList[id] = target;

        nextID++;
        return id;
    }

    public static void Release(IProjectileTarget t)
    {
        var id = t.ProjectileTargetID;
        if (id != 0 && id < targetList.Count)
        {
            targetList[id] = null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        nextID = 1;
        
        targetList = new List<IProjectileTarget>(1024);

        Application.quitting += OnApplicationQuit;
        AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
    }

    private static void OnApplicationQuit()
    {
        targetList = null;
        Application.quitting -= OnApplicationQuit;
        AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
    }

    private static void OnDomainUnload(object sender, EventArgs e)
    {
        OnApplicationQuit();
    }
}