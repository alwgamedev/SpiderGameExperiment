using System.Collections.Generic;
using UnityEditor.Events;
using UnityEngine.Events;

public static class MiscTools
{
    public static void RemoveAllPersistentListeners(this UnityEventBase e)
    {
        int i = e.GetPersistentEventCount();
        while (i-- > 0)
        {
            UnityEventTools.RemovePersistentListener(e, i);
        }
    }
}