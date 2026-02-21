using System;
using UnityEngine;

[Serializable]
public class SpiderPostureControl
{
    [SerializeField] Transform abdomenBone;
    [SerializeField] Transform headBone;
    [SerializeField] Transform headEndPt;
    [SerializeField] float headPreferredHeight;
    [SerializeField] float abdomenPreferredHeight;
    [SerializeField] float postureLerpSpeed;
    [SerializeField]

    float abdomenBoneLength;
    float abdomenBoneInverseLength;
    float headBoneLength;
    float headBoneInverseLength;

    public void Initialize()
    {
        abdomenBoneLength = Vector2.Distance(abdomenBone.position, headBone.position);
        abdomenBoneInverseLength = 1 / abdomenBoneLength;
        headBoneLength = Vector2.Distance(headBone.position, headEndPt.position);
        headBoneInverseLength = 1 / headBoneLength;
    }

    public void CacheBaseRotations(Transform spider)
    {

    }

    public void UpdatePosture(float dt, GroundMap groundMap)
    {
        Vector2 headMidPt = 0.5f * (headBone.position + headEndPt.position);
        var headClosestGroundPt = groundMap.TrueClosestPoint(headMidPt, out _, out var n, out _);

        Vector2 abdomenMidPt = 0.5f * (abdomenBone.position + headBone.position);
        var abdomenClosestGroundPt = groundMap.TrueClosestPoint(abdomenMidPt, out _, out _, out _);
    }
}