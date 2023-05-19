using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TBird.Core
{
    public static class TaskExtension
    {
        public static async Task Cts(this Task task, params CancellationTokenSource[] cancellations)
        {
            var ccs = new TaskCompletionSource<bool>();
            var arr = cancellations.Where(x => x != null).Select(x => x.Token).ToArray();
            using (var tmp = CancellationTokenSource.CreateLinkedTokenSource(arr))
            using (tmp.Token.Register(() => ccs.TrySetResult(true)))
            {
                if (task != await Task.WhenAny(task, ccs.Task))
                {
                    throw new TimeoutException("The process was interrupted.", new OperationCanceledException(tmp.Token));
                }

                if (task.Exception?.InnerException != null)
                {
                    throw new TimeoutException("The process was interrupted.", task.Exception.InnerException);
                }
            }
        }

        public static async Task<T> Cts<T>(this Task<T> task, params CancellationTokenSource[] cancellations)
        {
            await ((Task)task).Cts(cancellations);
            return task.Result;
        }

        /// <summary>
        /// 非同期ﾀｽｸを実行し、指定した時間で処理が完了しない場合は例外を発生させます。
        /// </summary>
        /// <param name="task">非同期ﾀｽｸ</param>
        /// <param name="timeout">ﾀｲﾑｱｳﾄ時間</param>
        /// <returns></returns>
        public static Task Timeout(this Task task, TimeSpan timeout, CancellationTokenSource src)
        {
            using (var cts = new CancellationTokenSource(timeout))
            {
                return task.Cts(src, cts);
            }
        }

        /// <summary>
        /// 非同期ﾀｽｸを実行し、指定した時間で処理が完了しない場合は例外を発生させます。
        /// </summary>
        /// <param name="task">非同期ﾀｽｸ</param>
        /// <param name="timeout">ﾀｲﾑｱｳﾄ時間</param>
        /// <returns></returns>
        public static async Task<T> Timeout<T>(this Task<T> task, TimeSpan timeout, CancellationTokenSource src)
        {
            await ((Task)task).Timeout(timeout, src);
            return task.Result;
        }

        /// <summary>
        /// 非同期ﾀｽｸをすべて実行します。
        /// </summary>
        /// <param name="tasks">非同期ﾀｽｸﾘｽﾄ</param>
        /// <returns></returns>
        public static Task WhenAll(this IEnumerable<Task> tasks)
        {
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// 非同期ﾀｽｸをすべて実行します。
        /// </summary>
        /// <param name="tasks">非同期ﾀｽｸﾘｽﾄ</param>
        /// <returns></returns>
        public static Task<T[]> WhenAll<T>(this IEnumerable<Task<T>> tasks)
        {
            return Task.WhenAll(tasks);
        }
    }
}
