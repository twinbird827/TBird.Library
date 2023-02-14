using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Wpf
{
    public class TaskManager : TaskManager<object>
    {

    }

    public partial class TaskManager<T> : IDisposable
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

        public void Execute(T parameter)
        {
            using (var vm = new TaskViewModel<T>(this, parameter))
            {
                vm.ShowDialog(() => new TaskWindow());
            }
        }

        public Task ExecuteAsync()
        {
            return ExecuteAsync(default(T));
        }

        public async Task ExecuteAsync(T parameter)
        {
            try
            {
                foreach (var x in _list)
                {
                    var nextloop = true;
                    if (x is Action a)
                    {
                        await WpfUtil.ExecuteOnBackground(a).Cts(_cts);
                    }
                    else if (x is Func<Task> b)
                    {
                        await WpfUtil.ExecuteOnBackground(b).Cts(_cts);
                    }
                    else if (x is Action<T> c)
                    {
                        await WpfUtil.ExecuteOnBackground(() => c(parameter)).Cts(_cts);
                    }
                    else if (x is Func<T, Task> d)
                    {
                        await WpfUtil.ExecuteOnBackground(() => d(parameter)).Cts(_cts);
                    }
                    else if (x is Func<bool> e)
                    {
                        nextloop = await WpfUtil.ExecuteOnBackground(e).Cts(_cts);
                    }
                    else if (x is Func<Task<bool>> f)
                    {
                        nextloop = await WpfUtil.ExecuteOnBackground(f).Cts(_cts);
                    }
                    else if (x is Func<T, bool> g)
                    {
                        nextloop = await WpfUtil.ExecuteOnBackground(() => g(parameter)).Cts(_cts);
                    }
                    else if (x is Func<T, Task<bool>> h)
                    {
                        nextloop = await WpfUtil.ExecuteOnBackground(() => h(parameter)).Cts(_cts);
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
                ServiceFactory.MessageService.Exception(ex);
            }
        }

    }
}
