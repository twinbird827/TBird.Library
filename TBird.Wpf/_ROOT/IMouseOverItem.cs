using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Wpf
{
    public interface IMouseOverItem
    {
        /// <summary>
        /// ﾏｳｽｵｰﾊﾞｰ中かどうか
        /// </summary>
        bool IsMouseOver { get; set; }
    }
}
