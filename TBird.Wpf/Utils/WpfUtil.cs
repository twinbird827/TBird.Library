using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TBird.Core;

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
                WaitDoEvents(_dispatcher.BeginInvoke(action));
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
                return (T)WaitDoEvents(_dispatcher.BeginInvoke(action)).Result;
            }
            else
            {
                return action();
            }
        }

        /// <summary>
        /// UIｽﾚｯﾄﾞの処理を徐々に実行します。
        /// </summary>
        /// <param name="x">処理ﾀｽｸ</param>
        /// <returns></returns>
        private static DispatcherOperation WaitDoEvents(DispatcherOperation x)
        {
            while (x.Status == DispatcherOperationStatus.Executing || x.Status == DispatcherOperationStatus.Pending)
            {
                x.Wait(TimeSpan.FromMilliseconds(32));
                DoEvents();
            }
            return x;
        }

        public static Task ExecuteOnBackground(Func<Task> func)
        {
            return OnUI() ? Task.Run(func) : func();
        }

        public static Task<T> ExecuteOnBackground<T>(Func<Task<T>> func)
        {
            return OnUI() ? Task.Run(func) : func();
        }

        public static Task ExecuteOnBackground(Action action)
        {
            return ExecuteOnBackground(() => CoreUtil.WaitAsync(action));
        }

        public static Task<T> ExecuteOnBackground<T>(Func<T> func)
        {
            return ExecuteOnBackground(() => CoreUtil.WaitAsync(func));
        }

        /// <summary>
        /// 画面ｲﾍﾞﾝﾄをすべて実行します。
        /// </summary>
        public static void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            var callback = new DispatcherOperationCallback(obj =>
            {
                ((DispatcherFrame)obj).Continue = false;
                return null;
            });
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, callback, frame);
            Dispatcher.PushFrame(frame);
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
