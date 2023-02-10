using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace TBird.Core
{
    public static class XmlExtension
    {
        /// <summary>
        /// Xml属性をbool値に変換して取得します。
        /// </summary>
        /// <param name="xml"><see cref="XElement"/>ｲﾝｽﾀﾝｽ</param>
        /// <param name="name">属性の名前</param>
        /// <returns></returns>
        public static bool AttributeB(this XElement xml, XName name, bool def = true)
        {
            var attr = xml.Attribute(name);
            return attr != null ? (bool)attr : def;
        }

        /// <summary>
        /// Xml属性をstring値に変換して取得します。
        /// </summary>
        /// <param name="xml"><see cref="XElement"/>ｲﾝｽﾀﾝｽ</param>
        /// <param name="name">属性の名前</param>
        /// <returns></returns>
        public static string AttributeS(this XElement xml, XName name, string def = null)
        {
            var attr = xml.Attribute(name);
            return attr != null ? (string)attr : def;
        }

        /// <summary>
        /// Xml属性をint値に変換して取得します。
        /// </summary>
        /// <param name="xml"><see cref="XElement"/>ｲﾝｽﾀﾝｽ</param>
        /// <param name="name">属性の名前</param>
        /// <returns></returns>
        public static int AttributeI(this XElement xml, XName name, int def = 0)
        {
            var attr = xml.Attribute(name);
            return attr != null ? (int)attr : def;
        }

        /// <summary>
        /// Xml属性をlong値に変換して取得します。
        /// </summary>
        /// <param name="xml"><see cref="XElement"/>ｲﾝｽﾀﾝｽ</param>
        /// <param name="name">属性の名前</param>
        /// <returns></returns>
        public static long AttributeL(this XElement xml, XName name, long def = 0)
        {
            var attr = xml.Attribute(name);
            return attr != null ? (long)attr : def;
        }

        /// <summary>
        /// Xml属性をlong値に変換して取得します。
        /// </summary>
        /// <param name="xml"><see cref="XElement"/>ｲﾝｽﾀﾝｽ</param>
        /// <param name="name">属性の名前</param>
        /// <returns></returns>
        public static double AttributeD(this XElement xml, XName name, double def = 0)
        {
            var attr = xml.Attribute(name);
            return attr != null ? (double)attr : def;
        }

        /// <summary>
        /// Xml属性を列挙値に変換して取得します。
        /// </summary>
        /// <param name="xml"><see cref="XElement"/>ｲﾝｽﾀﾝｽ</param>
        /// <param name="name">属性の名前</param>
        /// <returns></returns>
        public static T AttributeE<T>(this XElement xml, XName name) where T : struct
        {
            return EnumUtil.ToEnum<T>(xml.AttributeS(name));
        }
    }
}
