using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Wpf
{
    public partial class BindableBase
    {
        /// <summary>
        /// GUID
        /// </summary>
        protected string Guid
        {
            get => _Guid = _Guid ?? System.Guid.NewGuid().ToString();
        }
        private string _Guid;

        /// <summary>
        /// ｲﾝｽﾀﾝｽの文字列表現を取得します。
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{base.ToString()} {Guid}";
        }

        /// <summary>
        /// ｲﾝｽﾀﾝｽと指定した別のBindableBaseの値が同値か比較します。
        /// </summary>
        /// <param name="obj">比較対象のｲﾝｽﾀﾝｽ</param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return obj is BindableBase bindable && bindable != null
                ? Guid.Equals(bindable.Guid)
                : false;
        }

        /// <summary>
        /// このｲﾝｽﾀﾝｽのﾊｯｼｭｺｰﾄﾞを返却します。
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Guid.GetHashCode();
        }
    }
}
