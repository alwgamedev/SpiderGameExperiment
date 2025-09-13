using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public struct GroundMap
{
    public int numFwdIntervals;
    public float intervalWidth;
    public float raycastLength;
    public LayerMask raycastLayerMask;

    public GroundMapPt[] map;

    public int NumPts => (numFwdIntervals << 1) | 1;

    //2do: ALSO DEAL WITH CASE WHERE GROUND CUTS *UP* SHARPLY (resulting in raycast origin o being inside collider)
    public void BuildMap(Vector2 origin, Vector2 originDown)
    {
        //n = 2 * numFwdPts + 1
        int n = NumPts;
        if (map == null || map.Length != n)
        {
            map = new GroundMapPt[n];
        }

        var r = Physics2D.Raycast(origin, originDown, raycastLength, raycastLayerMask);
        map[numFwdIntervals] = r ? new GroundMapPt(r.point, r.normal)
            : new GroundMapPt(origin + raycastLength * originDown, -originDown);
        var lastRaycastOrigin = origin;

        for (int i = numFwdIntervals; i < n - 1; i++)
        {
            //set map[i + 1]
            var lastMapPt = map[i];
            var lastNormal = lastMapPt.colliderNormal;
            var lastTangent = lastNormal.CWPerp();
            r = Physics2D.Raycast(lastRaycastOrigin, lastTangent, intervalWidth, raycastLayerMask);
            if (r)
            {
                map[i + 1] = new GroundMapPt(r.point, r.normal);
                //and keep last raycast origin the same
            }
            else
            {
                var o = lastRaycastOrigin + intervalWidth * lastTangent;
                r = Physics2D.Raycast(o, -lastNormal, raycastLength, raycastLayerMask);
                if (r)
                {
                    map[i + 1] = new GroundMapPt(r.point, r.normal);
                    lastRaycastOrigin = o;
                }
                else
                {
                    var oo = o - raycastLength * lastNormal;
                    r = Physics2D.Raycast(oo, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    map[i + 1] = r ? new GroundMapPt(r.point, r.normal) : new GroundMapPt(oo, lastNormal);
                    lastRaycastOrigin = r ? oo : o;
                }
            }
        }

        lastRaycastOrigin = origin;
        for (int i = numFwdIntervals; i > 0; i--)
        {
            //set map[i + 1]
            var lastMapPt = map[i];
            var lastNormal = lastMapPt.colliderNormal;
            var lastTangent = lastNormal.CCWPerp();
            r = Physics2D.Raycast(lastRaycastOrigin, lastTangent, intervalWidth, raycastLayerMask);
            if (r)
            {
                map[i - 1] = new GroundMapPt(r.point, r.normal);
                //and keep last raycast origin the same
            }
            else
            {
                var o = lastRaycastOrigin + intervalWidth * lastTangent;
                r = Physics2D.Raycast(o, -lastNormal, raycastLength, raycastLayerMask);
                if (r)
                {
                    map[i - 1] = new GroundMapPt(r.point, r.normal);
                    lastRaycastOrigin = o;
                }
                else
                {
                    var oo = o - raycastLength * lastNormal;
                    r = Physics2D.Raycast(oo, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    map[i - 1] = r ? new GroundMapPt(r.point, r.normal) : new GroundMapPt(oo, lastNormal);
                    lastRaycastOrigin = r ? oo : o;
                }
            }
        }
    }

    //2DO
    public GroundMapPt GetPointAtPosition(float x, out int lastIndexBeforePosition)
    {
        lastIndexBeforePosition = 0;
        return new();
    }
}

//as a shortcut we will use position along map (of point i) = (i - numFwdIntervals) * intervalWidth
public struct GroundMapPt
{
    public Vector2 point;
    public Vector2 colliderNormal;

    public GroundMapPt(Vector2 point, Vector2 colliderNormal)
    {
        this.point = point;
        this.colliderNormal = colliderNormal;
    }
}