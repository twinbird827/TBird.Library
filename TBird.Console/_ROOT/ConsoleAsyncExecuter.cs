namespace TBird.Console
{
	public abstract class ConsoleAsyncExecuter : ConsoleExecuter
	{
		protected override sealed void Process(Dictionary<string, string> options, string[] args)
		{
			var task = ProcessAsync(options, args);

			while (!task.IsCompleted)
			{
				Thread.Sleep(100);
			}

			if (task.Exception is AggregateException ex)
			{
				throw 1 < ex.InnerExceptions.Count
					? ex
					: ex.InnerException is Exception x ? x : ex.GetBaseException();
			}
		}

		protected abstract Task ProcessAsync(Dictionary<string, string> options, string[] args);
	}
}