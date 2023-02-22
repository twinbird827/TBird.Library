using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Core
{
    public interface ILocker
    {
        string Lock { get; }
    }

    public static class ILockerExtension
    {
        public static string CreateLock4Instance(this ILocker x)
        {
            return Guid.NewGuid().ToString();
        }

        public static string CreateLock4Class(this ILocker x)
        {
            return x.GetType().FullName;
        }

        public static Task WaitAsync(this ILocker x)
        {
            return Locker.WaitAsync(x.Lock);
        }

        public static int Release(this ILocker x)
        {
            return Locker.Release(x.Lock);
        }

        public static int LockCount(this ILocker x)
        {
            return Locker.Count(x.Lock);
        }

        public static Task<IDisposable> LockAsync(this ILocker x)
        {
            return Locker.LockAsync(x.Lock);
        }

        public static IDisposable Lock(this ILocker x)
        {
            return Locker.Lock(x.Lock);
        }

        public static void WaitRelease(this ILocker x)
        {
            if (0 < x.LockCount()) using (x.Lock()) { }
        }

        public static async Task WaitReleaseAsync(this ILocker x)
        {
            if (0 < x.LockCount()) using (await x.LockAsync()) { }
        }
    }
}
