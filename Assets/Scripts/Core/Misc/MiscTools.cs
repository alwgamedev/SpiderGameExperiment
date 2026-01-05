using UnityEngine;
//using UnityEngine.Events;
using System.Collections.Generic;

public static class MiscTools
{
    public static GameObjectPathComparer GOPathComparer = new();

    public static int Stride<T>()
    {
        return System.Runtime.InteropServices.Marshal.SizeOf<T>();
    }

    public static int ComponentPathCompare(Component x, Component y)
    {
        return GOPathComparer.Compare(x ? x.gameObject : null, y ? y.gameObject : null);
    }

    public class GameObjectPathComparer : IComparer<GameObject>
    {
        public int Compare(GameObject x, GameObject y)
        {
            if (!x)
            {
                return y ? 1 : 0;
            }
            if (!y)
            {
                return -1;
            }

            var m = x.name;
            var n = y.name;
            var s = x.transform.parent;
            var t = y.transform.parent;

            while (m == n && (s || t))
            {
                if (s)
                {
                    m += s.name;
                    s = s.parent;
                }
                if (t)
                {
                    n += t.name;
                    t = t.parent;
                }
            }

            return m.CompareTo(n);
        }
    }
}