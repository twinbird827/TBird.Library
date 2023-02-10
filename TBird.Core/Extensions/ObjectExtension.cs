using System;
using System.Collections.Generic;
using System.Text;

namespace TBird.Core
{
    public static class ObjectExtension
    {
        /// <summary>
        /// 指定したｵﾌﾞｼﾞｪｸﾄがIDisposableを実装しているなら破棄します。
        /// </summary>
        /// <param name="value">ｵﾌﾞｼﾞｪｸﾄ</param>
        public static void TryDispose(this object value)
        {
            if (value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
