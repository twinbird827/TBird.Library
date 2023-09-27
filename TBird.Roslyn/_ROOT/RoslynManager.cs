using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Roslyn
{
	public partial class RoslynManager
	{
		private const string _csxroot = "scripts";

		private IList<IRoslynExecuter> _list = new List<IRoslynExecuter>();

		public static RoslynManager Instance
		{
			get => _Instance = _Instance ?? new RoslynManager();
		}
		private static RoslynManager _Instance;

		public void Initialize<T>(T parameter)
		{
			// ﾘｽﾄ追加済なら中断
			if (_list.Any()) return;
			// 指定したﾌｫﾙﾀﾞ内の全ｽｸﾘﾌﾟﾄﾌｧｲﾙをﾛｰﾄﾞする。
			DirectoryUtil
				.GetFiles(Path.Combine(Directories.RootDirectory, _csxroot), "*.csx")
				.ForEach(path => Add(path, parameter));
		}

		public void Add<T>(string path, T parameter)
		{
			_list.Add(new RoslynExecuter<T>(path, parameter));
		}

		public Task RunAsync()
		{
			return _list.Select(x => x.RunAsync()).WhenAll();
		}

		public async void RunBackground()
		{
			await TaskUtil.WaitAsync(RunAsync);
		}
	}
}