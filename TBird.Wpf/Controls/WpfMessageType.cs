using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Wpf.Controls
{
    /// <summary>
    /// ﾀﾞｲｱﾛｸﾞの種類
    /// </summary>
    public enum WpfMessageType
    {
        /// <summary>
        /// 情報ﾀﾞｲｱﾛｸﾞ
        /// </summary>
        Information = 0,

        /// <summary>
        /// 確認ﾀﾞｲｱﾛｸﾞ
        /// </summary>
        Confirm = 1,

        /// <summary>
        /// ｴﾗｰﾀﾞｲｱﾛｸﾞ
        /// </summary>
        Error = 2,
    }
}