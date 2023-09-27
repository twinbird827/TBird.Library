using System;
using System.Threading;
using System.Threading.Tasks;

namespace TBird.Core
{
	public static class SemaphoreExtension
	{
		public static async Task<IDisposable> LockAsync(this SemaphoreSlim slim)
		{
			await slim.WaitAsync();
			return new Disposer<SemaphoreSlim>(slim, arg => arg.Release());
		}
	}
}