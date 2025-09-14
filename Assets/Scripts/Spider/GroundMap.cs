using UnityEngine;
using System;

[Serializable]
public struct GroundMap
{
    public int numFwdIntervals;
    public float intervalWidth;
    public LayerMask raycastLayerMask;

    [SerializeField] GroundMapPt[] _map;

    //public GroundMapPt[] map => _map;
    //in future, can cache some of these and make numFwdIntervals and intervalWidth private
    public int NumPts => (numFwdIntervals << 1) | 1;
    public float MapHalfWidth => intervalWidth * numFwdIntervals;
    public GroundMapPt Center => _map[numFwdIntervals];
    public GroundMapPt LeftEndPt => _map[0];
    public GroundMapPt RightEndPt => _map[^1];

    public GroundMapPt PointFromCenterByIndex(int i)
    {
        return _map[numFwdIntervals + i];
    }

    public Vector2 PointFromCenterByPosition(float x)
    {
        if (x < 0)
        {
            int nn = -numFwdIntervals;
            int i = Mathf.Max(-(int)(-x / intervalWidth), nn);
            float dx = (x - i * intervalWidth) / intervalWidth;
            if (i == nn)
            {
                return LeftEndPt.point + dx * LeftEndPt.normal.CWPerp();
            }
            return Vector2.Lerp(PointFromCenterByIndex(i).point, PointFromCenterByIndex(i - 1).point, dx);
        }
        else
        {
            int i = Mathf.Min((int)(x / intervalWidth), numFwdIntervals);
            float dx = (x - i * intervalWidth) / intervalWidth;
            if (i == numFwdIntervals)
            {
                return RightEndPt.point + dx * RightEndPt.normal.CWPerp();
            }
            return Vector2.Lerp(PointFromCenterByIndex(i).point, PointFromCenterByIndex(i + 1).point, dx);
        }
    }

    //it doesn't really "project" but travels an appropriate distance along the ground

    public Vector2 ProjectOntoGround(Vector2 p)
    {
        var x = Vector2.Dot(p - Center.point, Center.normal.CWPerp());
        return PointFromCenterByPosition(x);
    }

    public void UpdateMap(Vector2 origin, Vector2 originDown, float raycastLength)
    {
        //n = 2 * numFwdPts + 1
        int n = NumPts;
        if (_map == null || _map.Length != n)
        {
            _map = new GroundMapPt[n];
        }

        var r = MathTools.DebugRaycast(origin, originDown, raycastLength, raycastLayerMask, Color.red);
        _map[numFwdIntervals] = r ? new GroundMapPt(r.point, r.normal, r.distance, true)
            : new GroundMapPt(origin + raycastLength * originDown, -originDown, raycastLength, false);

        for (int i = numFwdIntervals; i < n - 1; i++)
        {
            //set map[i + 1]
            var lastMapPt = _map[i];
            var lastNormal = lastMapPt.normal;
            var lastTangent = lastNormal.CWPerp();
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            //first raycast horizontally to check if ground kicks UP suddenly (running into a wall e.g.)
            r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, Color.blue);
            if (r)
            {
                _map[i + 1] = new GroundMapPt(r.point, r.normal, r.distance, true);
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, Color.cyan);
                if (r)
                {
                    _map[i + 1] = new GroundMapPt(r.point, r.normal, r.distance * Vector2.Dot(lastNormal, r.normal), true);
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    _map[i + 1] = r ? new GroundMapPt(r.point, r.normal, r.distance, true) : new GroundMapPt(o - raycastLength * lastNormal, lastNormal, raycastLength, false);
                }
            }
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            //set map[i - 1]
            var lastMapPt = _map[i];
            var lastNormal = lastMapPt.normal;
            var lastTangent = lastNormal.CCWPerp();
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, Color.blue);
            if (r)
            {
                _map[i - 1] = new GroundMapPt(r.point, r.normal, r.distance, true);
            }
            else
            {
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, Color.cyan);
                if (r)
                {
                    _map[i - 1] = new GroundMapPt(r.point, r.normal, r.distance * Vector2.Dot(lastNormal, r.normal), true);
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    _map[i - 1] = r ? new GroundMapPt(r.point, r.normal, r.distance, true) : new GroundMapPt(o - raycastLength * lastNormal, lastNormal, raycastLength, false);
                }
            }
        }
    }

    public void DrawGizmos()
    {
        if (_map == null || _map.Length == 0) return;

        Gizmos.color = Color.green;
        for (int i = 0; i < numFwdIntervals; i++)
        {
            Gizmos.DrawSphere(_map[i].point, 0.1f);
        }
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(_map[numFwdIntervals].point, 0.1f);
        Gizmos.color = Color.yellow;
        for (int i = numFwdIntervals + 1; i < NumPts; i++)
        {
            Gizmos.DrawSphere(_map[i].point, 0.1f);
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