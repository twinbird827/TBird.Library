using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public partial class RelayCommand<T> : IRelayCommand
    {
        private Action<T> _action;
        private Predicate<T> _predicate;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _executing = false;
        public string Lock { get; private set; }

        public RelayCommand(Action<T> action, Predicate<T> predicate)
        {
            _action = x =>
            {
                ChangeExecuting(true);
                action(x);
                ChangeExecuting(false);
            };
            _predicate = predicate;
        }

        public RelayCommand(Func<T, Task> func, Predicate<T> predicate)
        {
            Lock = this.CreateLock4Instance();

            _action = async x =>
            {
                using (await Locker.LockAsync(Lock))
                {
                    RaiseCanExecuteChanged();

                    // 実行直前に再度確認する
                    if (!CanExecute(x)) return;

                    // 複数の処理が待機されていた場合、最後の処理だけ実行する
                    if (1 < Locker.Count(Lock)) return;

                    try
                    {
                        // 押せなくする。
                        ChangeExecuting(true);

                        // 処理実行
                        await func(x).Cts(_cts);
                    }
                    catch (TimeoutException)
                    {
                        // ｽｷｯﾌﾟ
                    }
                    catch (Exception ex)
                    {
                        MessageService.Exception(ex);
                    }
                    finally
                    {
                        // 押せるようにする。
                        ChangeExecuting(false);
                    }
                }
            };
            _predicate = predicate;
        }

        public event EventHandler CanExecuteChanged;

        private void ChangeExecuting(bool value)
        {
            _executing = value; RaiseCanExecuteChanged();
        }

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
                : _executing
                ? false
                : _cts.IsCancellationRequested
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
                MessageService.Exception(ex);
            }
        }
    }
}