using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Console
{
	public abstract class ConsoleExecuter : TBirdObject
	{
		// 引数がｵﾌﾟｼｮﾝかどうか
		private bool IsOption(string x) => x.StartsWith('/');

		// 引数がﾊﾟﾗﾒｰﾀかどうか
		private bool IsArgs(string x) => !IsOption(x);

		private void WriteLine(string message) => System.Console.WriteLine(message);

		private void Write(string message) => System.Console.Write(message);

		private string ReadLine() => System.Console.ReadLine().NotNull();

		public void Execute(string[] args)
		{
			MessageService.SetService(new ConsoleMessageService());

			var assm = Assembly.GetEntryAssembly().NotNull();
			var ver = assm.GetName().Version.NotNull();
			var title = $"{assm.FullName} Version: {ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}";

			WriteLine($"**********");
			WriteLine($"* 開始 {title}");
			WriteLine($"**********");

			// 引数をｵﾌﾟｼｮﾝとﾊﾟﾗﾒｰﾀに分ける
			var b = args
				.Where(IsOption)
				.Select(x => x.Substring(1).Split('='))
				.ToDictionary(x => x[0].ToUpper(), x => x.Skip(1).FirstOrDefault() ?? string.Empty);
			var a = args
				.Where(IsArgs)
				.ToArray();
			// 足りないﾊﾟﾗﾒｰﾀを個別に補う
			var o = GetOptions(b);

			try
			{
				// 個別処理実行
				Process(o, a);

				WriteLine($"**********");
				WriteLine($"* 終了 {title}");
				WriteLine($"**********");

				Environment.Exit(0);
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);

				WriteLine($"**********");
				WriteLine($"* 異常 {title}");
				WriteLine($"**********");

				Pause(o);

				Environment.Exit(GetErrorCode(ex));
			}
		}

		protected virtual Dictionary<string, string> GetOptions(Dictionary<string, string> options)
		{
			return options;
		}

		protected abstract void Process(Dictionary<string, string> options, string[] args);

		protected virtual int GetErrorCode(Exception ex) => -1;

		protected void Pause(Dictionary<string, string> o)
		{
			if (!o.ContainsKey("H")) System.Console.Read();
		}

		protected void SetOption(Dictionary<string, string> options, string key, string def, params string[] messages)
		{
			messages.ForEach(x => System.Console.WriteLine(x));

			Write($"INPUT: ");
			var v = options.ContainsKey(key) ? options[key] : CoreUtil.Nvl(ReadLine(), def);
			if (options.ContainsKey(key)) WriteLine(v);

			options[key] = v;
		}
	}
}