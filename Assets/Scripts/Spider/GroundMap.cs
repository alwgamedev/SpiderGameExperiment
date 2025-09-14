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

    public void DrawGizmos()
    {
        if (map == null || map.Length == 0) return;

        Gizmos.color = Color.green;
        for (int i = 0; i < numFwdIntervals; i++)
        {
            Gizmos.DrawSphere(map[i].point, 0.1f);
        }
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(map[numFwdIntervals].point, 0.1f);
        Gizmos.color = Color.yellow;
        for (int i = numFwdIntervals + 1; i < NumPts; i++)
        {
            Gizmos.DrawSphere(map[i].point, 0.1f);
        }
    }

    //2do: ALSO DEAL WITH CASE WHERE GROUND CUTS *UP* SHARPLY (resulting in raycast origin o being inside collider)
    public void BuildMap(Vector2 origin, Vector2 originDown)
    {
        //n = 2 * numFwdPts + 1
        int n = NumPts;
        if (map == null || map.Length != n)
        {
            map = new GroundMapPt[n];
        }

        var r = MathTools.DebugRaycast(origin, originDown, raycastLength, raycastLayerMask, Color.red);
        map[numFwdIntervals] = r ? new GroundMapPt(r.point, r.normal, r.distance)
            : new GroundMapPt(origin + raycastLength * originDown, -originDown, raycastLength);
        //var lastRaycastOrigin = origin;
        //float firstRaycastLength = r ? r.distance : raycastLength;
        //float lastRaycastLength = firstRaycastLength;

        for (int i = numFwdIntervals; i < n - 1; i++)
        {
            //set map[i + 1]
            var lastMapPt = map[i];
            var lastNormal = lastMapPt.normal;
            var lastTangent = lastNormal.CWPerp();
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            //first raycast horizontally to check if ground kicks UP suddenly (running into a wall e.g.)
            r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, Color.blue);
            if (r)
            {
                map[i + 1] = new GroundMapPt(r.point, r.normal, r.distance);
                Debug.Log($"hit wall at index {i + 1}");
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, Color.cyan);
                if (r)
                {
                    map[i + 1] = new GroundMapPt(r.point, r.normal, r.distance * Vector2.Dot(lastNormal, r.normal));
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + intervalWidth, raycastLength);
                    var oo = o - l * lastNormal;
                    r = Physics2D.Raycast(oo, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    map[i + 1] = r ? new GroundMapPt(r.point, r.normal, r.distance) : new GroundMapPt(o - raycastLength * lastNormal, lastNormal, raycastLength);
                }
            }
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            //set map[i - 1]
            var lastMapPt = map[i];
            var lastNormal = lastMapPt.normal;
            var lastTangent = lastNormal.CCWPerp();
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, Color.blue);
            if (r)
            {
                map[i - 1] = new GroundMapPt(r.point, r.normal, r.distance);
            }
            else
            {
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, Color.cyan);
                if (r)
                {
                    map[i - 1] = new GroundMapPt(r.point, r.normal, r.distance * Vector2.Dot(lastNormal, r.normal));
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + intervalWidth, raycastLength);
                    var oo = o - l * lastNormal;
                    r = Physics2D.Raycast(oo, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    map[i - 1] = r ? new GroundMapPt(r.point, r.normal, r.distance) : new GroundMapPt(o - raycastLength * lastNormal, lastNormal, raycastLength);
                }
            }
        }
    }

    //2DO
    //public GroundMapPt GetPointAtPosition(float x, out int lastIndexBeforePosition)
    //{
    //    lastIndexBeforePosition = 0;
    //    return new();
    //}
}

[Serializable]//just so i can watch them in the inspector
public struct GroundMapPt
{
    public Vector2 point;
    public Vector2 normal;
    public float raycastDistance;

    public GroundMapPt(Vector2 point, Vector2 normal, float raycastDistance)
    {
        this.point = point;
        this.normal = normal;
        this.raycastDistance = raycastDistance;
    }
}