using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace TBird.DB
{
    public static class DbDataReaderExtension
    {
        /// <summary>
        /// DbDataReaderから指定したｲﾝﾃﾞｯｸｽにある値を指定した型で取得します。
        /// </summary>
        /// <typeparam name="T">型</typeparam>
        /// <param name="reader">DbDataReader</param>
        /// <param name="index">ｲﾝﾃﾞｯｸｽ</param>
        /// <returns></returns>
        public static T Get<T>(this DbDataReader reader, int index)
        {
            return DbUtil.GetValue<T>(reader.GetValue(index));
        }
    }
}
