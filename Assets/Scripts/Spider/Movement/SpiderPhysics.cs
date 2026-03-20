using System;
using UnityEngine;
using Unity.U2D.Physics;


[Serializable]
public class SpiderPhysics
{
    public PhysicsBody physicsBody;
    public PhysicsShape physicsShape;
    public PhysicsQuery.QueryFilter queryFilter;

    [SerializeField] PhysicsBodyDefinition bodyDef;
    [SerializeField] PhysicsShapeDefinition shapeDef;
    [Range(0,0.5f)][SerializeField] float capsuleCenter;//0 - 0.5 for where first center is positioned
    [SerializeField] Transform spiderTransform;
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform headBone;
    [SerializeField] Transform headBoneEndpt;
    [SerializeField] Transform heightReferencePoint;//just for initialization; cache position in local space
    

    public void OnValidate()
    {
        if (physicsBody.isValid)
        {
            physicsBody.SetBodyDefLive(bodyDef);
            physicsBody.SetShapeDef(shapeDef);
            queryFilter = shapeDef.contactFilter.ToQueryFilter(queryFilter.ignoreFilter);
        }
    }

    public void CreatePhysicsBody()
    {
        if (physicsBody.isValid)
        {
            physicsBody.Destroy();
        }

        bodyDef.position = spiderTransform.position;
        bodyDef.rotation = new PhysicsRotate(spiderTransform.rotation, PhysicsWorld.TransformPlane.XY);

        queryFilter = shapeDef.contactFilter.ToQueryFilter(queryFilter.ignoreFilter);

        physicsBody = PhysicsWorld.defaultWorld.CreateBody(bodyDef);

        var centerOfMass = physicsBody.transform.InverseTransformPoint(heightReferencePoint.position);
        var e0 = physicsBody.transform.InverseTransformPoint(abdomenBone.position);
        var e1 = physicsBody.transform.InverseTransformPoint(headBoneEndpt.position);
        var r = capsuleCenter * (e1.x - e0.x);
        var c1 = new Vector2(e0.x + r, centerOfMass.y);
        var c2 = new Vector2(e1.x - r, centerOfMass.y);
        var capsule = new CapsuleGeometry()
        {
            center1 = c1,
            center2 = c2,
            radius = r
        };

        physicsShape = physicsBody.CreateShape(capsule, shapeDef);

        var massConfig = physicsBody.massConfiguration;
        massConfig.center = centerOfMass;
        physicsBody.massConfiguration = massConfig;

        physicsBody.transformObject = spiderTransform;
    }

    public bool HasContact()
    {
        return physicsBody.GetContacts().Length > 0;
        //never includes triggers (triggers do not generate contacts)
        //+ temp allocated native array doesn't need disposal
    }
}