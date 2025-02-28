using System.Collections.Generic;

namespace TBird.Core
{
    public static class ICollectionExtension
    {
        /// <summary>
        /// ﾘｽﾄに配列を追加します。
        /// </summary>
        /// <typeparam name="T">ﾘｽﾄの型</typeparam>
        /// <param name="collection">配列を追加するﾘｽﾄ</param>
        /// <param name="array">ﾘｽﾄに追加する配列</param>
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> array)
        {
            if (array == null) return;

            foreach (var x in array)
            {
                collection.Add(x);
            }
        }
    }
}