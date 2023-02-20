using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Wpf
{
    public partial class BindableBase : ILocker
    {
        /// <summary>
        /// GUID
        /// </summary>
        public string Lock
        {
            get => _Guid = _Guid ?? this.CreateLock4Instance();
        }
        private string _Guid;

        /// <summary>
        /// ｲﾝｽﾀﾝｽの文字列表現を取得します。
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{base.ToString()} {Lock}";
        }

        /// <summary>
        /// ｲﾝｽﾀﾝｽと指定した別のBindableBaseの値が同値か比較します。
        /// </summary>
        /// <param name="obj">比較対象のｲﾝｽﾀﾝｽ</param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return obj is BindableBase bindable && bindable != null
                ? Lock.Equals(bindable.Lock)
                : false;
        }

        /// <summary>
        /// このｲﾝｽﾀﾝｽのﾊｯｼｭｺｰﾄﾞを返却します。
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Lock.GetHashCode();
        }
    }
}
