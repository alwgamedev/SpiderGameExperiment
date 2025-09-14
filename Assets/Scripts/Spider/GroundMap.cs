using UnityEngine;
using System;

[Serializable]
public struct GroundMap
{
    public int numFwdIntervals;
    public float intervalWidth;
    //public float raycastLength;
    public LayerMask raycastLayerMask;

    public GroundMapPt[] map;

    public int NumPts => (numFwdIntervals << 1) | 1;
    public GroundMapPt Center => map[numFwdIntervals];
    public GroundMapPt LeftEndPt => map[0];
    public GroundMapPt RightEndPt => map[^1];

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
    public void UpdateMap(Vector2 origin, Vector2 originDown, float raycastLength)
    {
        //n = 2 * numFwdPts + 1
        int n = NumPts;
        if (map == null || map.Length != n)
        {
            map = new GroundMapPt[n];
        }

        var r = MathTools.DebugRaycast(origin, originDown, raycastLength, raycastLayerMask, Color.red);
        map[numFwdIntervals] = r ? new GroundMapPt(r.point, r.normal, r.distance, true)
            : new GroundMapPt(origin + raycastLength * originDown, -originDown, raycastLength, false);

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
                map[i + 1] = new GroundMapPt(r.point, r.normal, r.distance, true);
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, Color.cyan);
                if (r)
                {
                    map[i + 1] = new GroundMapPt(r.point, r.normal, r.distance * Vector2.Dot(lastNormal, r.normal), true);
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    map[i + 1] = r ? new GroundMapPt(r.point, r.normal, r.distance, true) : new GroundMapPt(o - raycastLength * lastNormal, lastNormal, raycastLength, false);
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
                map[i - 1] = new GroundMapPt(r.point, r.normal, r.distance, true);
            }
            else
            {
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, Color.cyan);
                if (r)
                {
                    map[i - 1] = new GroundMapPt(r.point, r.normal, r.distance * Vector2.Dot(lastNormal, r.normal), true);
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    map[i - 1] = r ? new GroundMapPt(r.point, r.normal, r.distance, true) : new GroundMapPt(o - raycastLength * lastNormal, lastNormal, raycastLength, false);
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
    public readonly Vector2 point;
    public readonly Vector2 normal;
    public readonly float raycastDistance;
    public readonly bool hitGround;

    public GroundMapPt(Vector2 point, Vector2 normal, float raycastDistance, bool hitGround)
    {
        this.point = point;
        this.normal = normal;
        this.raycastDistance = raycastDistance;
        this.hitGround = hitGround;
    }
}