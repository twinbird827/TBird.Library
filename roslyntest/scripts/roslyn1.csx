using TBird.Core;

Func<string, string> func = x => x + "+FUNC";

Run(x =>
{
	Console.WriteLine("Console test 1.");
});

Ticks.Add(x =>
{
	MessageService.Info(x.Data1 + "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
});