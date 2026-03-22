using System;
using Unity.Collections;

public static partial class MiscTools
{
    public struct AlwaysAccessibleNativeReference<T> : IDisposable where T : unmanaged
    {
        /// <summary>
        /// Use native at your own risk.
        /// </summary>
        public NativeReference<T> native;
        T snapshot;
        bool locked;

        public bool Locked
        {
            readonly get => locked;
            set
            {
                if (value != locked)
                {
                    locked = value;
                    if (locked)
                    {
                        snapshot = native.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Writes made while locked are temporary.
        /// </summary>
        public T Value
        {
            get => locked ? snapshot : native.Value;
            set
            {
                if (locked)
                {
                    snapshot = value;
                }
                else
                {
                    native.Value = value;
                }
            }
        }

        public AlwaysAccessibleNativeReference(Allocator allocator, bool locked = false)
        {
            native = new NativeReference<T>(allocator);
            snapshot = native.Value;
            this.locked = locked;
        }

        public AlwaysAccessibleNativeReference(T value, Allocator allocator, bool locked = false)
        {
            native = new NativeReference<T>(value, allocator);
            snapshot = native.Value;
            this.locked = locked;
        }

        public void Dispose()
        {
            if (native.IsCreated)
            {
                native.Dispose();
            }
        }
    }
}