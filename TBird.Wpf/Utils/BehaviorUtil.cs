using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TBird.Wpf
{
    public static class BehaviorUtil
    {
        /// <summary>
        /// 添付ﾌﾟﾛﾊﾟﾃｨを登録します。
        /// </summary>
        /// <typeparam name="T">添付ﾌﾟﾛﾊﾟﾃｨのﾃﾞｰﾀ型</typeparam>
        /// <param name="name">名前</param>
        /// <param name="owner">添付ﾌﾟﾛﾊﾟﾃｨを保持するｸﾗｽの型</param>
        /// <param name="defaultValue">ﾃﾞﾌｫﾙﾄ値</param>
        /// <param name="callback">値が変更された際に呼ばれる処理</param>
        /// <param name="bindingOption">添付ﾌﾟﾛﾊﾟﾃｨのﾊﾞｲﾝﾃﾞｨﾝｸﾞ方法</param>
        /// <returns></returns>
        public static DependencyProperty RegisterAttached<T>(
                    string name, Type owner, T defaultValue, FrameworkPropertyMetadataOptions bindingOption, PropertyChangedCallback callback = null, ValidateValueCallback validate = null
            )
        {
            return DependencyProperty.RegisterAttached(
                name, typeof(T), owner, new FrameworkPropertyMetadata(defaultValue, bindingOption, callback), validate
            );
        }

        /// <summary>
        /// 添付ﾌﾟﾛﾊﾟﾃｨを登録します。
        /// </summary>
        /// <typeparam name="T">添付ﾌﾟﾛﾊﾟﾃｨのﾃﾞｰﾀ型</typeparam>
        /// <param name="name">名前</param>
        /// <param name="owner">添付ﾌﾟﾛﾊﾟﾃｨを保持するｸﾗｽの型</param>
        /// <param name="defaultValue">ﾃﾞﾌｫﾙﾄ値</param>
        /// <param name="callback">値が変更された際に呼ばれる処理</param>
        /// <returns></returns>
        public static DependencyProperty RegisterAttached<T>(
                    string name, Type owner, T defaultValue, PropertyChangedCallback callback = null, ValidateValueCallback validate = null
            )
        {
            return RegisterAttached(
                name, owner, defaultValue, FrameworkPropertyMetadataOptions.None, callback, validate
            );
        }

        /// <summary>
        /// 依存関係ﾌﾟﾛﾊﾟﾃｨを登録します。
        /// </summary>
        /// <typeparam name="T">依存関係ﾌﾟﾛﾊﾟﾃｨのﾃﾞｰﾀ型</typeparam>
        /// <param name="name">名前</param>
        /// <param name="owner">依存関係ﾌﾟﾛﾊﾟﾃｨを保持するｸﾗｽの型</param>
        /// <param name="defaultValue">ﾃﾞﾌｫﾙﾄ値</param>
        /// <param name="bindingOption">依存関係ﾌﾟﾛﾊﾟﾃｨのﾊﾞｲﾝﾃﾞｨﾝｸﾞ方法</param>
        /// <param name="callback">値変更時の処理</param>
        /// <param name="validate">値変更時の検証処理</param>
        /// <returns></returns>
        public static DependencyProperty Register<T>(
                    string name, Type owner, T defaultValue, FrameworkPropertyMetadataOptions bindingOption, PropertyChangedCallback callback = null, ValidateValueCallback validate = null
            )
        {
            return DependencyProperty.Register(
                name, typeof(T), owner, new FrameworkPropertyMetadata(defaultValue, bindingOption, callback), validate
            );
        }

        /// <summary>
        /// 依存関係ﾌﾟﾛﾊﾟﾃｨを登録します。
        /// </summary>
        /// <typeparam name="T">依存関係ﾌﾟﾛﾊﾟﾃｨのﾃﾞｰﾀ型</typeparam>
        /// <param name="name">名前</param>
        /// <param name="owner">依存関係ﾌﾟﾛﾊﾟﾃｨを保持するｸﾗｽの型</param>
        /// <param name="defaultValue">ﾃﾞﾌｫﾙﾄ値</param>
        /// <param name="bindingOption">依存関係ﾌﾟﾛﾊﾟﾃｨのﾊﾞｲﾝﾃﾞｨﾝｸﾞ方法</param>
        /// <param name="callback">値変更時の処理</param>
        /// <param name="validate">値変更時の検証処理</param>
        /// <returns></returns>
        public static DependencyProperty Register<T>(
                    string name, Type owner, T defaultValue, PropertyChangedCallback callback = null, ValidateValueCallback validate = null
            )
        {
            return Register(
                    name, owner, defaultValue, FrameworkPropertyMetadataOptions.None, callback, validate
            );
        }

        /// <summary>
        /// 指定したｵﾌﾞｼﾞｪｸﾄにｲﾍﾞﾝﾄを追加します。
        /// </summary>
        /// <typeparam name="T">ｵﾌﾞｼﾞｪｸﾄの型</typeparam>
        /// <param name="element">ｵﾌﾞｼﾞｪｸﾄ</param>
        /// <param name="add">ｲﾍﾞﾝﾄを追加するためのｱｸｼｮﾝ</param>
        /// <param name="del">ｲﾍﾞﾝﾄを削除するためのｱｸｼｮﾝ</param>
        public static void SetEventHandler<T>(T element, Action<T> add, Action<T> del) where T : FrameworkElement
        {
            if (element == null || add == null || del == null)
            {
                return;
            }

            // ｲﾍﾞﾝﾄを削除してから追加
            del(element);
            add(element);

            Unloaded(element, (sender, e) =>
            {
                if (sender is FrameworkElement fe)
                {
                    del((T)fe);
                }
                add = null;
                del = null;
            });
        }

        /// <summary>
        /// ｵﾌﾞｼﾞｪｸﾄ読込時の処理を実行します。
        /// </summary>
        /// <param name="element">対象ｵﾌﾞｼﾞｪｸﾄ</param>
        /// <param name="handler">読込時処理</param>
        public static void Loaded(FrameworkElement element, RoutedEventHandler handler)
        {
            if (WpfUtil.IsDesignMode()) return;

            // ﾛｰﾄﾞｲﾍﾞﾝﾄ追加orﾛｰﾄﾞ済みの場合は直接実行
            element.Dispatcher.BeginInvoke(
                new Action(() => handler(element, new RoutedEventArgs())),
                DispatcherPriority.Loaded
            );
        }

        public static void Unloaded(FrameworkElement element, RoutedEventHandler handler)
        {
            RoutedEventHandler unloaded = null;

            unloaded = (sender, e) =>
            {
                if (sender is FrameworkElement fe)
                {
                    fe.Unloaded -= handler;
                    fe.Unloaded -= unloaded;
                    unloaded = null;
                }
            };

            element.Unloaded -= handler;
            element.Unloaded += handler;
            element.Unloaded -= unloaded;
            element.Unloaded += unloaded;
        }

        /// <summary>
        /// ScrollViewerを取得します。
        /// </summary>
        /// <param name="target">取得元のｲﾝｽﾀﾝｽ</param>
        public static ScrollViewer GetScrollViewer(DependencyObject target)
        {
            if (target == null)
            {
                return null;
            }
            else if (target is ScrollViewer)
            {
                return (ScrollViewer)target;
            }
            else if (VisualTreeHelper.GetChildrenCount(target) == 0)
            {
                return null;
            }

            var child = VisualTreeHelper.GetChild(target, 0) as DependencyObject;

            if (child == null) return null;

            if (child is ScrollViewer)
            {
                return (ScrollViewer)child;
            }

            return GetScrollViewer(child);
        }

        /// <summary>
        /// 指定した <see cref="DependencyObject"/> の子孫のうち、指定された型のｵﾌﾞｼﾞｪｸﾄを列挙します。
        /// </summary>
        /// <typeparam name="T">列挙する型</typeparam>
        /// <param name="obj">DependencyObject</param>
        /// <returns>指定した型に一致する子孫ｵﾌﾞｼﾞｪｸﾄ</returns>
        public static IEnumerable<T> EnumerateDescendantObjects<T>(DependencyObject obj) where T : DependencyObject
        {
            foreach (var child in LogicalTreeHelper.GetChildren(obj))
            {
                if (child is T cobj)
                {
                    yield return cobj;
                }
                if (child is DependencyObject dobj)
                {
                    foreach (var cobj2 in EnumerateDescendantObjects<T>(dobj))
                    {
                        yield return cobj2;
                    }
                }
            }
        }
    }
}
