using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TBird.Core;

namespace TBird.Wpf
{
    public class RelayCommand : RelayCommand<object>
    {
        public static IRelayCommand DefaultCommand { get; private set; } = Create(null);

        public static IRelayCommand Create(Action<object> action)
        {
            return Create(action, null);
        }

        public static IRelayCommand Create(Action<object> action, Predicate<object> predicate)
        {
            return Create<object>(action, predicate);
        }

        public static IRelayCommand Create(Func<object, Task> func)
        {
            return Create(func, null);
        }

        public static IRelayCommand Create(Func<object, Task> func, Predicate<object> predicate)
        {
            return Create<object>(func, predicate);
        }

        public static IRelayCommand Create<T>(Action<T> action)
        {
            return Create(action, null);
        }

        public static IRelayCommand Create<T>(Action<T> action, Predicate<T> predicate)
        {
            return new RelayCommand<T>(action, predicate);
        }

        public static IRelayCommand Create<T>(Func<T, Task> func)
        {
            return Create(func, null);
        }

        public static IRelayCommand Create<T>(Func<T, Task> func, Predicate<T> predicate)
        {
            return new RelayCommand<T>(func, predicate);
        }

        private RelayCommand() : base(null, null)
        {
            // dummy
        }
    }

    public partial class RelayCommand<T> : IRelayCommand, ILocker
    {
        private Action<T> _action;
        private Predicate<T> _predicate;
        private BackgroundWorker _worker;

        public string Lock { get; private set; }

        public RelayCommand(Action<T> action, Predicate<T> predicate)
        {
            _action = action;
            _predicate = predicate;
        }

        public RelayCommand(Func<T, Task> func, Predicate<T> predicate)
        {
            Lock = this.CreateLock4Instance();

            _worker = new BackgroundWorker();
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += async (sender, e) =>
            {
                RaiseCanExecuteChanged();
                // 実行直前に再度確認する
                if (!CanExecute((T)e.Argument)) return;
                // 処理実行
                await func((T)e.Argument);
            };
            _worker.RunWorkerCompleted += (sender, e) =>
            {
                RaiseCanExecuteChanged();
            };

            _action = async x =>
            {
                if (_worker.IsBusy)
                {
                    _worker.CancelAsync();
                }

                using (await this.LockAsync())
                {
                    // ﾋﾞｼﾞｰ状態が解除されるまで待機
                    while (_worker.IsBusy) await CoreUtil.Delay(16);
                    // 複数の処理が待機されていた場合、最後の処理だけ実行する
                    if (1 < this.LockCount()) return;
                    // 処理実行
                    _worker.RunWorkerAsync(x);
                }
            };
            _predicate = predicate;
        }

        public event EventHandler CanExecuteChanged;

        public void RaiseCanExecuteChanged()
        {
            WpfUtil.ExecuteOnUI(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }

        public IRelayCommand AddCanExecuteChanged(IBindable bindable, params string[] names)
        {
            if (bindable == null) return this;

            bindable.AddOnPropertyChanged(bindable, (sender, e) =>
            {
                if (names.Contains(e.PropertyName))
                {
                    RaiseCanExecuteChanged();
                }
            });

            return this;
        }

        public bool CanExecute(object parameter)
        {
            return disposedValue
                ? false
                : _predicate == null
                ? true
                : _predicate((T)parameter);
        }

        public void Execute(object parameter)
        {
            try
            {
                if (_action == null)
                {
                    return;
                }
                else if (WpfUtil.IsDesignMode())
                {
                    return;
                }
                else if (!CanExecute(parameter))
                {
                    return;
                }
                else
                {
                    _action((T)parameter);
                }
            }
            catch (Exception ex)
            {
                ServiceFactory.MessageService.Exception(ex);
            }
        }
    }
}
