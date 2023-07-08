using System;
using System.Collections.Generic;
using System.Text;

namespace TBird.Core
{
    public class Win32ShowWindowStates
    {
        /// <summary>ｳｨﾝﾄﾞｳを非表示にし、他のｳｨﾝﾄﾞｳをｱｸﾃｨﾌﾞにします。</summary>
        public const int SW_HIDE = 0;

        /// <summary>ｳｨﾝﾄﾞｳをｱｸﾃｨﾌﾞにして表示します。ｳｨﾝﾄﾞｳが最小化または最大化されていた場合は、その位置とｻｲｽﾞを元に戻します。</summary>
        public const int SW_SHOWNORMAL = 1;

        /// <summary>ｳｨﾝﾄﾞｳをｱｸﾃｨﾌﾞにして、最小化します。</summary>
        public const int SW_SHOWMINIMIZED = 2;

        /// <summary>ｳｨﾝﾄﾞｳをｱｸﾃｨﾌﾞにして、最大化します。</summary>
        public const int SW_SHOWMAXIMIZED = 3;

        /// <summary>ｳｨﾝﾄﾞｳを最大化します。</summary>
        public const int SW_MAXIMIZE = 3;

        /// <summary>ｳｨﾝﾄﾞｳを直前の位置とｻｲｽﾞで表示します。</summary>
        public const int SW_SHOWNOACTIVATE = 4;

        /// <summary>ｳｨﾝﾄﾞｳをｱｸﾃｨﾌﾞにして、現在の位置とｻｲｽﾞで表示します。</summary>
        public const int SW_SHOW = 5;

        /// <summary>ｳｨﾝﾄﾞｳを最小化し、Z ｵｰﾀﾞｰが次のﾄｯﾌﾟﾚﾍﾞﾙｳｨﾝﾄﾞｳをｱｸﾃｨﾌﾞにします。</summary>
        public const int SW_MINIMIZE = 6;

        /// <summary>ｳｨﾝﾄﾞｳを最小化します。(ｱｸﾃｨﾌﾞにはしない)</summary>
        public const int SW_SHOWMINNOACTIVE = 7;

        /// <summary>ｳｨﾝﾄﾞｳを現在のｻｲｽﾞと位置で表示します。(ｱｸﾃｨﾌﾞにはしない)</summary>
        public const int SW_SHOWNA = 8;

        /// <summary>ｳｨﾝﾄﾞｳをｱｸﾃｨﾌﾞにして表示します。最小化または最大化されていたｳｨﾝﾄﾞｳは、元の位置とｻｲｽﾞに戻ります。</summary>
        public const int SW_RESTORE = 9;

        /// <summary>ｱﾌﾟﾘｹｰｼｮﾝを起動したﾌﾟﾛｸﾞﾗﾑが 関数に渡した 構造体で指定された SW_ ﾌﾗｸﾞに従って表示状態を設定します。</summary>
        public const int SW_SHOWDEFAULT = 10;

        /// <summary>たとえｳｨﾝﾄﾞｳを所有するｽﾚｯﾄﾞがﾊﾝｸﾞしていても、ｳｨﾝﾄﾞｳを最小化します。このﾌﾗｸﾞは、ほかのｽﾚｯﾄﾞのｳｨﾝﾄﾞｳを最小化する場合にだけ使用してください。</summary>
        public const int SW_FORCEMINIMIZE = 11;
    }
}