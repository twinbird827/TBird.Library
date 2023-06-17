using System;
using System.Collections.Generic;
using System.Text;

namespace TBird.Core.Stateful
{
    public class AnonymousComparer<T> : IComparer<T>
    {
        private readonly Func<T, T, int> _comparer;
        public AnonymousComparer(Func<T, T, int> comparer)
        {
            _comparer = comparer;
        }

        public int Compare(T x, T y)
        {
            return _comparer(x, y);
        }
    }
}
