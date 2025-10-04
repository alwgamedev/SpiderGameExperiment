using UnityEngine;
using System;
using UnityEditor.ShaderGraph.Internal;
using Unity.VisualScripting;

[Serializable]
public struct GroundMap
{
    public int numFwdIntervals;
    public float intervalWidth;
    //public LayerMask raycastLayerMask;

    Color RaycastColor0 => Color.clear;//Color.red;
    Color RaycastColor1 => Color.clear;//Color.blue;
    Color RaycastColor2 => Color.clear;//Color.cyan;

    Color GizmoColorCenter => Color.red;
    Color GizmoColorRight => Color.green;
    Color GizmoColorLeft => Color.yellow;

    [SerializeField] GroundMapPt[] map;

    //in future, can cache some of these and make numFwdIntervals and intervalWidth private
    public int CentralIndex => numFwdIntervals;
    public int NumPts => (numFwdIntervals << 1) | 1;
    public float MapHalfWidth => intervalWidth * numFwdIntervals;
    public GroundMapPt Center => map[numFwdIntervals];
    public GroundMapPt LeftEndPt => map[0];
    public GroundMapPt RightEndPt => map[^1];
    public GroundMapPt this[int i]
    {
        get
        {
            if (i >= NumPts)
            {
                return RightEndPt;
            }
            if (i < 0)
            {
                return LeftEndPt;
            }
            return map[i];
        }
    }

    public bool IsCentralIndex(int i)
    {
        return i == numFwdIntervals;
    }

    public bool AllHitGround()
    {
        for (int i = 0; i < map.Length; i++)
        {
            if (!map[i].hitGround)
            {
                return false;
            }
        }

        return true;
    }

    public int IndexOfFirstGroundHitFromCenter()
    {
        int i = CentralIndex;
        if (map[i].hitGround)
        {
            return i;
        }

        i++;
        int j = CentralIndex - 1;
        int n = NumPts;
        while (i < n && j > 0)
        {
            if (map[i].hitGround)
            {
                return i;
            }
            if (map[j].hitGround)
            {
                return j;
            }

            i++;
            j--;
        }

        return CentralIndex;
    }

    public GroundMapPt PointFromCenterByIndex(int i)
    {
        if (i >= numFwdIntervals)
        {
            return RightEndPt;
        }
        if (i <= -numFwdIntervals)
        {
            return LeftEndPt;
        }
        return map[numFwdIntervals + i];
    }

    public Vector2 ProjectOntoGround(Vector2 p, out Vector2 normal)
    {
        var x = Vector2.Dot(p - Center.point, Center.normal.CWPerp());
        return PointFromCenterByPosition(x, out normal);
    }

    public Vector2 PointFromCenterByPosition(float x, out Vector2 normal)
    {
        if (x > 0)
        {
            for (int i = numFwdIntervals; i < map.Length - 1; i++)
            {
                var p = map[i + 1];
                if (p.horizontalPosition > x)
                {
                    var q = map[i];
                    var s = p.horizontalPosition - q.horizontalPosition;
                    normal = q.normal;
                    return Vector2.Lerp(q.point, p.point, (x - q.horizontalPosition) / s);
                }
            }

            var t = x - RightEndPt.horizontalPosition;
            normal = RightEndPt.normal;
            return RightEndPt.point + t * RightEndPt.normal.CWPerp();
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            var p = map[i - 1];
            if (p.horizontalPosition < x)
            {
                var q = map[i];
                var s = p.horizontalPosition - q.horizontalPosition;
                normal = q.normal;
                return Vector2.Lerp(q.point, p.point, (x - q.horizontalPosition) / s);
            }
        }

        var u = x - LeftEndPt.horizontalPosition;
        normal = LeftEndPt.normal;
        return LeftEndPt.point + u * LeftEndPt.normal.CWPerp();
    }

    public int IndexOfLastMarkedPointBeforePosition(float x)
    {
        if (x > 0)
        {
            for (int i = numFwdIntervals + 1; i < map.Length; i++)
            {
                if (map[i].horizontalPosition > x)
                {
                    return i - 1;
                }
            }

            return map.Length - 1;
        }

        for (int i = numFwdIntervals - 1; i > -1; i--)
        {
            if (map[i].horizontalPosition < x)
            {
                return i + 1;
            }
        }

        return 0;
    }

    public Vector2 AveragePointFromCenter(float minPos, float maxPos)
    {
        maxPos += Mathf.Epsilon;//so we can use < instead of <= (probably pointless)
        minPos -= Mathf.Epsilon;
        Vector2 sum = default;
        int ct = 0;

        for (int i = 0; i < map.Length; i++)
        {
            if (map[i].horizontalPosition < maxPos && map[i].horizontalPosition > minPos)
            {
                sum += map[i].point;
                ct++;
            }
        }

        return ct == 0 ? sum : sum / ct;
    }

    public Vector2 AverageNormalFromCenter(float minPos, float maxPos)
    {
        maxPos += Mathf.Epsilon;//so we can use < instead of <= (probably pointless)
        minPos -= Mathf.Epsilon;
        Vector2 sum = default;
        int ct = 0;

        for (int i = 0; i < map.Length; i++)
        {
            if (map[i].horizontalPosition < maxPos && map[i].horizontalPosition > minPos)
            {
                sum += map[i].normal;
                ct++;
            }
        }

        return ct == 0 ? sum : sum / ct;
    }

    public void UpdateMap(Vector2 origin, Vector2 originDown, float raycastLength, int centralIndex, int raycastLayerMask)
    {
        //n = 2 * numFwdPts + 1
        int n = NumPts;
        if (map == null || map.Length != n)
        {
            map = new GroundMapPt[n];
        }

        var r = MathTools.DebugRaycast(origin, originDown, raycastLength, raycastLayerMask, RaycastColor0);
        map[centralIndex] = r ? new GroundMapPt(r.point, r.normal, 0, r.distance, true)
            : new GroundMapPt(origin + raycastLength * originDown, -originDown, 0, raycastLength, false);

        for (int i = centralIndex; i < n - 1; i++)
        {
            //set map[i + 1]
            var lastMapPt = map[i];
            var lastNormal = lastMapPt.normal;
            var lastTangent = lastNormal.CWPerp();
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            //first raycast horizontally to check if ground kicks UP suddenly (running into a wall e.g.)
            r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, RaycastColor1);
            if (r)
            {
                var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point);
                map[i + 1] = new GroundMapPt(r.point, r.normal, h, r.distance, true);
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, RaycastColor2);
                if (r)
                {
                    var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point);
                    map[i + 1] = new GroundMapPt(r.point, r.normal, h, r.distance * Vector2.Dot(lastNormal, r.normal), true);
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point);
                        map[i + 1] = new GroundMapPt(r.point, r.normal, h, r.distance, true);
                    }
                    else
                    {
                        var p = o - raycastLength * lastNormal;
                        var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, p);
                        map[i + 1] = new GroundMapPt(p, lastNormal, h, raycastLength, false);
                    }
                }
            }
        }

        for (int i = centralIndex; i > 0; i--)
        {
            //set map[i - 1]
            var lastMapPt = map[i];
            var lastNormal = lastMapPt.normal;
            var lastTangent = lastNormal.CCWPerp();
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, RaycastColor1);
            if (r)
            {
                var h = lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point);
                map[i - 1] = new GroundMapPt(r.point, r.normal, h, r.distance, true);
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, RaycastColor2);
                if (r)
                {
                    var h = lastMapPt.horizontalPosition - Vector2.Distance(lastMapPt.point, r.point);
                    map[i - 1] = new GroundMapPt(r.point, r.normal, h, r.distance * Vector2.Dot(lastNormal, r.normal), true);
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        var h = lastMapPt.horizontalPosition - Vector2.Distance(lastMapPt.point, r.point);
                        map[i - 1] = new GroundMapPt(r.point, r.normal, h, r.distance, true);
                    }
                    else
                    {
                        var p = o - raycastLength * lastNormal;
                        var h = lastMapPt.horizontalPosition - Vector2.Distance(lastMapPt.point, p);
                        map[i - 1] = new GroundMapPt(p, lastNormal, h, raycastLength, false);
                    }
                }
            }
        }
    }

    public void DrawGizmos()
    {
        if (map == null || map.Length == 0) return;

        Gizmos.color = GizmoColorLeft;
        for (int i = 0; i < numFwdIntervals; i++)
        {
            Gizmos.DrawSphere(map[i].point, 0.1f);
        }
        Gizmos.color = GizmoColorCenter;
        Gizmos.DrawSphere(map[numFwdIntervals].point, 0.1f);
        Gizmos.color = GizmoColorRight;
        for (int i = numFwdIntervals + 1; i < NumPts; i++)
        {
            Gizmos.DrawSphere(map[i].point, 0.1f);
        }
    }
}

[Serializable]//just so i can watch them in the inspector
public readonly struct GroundMapPt
{
    public readonly Vector2 point;
    public readonly Vector2 normal;
    public readonly float horizontalPosition;
    public readonly float raycastDistance;
    public readonly bool hitGround;

    public GroundMapPt(Vector2 point, Vector2 normal, float horizontalPosition, float raycastDistance, bool hitGround)
    {
        this.point = point;
        this.normal = normal;
        this.horizontalPosition = horizontalPosition;
        this.raycastDistance = raycastDistance;
        this.hitGround = hitGround;
    }
}