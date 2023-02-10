using System;
using System.Collections.Generic;
using System.Text;

namespace TBird.Core
{
    public static class EnumExtension
    {
        /// <summary>
        /// 列挙値の文字列表現を取得します。
        /// </summary>
        /// <typeparam name="T">列挙値の型</typeparam>
        /// <param name="target">列挙値</param>
        /// <returns></returns>
        public static string GetLabel<T>(this T target) where T : Enum
        {
            var targetName = target.ToString();
            var targetNamespace = target.GetType().FullName;
            var targetLang = $"{targetNamespace.Replace('.', '_')}_{targetName}";
            return Lang.Instance.Get(targetLang) ?? targetName;
        }
    }
}
