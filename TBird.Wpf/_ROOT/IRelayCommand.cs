using System;
using System.Windows.Input;
using TBird.Core;

namespace TBird.Wpf
{
    public interface IRelayCommand : ICommand, IDisposable, ILocker
    {
        /// <summary>
        /// <see cref="ICommand.CanExecuteChanged"/>ｲﾍﾞﾝﾄを発行します。
        /// </summary>
        void RaiseCanExecuteChanged();

        /// <summary>
        /// <see cref="ICommand.CanExecuteChanged"/>ｲﾍﾞﾝﾄを発行するﾌﾟﾛﾊﾟﾃｨを追加します。
        /// 追加したﾌﾟﾛﾊﾟﾃｨの内容が変更されると<see cref=" ICommand.CanExecuteChanged"/>ｲﾍﾞﾝﾄが発行されるようになります。
        /// </summary>
        /// <param name="bindable">ﾌﾟﾛﾊﾟﾃｨを持つｲﾝｽﾀﾝｽ</param>
        /// <param name="names">ﾌﾟﾛﾊﾟﾃｨの名前ﾘｽﾄ</param>
        /// <returns></returns>
        IRelayCommand AddCanExecuteChanged(IBindable bindable, params string[] names);
    }

    public static class IRelayCommandExtension
    {
        public static bool RaiseAndCanExecuteChanged(this ICommand ic, object parameter)
        {
            if (ic is IRelayCommand rc)
            {
                rc.RaiseCanExecuteChanged();
            }
            return ic.CanExecute(parameter);
        }

        public static bool TryExecute(this ICommand ic, object parameter)
        {
            if (WpfUtil.IsDesignMode())
            {
                return true;
            }
            else if (!ic.RaiseAndCanExecuteChanged(parameter))
            {
                return true;
            }

            try
            {
                ic.Execute(parameter);
                return true;
            }
            catch (Exception ex)
            {
                MessageService.Exception(ex);
                return false;
            }
        }
    }
}