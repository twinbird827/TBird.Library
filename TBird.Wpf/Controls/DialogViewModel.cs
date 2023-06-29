using System;
using System.Windows;
using System.Windows.Input;

namespace TBird.Wpf.Controls
{
    public class DialogViewModel : WindowViewModel
    {

        /// <summary>
        /// ﾀﾞｲｱﾛｸﾞ結果
        /// </summary>
        public bool? DialogResult
        {
            get => _DialogResult;
            set => SetProperty(ref _DialogResult, value);
        }
        private bool? _DialogResult;

        public ICommand OKCommand
        {
            get => _OKCommand = _OKCommand ?? GetOKCommand();
        }
        private ICommand _OKCommand;

        protected virtual ICommand GetOKCommand()
        {
            return RelayCommand.Create(_ => DialogResult = true);
        }

        public ICommand CancelCommand
        {
            get => _CancelCommand = _CancelCommand ?? GetCancelCommand();
        }
        private ICommand _CancelCommand;

        protected virtual ICommand GetCancelCommand()
        {
            return RelayCommand.Create(_ => DialogResult = false);
        }

        public bool? ShowDialog(Func<Window> func)
        {
            return WpfUtil.ExecuteOnUI(() =>
            {
                var window = func();
                window.DataContext = this;
                return ShowDialog(window);
            });
        }

        /// <summary>
        /// ﾀﾞｲｱﾛｸﾞを表示します。
        /// </summary>
        /// <param name="window">ﾀﾞｲｱﾛｸﾞ</param>
        /// <returns></returns>
        private bool? ShowDialog(Window window)
        {
            return ShowModalWindow(window, ControlUtil.GetActiveWindow());
        }

        /// <summary>
        /// 親画面を指定してﾀﾞｲｱﾛｸﾞを表示します。
        /// </summary>
        /// <param name="window">ﾀﾞｲｱﾛｸﾞ</param>
        /// <param name="owner">親画面</param>
        /// <returns></returns>
        private bool? ShowModalWindow(Window window, Window owner)
        {
            if (owner != null)
            {
                return ShowModalWindow(window, owner, Mouse.PrimaryDevice.GetPosition(owner));
            }
            else
            {
                try
                {
                    return window.ShowDialog();
                }
                finally
                {
                    window.Close();
                }
            }
        }

        /// <summary>
        /// 親画面と表示位置を指定してﾀﾞｲｱﾛｸﾞを表示します。
        /// </summary>
        /// <param name="window">ﾀﾞｲｱﾛｸﾞ</param>
        /// <param name="position">表示位置</param>
        /// <param name="owner">親画面</param>
        /// <returns></returns>
        private bool? ShowModalWindow(Window window, Window owner, Point position)
        {
            try
            {
                if (position.X == 0 && position.Y == 0)
                {
                    // 表示位置が生成出来ていない場合は親画面の中心に表示する。
                    return ShowModalWindow(window, owner, new Point(owner.ActualWidth / 2, owner.ActualHeight / 2));
                }

                window.Owner = owner;

                if (window.WindowStartupLocation == WindowStartupLocation.Manual)
                {
                    var ot = owner.WindowState == WindowState.Maximized ? 0 : owner.Top;
                    var ol = owner.WindowState == WindowState.Maximized ? 0 : owner.Left;

                    window.Top = ot + position.Y;
                    window.Left = ol + position.X;

                    BehaviorUtil.Loaded(window, DialogViewModel_ShowDialog_Loaded);
                }

                return window.ShowDialog();
            }
            finally
            {
                window.Close();
            }
        }

        private const int Margin = 20;

        private void DialogViewModel_ShowDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                var owner = window.Owner;
                var ot = owner.WindowState == WindowState.Maximized ? 0 : owner.Top;
                var ol = owner.WindowState == WindowState.Maximized ? 0 : owner.Left;

                window.Top -= window.ActualHeight / 2;
                window.Left -= window.ActualWidth / 2;

                if (window.Top < ot)
                    window.Top = ot + Margin;
                else if (ot + owner.ActualHeight < window.Top + window.ActualHeight + Margin)
                    window.Top -= (window.Top + window.ActualHeight + Margin) - (ot + owner.ActualHeight);

                if (window.Left < ol)
                    window.Left = ol + Margin;
                else if (ol + owner.ActualWidth < window.Left + window.ActualWidth + Margin)
                    window.Left -= (window.Left + window.ActualWidth + Margin) - (ol + owner.ActualWidth);
            }
        }
    }
}