using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Core
{
    public interface ILocker : IDisposable
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
    }
}