using System;
using UnityEngine;

[Serializable]
public class GroundMap
{
    [SerializeField] float smoothingRate;
    [SerializeField] float totalGroundedSmoothingRate;
    [SerializeField] float middleGroundedSmoothingRate;
    [SerializeField] int middleGroundedWidth;

    public int numFwdIntervals;
    public float intervalWidth;
    public GroundMapPt[] map;

    public SteadyToggle grounded;
    public SteadyToggle smoothedGrounded;//I think you WILL want this for legSync.FreeHang
    public SteadyToggle stronglyGrounded;
    public SteadyToggle smoothedStronglyGrounded;
    public SteadyToggle middleGrounded;
    public SteadyToggle smoothedMiddleGrounded;

    float middleGroundedCountInverse;
    float totalGroundedCountInverse;

    public int CentralIndex => numFwdIntervals;
    public int NumPts => (numFwdIntervals << 1) | 1;
    public float MapHalfWidth => intervalWidth * numFwdIntervals;
    //public bool AllPointsHitGround => grounded.Brightness == 1f;
    //public bool AnyPointsHitGround => grounded.On;
    public float MiddleGroundedFraction { get; private set; }
    public float SmoothedMiddleGroundedFraction { get; private set; }
    public float TotalGroundedFraction { get; private set; }
    public float SmoothedTotalGroundedFraction { get; private set; }
    public Vector2 LastOrigin { get; private set; }
    public Vector2 LastOriginRight { get; private set; }
    public ref GroundMapPt Center => ref map[CentralIndex];//NOTE: you will modify the array element through this property! it's not a copy!!
    public ref GroundMapPt LeftEndPt => ref map[0];
    public ref GroundMapPt RightEndPt => ref map[^1];

    Color RaycastColor0 => Color.clear;//Color.red;
    Color RaycastColor1 => Color.clear;//Color.blue;
    Color RaycastColor2 => Color.clear;//Color.cyan;

    Color GizmoColorCenter => Color.red;
    Color GizmoColorRight => Color.green;
    Color GizmoColorLeft => Color.yellow;

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

    public bool AnyHitGround()
    {
        for (int i = 0; i < map.Length; i++)
        {
            if (map[i].hitGround)
            {
                return true;
            }
        }

        return false;
    }

    public bool AllHitGroundFromCenter(float maxPosition)
    {
        if (!Center.hitGround)
        {
            return false;
        }

        maxPosition += Mathf.Epsilon;
        var minPosition = -maxPosition - Mathf.Epsilon;
        for (int i = 1; i < numFwdIntervals; i++)
        {
            ref var r = ref map[numFwdIntervals + i];
            if (r.position < maxPosition && !r.hitGround)
            {
                return false;
            }

            ref var l = ref map[numFwdIntervals - i];
            if (l.position > minPosition && l.hitGround)
            {
                return false;
            }

            if (r.position > maxPosition && l.position < minPosition)
            {
                return true;
            }
        }

        return true;
    }

    public bool AnyHitGroundFromCenter(float maxPosition)
    {
        if (Center.hitGround)
        {
            return true;
        }

        maxPosition += Mathf.Epsilon;
        var minPosition = -maxPosition - Mathf.Epsilon;
        for (int i = 1; i < numFwdIntervals; i++)
        {
            ref var r = ref map[numFwdIntervals + i];
            if (r.position < maxPosition && r.hitGround)
            {
                return true;
            }

            ref var l = ref map[numFwdIntervals - i];
            if (l.position > minPosition && l.hitGround)
            {
                return true;
            }

            if (r.position > maxPosition && l.position < minPosition)
            {
                return false;
            }
        }

        return false;
    }

    public int IndexOfFirstGroundHitFromCenter(bool facingRight, out bool isCentralIndex)
    {
        if (grounded.On)
        {
            int i = CentralIndex;
            if (map[i].hitGround)
            {
                isCentralIndex = true;
                return i;
            }

            int di = facingRight ? 1 : -1;
            int n = numFwdIntervals << 1;
            int max = facingRight ? n : 0;

            //search forward first (not necessary - we could alternate front and behind until we get a hit - but i feel like this way is better for continuity)
            while (i != max)
            {
                i += di;
                if (map[i].hitGround)
                {
                    isCentralIndex = false;
                    return i;
                }
            }

            i = CentralIndex;
            di = -di;
            max = facingRight ? 0 : n;

            while (i != max)
            {
                i += di;
                if (map[i].hitGround)
                {
                    isCentralIndex = false;
                    return i;
                }
            }

            //i++;
            //int j = CentralIndex - 1;
            //int n = NumPts;

            //if (facingRight)
            //{ }
            
            //while (i < n && j > 0)
            //{
            //    if (map[i].hitGround)
            //    {
            //        isCentralIndex = false;
            //        return i;
            //    }
            //    if (map[j].hitGround)
            //    {
            //        isCentralIndex = false;
            //        return j;
            //    }

            //    i++;
            //    j--;
            //}
        }

        isCentralIndex = true;
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

    public Vector2 ProjectOntoGroundByArcLength(Vector2 p, out Vector2 normal, out bool hitGround)
    {
        return PointFromCenterByPosition(Vector2.Dot(p - LastOrigin, LastOriginRight), out normal, out hitGround);
        //using lastOrigin rather than center has made things so much smoother (eg. for rounding hard corners)
        //but still project by arc length giving better results than normal projection
    }

    public Vector2 PointFromCenterByPosition(float x, out Vector2 normal, out bool hitGround)
    {
        if (x > 0)
        {
            for (int i = numFwdIntervals; i < map.Length - 1; i++)
            {
                ref var p = ref map[i + 1];
                if (p.position > x)
                {
                    ref var q = ref map[i];
                    var s = p.position - q.position;
                    normal = q.normal;
                    hitGround = q.hitGround;
                    return Vector2.Lerp(q.point, p.point, (x - q.position) / s);
                }
            }

            var t = x - RightEndPt.position;
            normal = RightEndPt.normal;
            hitGround = RightEndPt.hitGround;
            return RightEndPt.point + t * RightEndPt.normal.CWPerp();
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            ref var p = ref map[i - 1];
            if (p.position < x)
            {
                ref var q = ref map[i];
                var s = p.position - q.position;
                normal = q.normal;
                hitGround = q.hitGround;
                return Vector2.Lerp(q.point, p.point, (x - q.position) / s);
            }
        }

        var u = x - LeftEndPt.position;
        normal = LeftEndPt.normal;
        hitGround = LeftEndPt.hitGround;
        return LeftEndPt.point + u * LeftEndPt.normal.CWPerp();
    }

    public void SetToPointFromCenterByPositionClamped(float x, ref GroundMapPt pt)
    {
        if (x > 0)
        {
            for (int i = numFwdIntervals; i < map.Length - 1; i++)
            {
                if (map[i + 1].position > x)
                {
                    var s = map[i + 1].position - map[i].position;
                    pt.Set(Vector2.Lerp(map[i].point, map[i + 1].point, (x - map[i].position) / s), map[i].normal, map[i].right, 0, 0, map[i].groundCollider);
                }
            }
            pt = RightEndPt;
        }

        for (int i = numFwdIntervals; i > 0; i--)
        {
            if (map[i - 1].position < x)
            {
                var s = map[i - 1].position - map[i].position;
                pt.Set(Vector2.Lerp(map[i].point, map[i - 1].point, (x - map[i].position) / s), map[i].normal, map[i].right, 0, 0, map[i].groundCollider);
            }
        }
        pt = LeftEndPt;
    }

    public Vector2 ClosestPoint(Vector2 p, out Vector2 normal, out bool hitGround)
    {
        var a1 = MathTools.Cross2D(p - map[0].point, map[0].normal);
        if (a1 == 0)
        {
            hitGround = map[0].hitGround;
            normal = map[0].normal;
            return map[0].point;
        }
        for (int i = 1; i < NumPts; i++)
        {
            var a2 = MathTools.Cross2D(p - map[i].point, map[i].normal);
            if (a2 == 0)
            {
                hitGround = map[i].hitGround;
                normal = map[i].normal;
                return map[i].point;
            }
            if (MathTools.OppositeSigns(a1, a2))
            {
                var t = Mathf.Abs(a1 / (a2 - a1));
                hitGround = t < 0.5f ? map[i - 1].hitGround : map[i].hitGround;
                normal = MathTools.CheapRotationalLerp(map[i - 1].normal, map[i].normal, t, out _);
                return Vector2.Lerp(map[i - 1].point, map[i].point, t);
            }
            a1 = a2;
        }

        if (Vector2.Dot(p - RightEndPt.point, RightEndPt.right) > 0)
        {
            hitGround = RightEndPt.hitGround;
            normal = RightEndPt.normal;
            return RightEndPt.point;
        }

        hitGround = LeftEndPt.hitGround;
        normal = LeftEndPt.normal;
        return LeftEndPt.point;
    }

    public Vector2 AveragePointFromCenter(float minPos, float maxPos)
    {
        maxPos += Mathf.Epsilon;//so we can use < instead of <= (probably pointless)
        minPos -= Mathf.Epsilon;
        Vector2 sum = default;
        int ct = 0;

        for (int i = 0; i < map.Length; i++)
        {
            if (map[i].position < maxPos && map[i].position > minPos)
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
            if (map[i].position < maxPos && map[i].position > minPos)
            {
                sum += map[i].normal;
                ct++;
            }
        }

        return ct == 0 ? sum : sum / ct;
    }

    public void Initialize()
    {
        map = new GroundMapPt[NumPts];
        middleGroundedCountInverse = 1 / (float)(2 * middleGroundedWidth + 1);
        totalGroundedCountInverse = 1 / (float)NumPts;
    }

    public void InitializeStats()
    {
        int count = 0;
        for (int i = CentralIndex - middleGroundedWidth; i < CentralIndex + middleGroundedWidth + 1; i++)
        {
            if (map[i].hitGround)
            {
                count++;
            }
        }
        MiddleGroundedFraction = count * middleGroundedCountInverse;
        SmoothedMiddleGroundedFraction = MiddleGroundedFraction;

        count = 0;
        for (int i = 0; i < NumPts; i++)
        {
            if (map[i].hitGround)
            {
                count++;
            }
        }
        TotalGroundedFraction = count * totalGroundedCountInverse;
        SmoothedTotalGroundedFraction = TotalGroundedFraction;

        grounded.UpdateState(TotalGroundedFraction);
        smoothedGrounded.UpdateState(SmoothedTotalGroundedFraction);
        middleGrounded.UpdateState(MiddleGroundedFraction);
        smoothedMiddleGrounded.UpdateState(SmoothedMiddleGroundedFraction);
    }

    public void UpdateStats()
    {
        int count = 0;
        for (int i = CentralIndex - middleGroundedWidth; i < CentralIndex + middleGroundedWidth + 1; i++)
        {
            if (map[i].hitGround)
            {
                count++;
            }
        }
        MiddleGroundedFraction = count * middleGroundedCountInverse;
        SmoothedMiddleGroundedFraction = Mathf.Lerp(SmoothedMiddleGroundedFraction, MiddleGroundedFraction, middleGroundedSmoothingRate * Time.deltaTime);

        count = 0;
        for (int i = 0; i < NumPts; i++)
        {
            if (map[i].hitGround)
            {
                count++;
            }
        }
        TotalGroundedFraction = count * totalGroundedCountInverse;
        SmoothedTotalGroundedFraction = Mathf.Lerp(SmoothedTotalGroundedFraction, TotalGroundedFraction, totalGroundedSmoothingRate * Time.deltaTime);

        grounded.UpdateState(TotalGroundedFraction);
        smoothedGrounded.UpdateState(SmoothedTotalGroundedFraction);
        stronglyGrounded.UpdateState(TotalGroundedFraction);
        smoothedStronglyGrounded.UpdateState(SmoothedTotalGroundedFraction);
        middleGrounded.UpdateState(MiddleGroundedFraction);
        smoothedMiddleGrounded.UpdateState(SmoothedMiddleGroundedFraction);
    }

    public void LerpUpdateMap(Vector2 origin, Vector2 originDown, Vector2 originRight, float raycastLength, int centralIndex, int raycastLayerMask)
    {
        int n = numFwdIntervals << 1;//last index of map array
        Vector2 dp = origin - LastOrigin;
        LastOrigin = origin;
        LastOriginRight = originRight;
        //AllPointsHitGround = true;
        //AnyPointsHitGround = false;

        var r = MathTools.DebugRaycast(origin, originDown, raycastLength, raycastLayerMask, RaycastColor0);
        if (r)
        {
            map[centralIndex].Set(r.point, r.normal, r.normal.CWPerp(), 0, r.distance, r.collider);
            //AnyPointsHitGround = true;
        }
        else
        {
            //AllPointsHitGround = false;
            map[centralIndex].Set(origin + raycastLength * originDown, -originDown, originRight, 0, raycastLength, null);
        }

        int i = centralIndex;
        while (i < n)
        {
            //set map[i + 1]
            ref var lastMapPt = ref map[i];
            var lastNormal = lastMapPt.normal;
            var lastRight = lastMapPt.right;
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;
            i++;

            //first raycast horizontally to check if ground kicks UP suddenly (running into a wall e.g.)
            r = MathTools.DebugRaycast(o, lastRight, intervalWidth, raycastLayerMask, RaycastColor1);
            if (r)
            {
                //map[i + 1].LerpTowards(dp, r.point, r.normal, r.normal.CWPerp(), lastMapPt.point, lastMapPt.horizontalPosition, 
                //    true, r.distance, r.collider, smoothingRate); 
                map[i].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position + Vector2.Distance(lastMapPt.point, r.point),
                    r.distance, r.collider);
                //just set in the extreme cases instead of trying to lerp
                //if (!AnyPointsHitGround)
                //{
                //    AnyPointsHitGround = true;
                //}
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastRight;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, RaycastColor2);
                if (r)
                {
                    //if (map[i].hitGround)
                    //{
                    //    map[i].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point),
                    //        r.distance * Vector2.Dot(lastNormal, r.normal), r.collider);
                    //}
                    //else
                    //{
                    //    map[i].LerpTowards(dp, r.point, r.normal, r.normal.CWPerp(), lastMapPt.point, lastMapPt.horizontalPosition,
                    //    true, r.distance * Vector2.Dot(lastNormal, r.normal), r.collider, smoothingRate);
                    //}
                    map[i].LerpTowards(dp, r.point, r.normal, r.normal.CWPerp(), lastMapPt.point, lastMapPt.position,
                        true, r.distance * Vector2.Dot(lastNormal, r.normal), r.collider, smoothingRate);
                    //map[i + 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, r.point),
                    //    r.distance * Vector2.Dot(lastNormal, r.normal), r.collider);
                    //if (!AnyPointsHitGround)
                    //{
                    //    AnyPointsHitGround = true;
                    //}
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastRight, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        //map[i + 1].LerpTowards(dp, r.point, r.normal, r.normal.CWPerp(), lastMapPt.point, lastMapPt.horizontalPosition,
                        //    true, r.distance, r.collider, smoothingRate);
                        map[i].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position + Vector2.Distance(lastMapPt.point, r.point), r.distance, r.collider);

                        //if (!AnyPointsHitGround)
                        //{
                        //    AnyPointsHitGround = true;
                        //}
                    }
                    else
                    {

                        //if (AllPointsHitGround)
                        //{
                        //    AllPointsHitGround = false;
                        //}

                        o -= raycastLength * lastNormal;
                        //map[i + 1].Set(o, lastNormal, lastMapPt.right, lastMapPt.horizontalPosition + Vector2.Distance(lastMapPt.point, o), raycastLength, null);
                        map[i].LerpTowards(dp, o, lastNormal, lastMapPt.right, lastMapPt.point, lastMapPt.position, true, raycastLength, null, smoothingRate);
                    }
                }
            }
        }

        i = centralIndex;
        while (i > 0)
        {
            //set map[i - 1]
            ref var lastMapPt = ref map[i];
            var lastNormal = lastMapPt.normal;
            var lastTangent = -lastMapPt.right;
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;
            i--;

            r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, RaycastColor1);
            if (r)
            {
                map[i].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position - Vector2.Distance(lastMapPt.point, r.point), r.distance, r.collider);
                //map[i - 1].LerpTowards(dp, r.point, r.normal, r.normal.CWPerp(), lastMapPt.point, lastMapPt.horizontalPosition,
                //    false, r.distance, r.collider, smoothingRate);
                //if (!AnyPointsHitGround)
                //{
                //    AnyPointsHitGround = true;
                //}
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, RaycastColor2);
                if (r)
                {
                    //if (map[i].hitGround)
                    //{
                    //    map[i].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.horizontalPosition - Vector2.Distance(lastMapPt.point, r.point),
                    //    r.distance * Vector2.Dot(lastNormal, r.normal), r.collider);
                    //}
                    //else
                    //{
                    //    map[i].LerpTowards(dp, r.point, r.normal, r.normal.CWPerp(), lastMapPt.point, lastMapPt.horizontalPosition,
                    //    false, r.distance * Vector2.Dot(lastNormal, r.normal), r.collider, smoothingRate);
                    //}
                    map[i].LerpTowards(dp, r.point, r.normal, r.normal.CWPerp(), lastMapPt.point, lastMapPt.position,
                        false, r.distance * Vector2.Dot(lastNormal, r.normal), r.collider, smoothingRate);
                    //if (!AnyPointsHitGround)
                    //{
                    //    AnyPointsHitGround = true;
                    //}
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        map[i].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position - Vector2.Distance(lastMapPt.point, r.point),
                            r.distance, r.collider);
                        //map[i - 1].LerpTowards(dp, r.point, r.normal, r.normal.CWPerp(), lastMapPt.point, lastMapPt.horizontalPosition,
                        //    false, r.distance, r.collider, smoothingRate);
                        //if (!AnyPointsHitGround)
                        //{
                        //    AnyPointsHitGround = true;
                        //}
                    }
                    else
                    {
                        //if (AllPointsHitGround)
                        //{
                        //    AllPointsHitGround = false;
                        //}

                        o -= raycastLength * lastNormal;
                        //map[i - 1].Set(o, lastNormal, lastMapPt.right, lastMapPt.horizontalPosition - Vector2.Distance(lastMapPt.point, o), raycastLength, null);
                        map[i].LerpTowards(dp, o, lastNormal, lastMapPt.right, lastMapPt.point, lastMapPt.position, false, raycastLength, null, smoothingRate);
                    }
                }
            }
        }
    }

    //return whether map filled without backtracking
    public void UpdateMap(Vector2 origin, Vector2 originDown, Vector2 originRight, float raycastLength, int centralIndex, int raycastLayerMask)
    {
        int n = numFwdIntervals << 1;//last index of map array
        LastOrigin = origin;
        LastOriginRight = originRight;
        //AllPointsHitGround = true;
        //AnyPointsHitGround = false;

        var r = MathTools.DebugRaycast(origin, originDown, raycastLength, raycastLayerMask, RaycastColor0);
        if (r)
        {
            map[centralIndex].Set(r.point, r.normal, r.normal.CWPerp(), 0, r.distance, r.collider);
            //AnyPointsHitGround = true;
        }
        else
        {
            //AllPointsHitGround = false;
            map[centralIndex].Set(origin + raycastLength * originDown, -originDown, originRight, 0, raycastLength, null);
        }

        for (int i = centralIndex; i < n; i++)
        {
            //set map[i + 1]
            ref var lastMapPt = ref map[i];
            var lastNormal = lastMapPt.normal;
            var lastRight = lastMapPt.right;
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            //first raycast horizontally to check if ground kicks UP suddenly (running into a wall e.g.)
            r = MathTools.DebugRaycast(o, lastRight, intervalWidth, raycastLayerMask, RaycastColor1);
            if (r)
            {
                map[i + 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position + Vector2.Distance(lastMapPt.point, r.point),
                    r.distance, r.collider);
                //if (!AnyPointsHitGround)
                //{
                //    AnyPointsHitGround = true;
                //}
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastRight;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, RaycastColor2);
                if (r)
                {
                    map[i + 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position + Vector2.Distance(lastMapPt.point, r.point),
                        r.distance * Vector2.Dot(lastNormal, r.normal), r.collider);
                    //if (!AnyPointsHitGround)
                    //{
                    //    AnyPointsHitGround = true;
                    //}
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastRight, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        map[i + 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position + Vector2.Distance(lastMapPt.point, r.point), r.distance, r.collider);
                        //if (!AnyPointsHitGround)
                        //{
                        //    AnyPointsHitGround = true;
                        //}
                    }
                    else
                    {

                        //if (AllPointsHitGround)
                        //{
                        //    AllPointsHitGround = false;
                        //}

                        o -= raycastLength * lastNormal;
                        map[i + 1].Set(o, lastNormal, lastMapPt.right, lastMapPt.position + Vector2.Distance(lastMapPt.point, o), raycastLength, null);
                    }
                }
            }
        }

        for (int i = centralIndex; i > 0; i--)
        {
            //set map[i - 1]
            ref var lastMapPt = ref map[i];
            var lastNormal = lastMapPt.normal;
            var lastTangent = -lastMapPt.right;
            var lastRaycastLength = lastMapPt.raycastDistance;
            var o = lastMapPt.point + lastRaycastLength * lastNormal;

            r = MathTools.DebugRaycast(o, lastTangent, intervalWidth, raycastLayerMask, RaycastColor1);
            if (r)
            {
                map[i - 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position - Vector2.Distance(lastMapPt.point, r.point), r.distance, r.collider);
                //if (!AnyPointsHitGround)
                //{
                //    AnyPointsHitGround = true;
                //}
            }
            else
            {
                //now shift forward and raycast down
                o += intervalWidth * lastTangent;
                r = MathTools.DebugRaycast(o, -lastNormal, raycastLength, raycastLayerMask, RaycastColor2);
                if (r)
                {
                    map[i - 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position - Vector2.Distance(lastMapPt.point, r.point),
                        r.distance * Vector2.Dot(lastNormal, r.normal), r.collider);
                    //if (!AnyPointsHitGround)
                    //{
                    //    AnyPointsHitGround = true;
                    //}
                }
                else
                {
                    var l = Mathf.Min(lastRaycastLength + 0.5f * intervalWidth, raycastLength);
                    r = Physics2D.Raycast(o - l * lastNormal, -lastTangent, 2 * intervalWidth, raycastLayerMask);
                    if (r)
                    {
                        map[i - 1].Set(r.point, r.normal, r.normal.CWPerp(), lastMapPt.position - Vector2.Distance(lastMapPt.point, r.point),
                            r.distance, r.collider);
                        //if (!AnyPointsHitGround)
                        //{
                        //    AnyPointsHitGround = true;
                        //}
                    }
                    else
                    {
                        //if (AllPointsHitGround)
                        //{
                        //    AllPointsHitGround = false;
                        //}

                        o -= raycastLength * lastNormal;
                        map[i - 1].Set(o, lastNormal, lastMapPt.right, lastMapPt.position - Vector2.Distance(lastMapPt.point, o), raycastLength, null);
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
public struct GroundMapPt
{
    public float position;
    //public float horizontalPosition;
    public float raycastDistance;
    public Vector2 point;
    public Vector2 normal;
    public Vector2 right;
    public Collider2D groundCollider;

    public bool hitGround => groundCollider != null;

    public GroundMapPt(Vector2 point, Vector2 normal, Vector2 right, float position, float raycastDistance, Collider2D groundCollider)
    {
        this.point = point;
        this.normal = normal;
        this.right = right;
        this.position = position;
        this.raycastDistance = raycastDistance;
        this.groundCollider = groundCollider;
    }

    public void Set(Vector2 point, Vector2 normal, Vector2 right, float position, float raycastDistance, Collider2D groundCollider)
    {
        this.point = point;
        this.normal = normal;
        this.right = right;
        this.position = position;
        this.raycastDistance = raycastDistance;
        this.groundCollider = groundCollider;
    }

    public void LerpTowards(Vector2 translation, Vector2 point, Vector2 normal, Vector2 right, Vector2 prevPoint,
        float prevPointHorizontalPosition, bool incrementingUp, float raycastDistance, Collider2D groundCollider, float lerpParameter)
    {
        this.point += translation;
        this.point = Vector2.Lerp(this.point, point, lerpParameter);
        this.normal = MathTools.CheapRotationalLerpClamped(this.normal, normal, lerpParameter, out _);
        this.right = MathTools.CheapRotationalLerpClamped(this.right, right, lerpParameter, out _);
        position = incrementingUp ? prevPointHorizontalPosition + Vector2.Distance(prevPoint, this.point) : prevPointHorizontalPosition - Vector2.Distance(prevPoint, this.point);
        this.raycastDistance = Mathf.Lerp(this.raycastDistance, raycastDistance, lerpParameter);
        this.groundCollider = groundCollider;
    }
}