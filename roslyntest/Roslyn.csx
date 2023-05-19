using TBird.Core;

Func<string, string> func = x => x + "+FUNC";

Run(x =>
{
	Console.WriteLine("Console test root.");
	MessageService.Info(func(x.Data1));
});

Run(x =>
{
	MessageService.Info(func(x.Data2));
});

Ticks.Add(x =>
{
	MessageService.Info(x.Data1 + "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
});