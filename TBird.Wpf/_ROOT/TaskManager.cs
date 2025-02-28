using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Wpf
{
    public class BackgroundTaskManager : BackgroundTaskManager<object>
    {

    }

    public class BackgroundTaskManager<T> : TaskManager<T>
    {
        public override void Execute(T parameter)
        {
            using (var vm = new TaskViewModel<T>(this, parameter))
            {
                vm.ShowDialog(() => new TaskWindow());
            }
        }

        public override Task ExecuteAsync(T parameter)
        {
            return WpfUtil.ExecuteOnBACK(() => base.ExecuteAsync(parameter));
        }
    }
}