using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Wpf
{
    public interface IFocusableItem
    {
        /// <summary>
        /// ﾌｫｰｶｽ取得中かどうか
        /// </summary>
        bool IsFocused { get; set; }
    }
}
