using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TBird.Core
{
    public class TaskManager : TaskManager<object>
    {

    }

    public partial class TaskManager<T> : TBirdObject
    {
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private List<object> _list = new List<object>();

        public void Add(Action action)
        {
            _list.Add(action);
        }

        public void Add(Func<Task> func)
        {
            _list.Add(func);
        }

        public void Add(Action<T> action)
        {
            _list.Add(action);
        }

        public void Add(Func<T, Task> func)
        {
            _list.Add(func);
        }

        public void Add(Func<bool> func)
        {
            _list.Add(func);
        }

        public void Add(Func<Task<bool>> func)
        {
            _list.Add(func);
        }

        public void Add(Func<T, bool> action)
        {
            _list.Add(action);
        }

        public void Add(Func<T, Task<bool>> func)
        {
            _list.Add(func);
        }

        public void Execute()
        {
            Execute(default(T));
        }

        public virtual void Execute(T parameter)
        {
            ExecuteAsync(parameter).Wait();
        }

        public Task ExecuteAsync()
        {
            return ExecuteAsync(default(T));
        }

        public virtual async Task ExecuteAsync(T parameter)
        {
            using (await Locker.LockAsync(Lock))
            {
                try
                {
                    foreach (var x in _list)
                    {
                        var nextloop = true;
                        if (x is Action a)
                        {
                            await ExecuteAsync(a);
                        }
                        else if (x is Func<Task> b)
                        {
                            await ExecuteAsync(b);
                        }
                        else if (x is Action<T> c)
                        {
                            await ExecuteAsync(() => c(parameter));
                        }
                        else if (x is Func<T, Task> d)
                        {
                            await ExecuteAsync(() => d(parameter));
                        }
                        else if (x is Func<bool> e)
                        {
                            nextloop = await ExecuteAsync(e);
                        }
                        else if (x is Func<Task<bool>> f)
                        {
                            nextloop = await ExecuteAsync(f);
                        }
                        else if (x is Func<T, bool> g)
                        {
                            nextloop = await ExecuteAsync(() => g(parameter));
                        }
                        else if (x is Func<T, Task<bool>> h)
                        {
                            nextloop = await ExecuteAsync(() => h(parameter));
                        }
                        if (!nextloop) break;
                    }
                }
                catch (TimeoutException)
                {
                    // no process
                }
                catch (Exception ex)
                {
                    MessageService.Exception(ex);
                }
            }
        }

        private Task ExecuteAsync(Func<Task> func)
        {
            return func().Cts(_cts);
        }

        private Task<TParam> ExecuteAsync<TParam>(Func<Task<TParam>> func)
        {
            return func().Cts(_cts);
        }

        private Task ExecuteAsync(Action action)
        {
            return ExecuteAsync(() => TaskUtil.WaitAsync(action));
        }

        private Task<TParam> ExecuteAsync<TParam>(Func<TParam> func)
        {
            return ExecuteAsync(() => TaskUtil.WaitAsync(func));
        }

        protected override void DisposeManagedResource()
        {
            // 処理中断
            _cts.Cancel();
            // 処理待機
            base.DisposeManagedResource();
            // ﾘｿｰｽ破棄
            _list.Clear();
        }
    }
}