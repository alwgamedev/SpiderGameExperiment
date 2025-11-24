using UnityEngine;
using UnityEditor.Events;
using UnityEngine.Events;
using System.Collections.Generic;

public static class MiscTools
{
    public static MonobehaviorPathComparer MbPathComparer = new();

    public static void RemoveAllPersistentListeners(this UnityEventBase e)
    {
        int i = e.GetPersistentEventCount();
        while (i-- > 0)
        {
            UnityEventTools.RemovePersistentListener(e, i);
        }
    }

    public class MonobehaviorPathComparer : IComparer<MonoBehaviour>
    {
        public int Compare(MonoBehaviour x, MonoBehaviour y)
        {
            if (!x || !x.gameObject)
            {
                return y ? 1 : 0;
            }
            if (!y || !y.gameObject)
            {
                return -1;
            }

            var m = x.gameObject.name;
            var n = y.gameObject.name;
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