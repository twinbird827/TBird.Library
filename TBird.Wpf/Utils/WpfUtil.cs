using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace TBird.Wpf
{
    public static class WpfUtil
    {
        /// <summary>
        /// UIのﾃﾞｨｽﾊﾟｯﾁｬ
        /// </summary>
        private static Dispatcher _dispatcher;

        /// <summary>
        /// 現在のﾃﾞｨｽﾊﾟｯﾁｬがUI上かどうか確認します。
        /// </summary>
        /// <returns></returns>
        public static bool OnUI()
        {
            if (Application.Current == null) return false;

            // UIのﾃﾞｨｽﾊﾟｯﾁｬ取得
            _dispatcher = _dispatcher ?? System.Windows.Application.Current.Dispatcher;
            // UIとｶﾚﾝﾄのﾃﾞｨｽﾊﾟｯﾁｬを比較した結果を返却
            return _dispatcher.Thread == Thread.CurrentThread;
        }

        /// <summary>
        /// UI上で処理を実行します。
        /// </summary>
        /// <param name="action"></param>
        public static void ExecuteOnUI(Action action)
        {
            if (!OnUI())
            {
                // 現在のｽﾚｯﾄﾞがUIのﾃﾞｨｽﾊﾟｯﾁｬ上ではない場合、UIのﾃﾞｨｽﾊﾟｯﾁｬ上で処理を実行する。
                _dispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// UI上で処理を実行します。
        /// </summary>
        /// <param name="action"></param>
        public static T ExecuteOnUI<T>(Func<T> action)
        {
            if (!OnUI())
            {
                // 現在のｽﾚｯﾄﾞがUIのﾃﾞｨｽﾊﾟｯﾁｬ上ではない場合、UIのﾃﾞｨｽﾊﾟｯﾁｬ上で処理を実行する。
                return _dispatcher.Invoke(action);
            }
            else
            {
                return action();
            }
        }

        /// <summary>
        /// ﾃﾞｻﾞｲﾝﾓｰﾄﾞかどうか確認します。
        /// </summary>
        /// <returns></returns>
        public static bool IsDesignMode()
        {
            // Check for design mode. 
            return (bool)DesignerProperties
                .IsInDesignModeProperty
                .GetMetadata(typeof(DependencyObject))
                .DefaultValue;
        }

    }
}
