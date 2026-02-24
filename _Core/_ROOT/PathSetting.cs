using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba
{
	public class PathSetting : JsonBase<PathSetting>
	{
		public static PathSetting Instance { get; } = new PathSetting();

		public PathSetting(string path) : base(path)
		{
			if (!Load())
			{
				RootDirectory = AppContext.BaseDirectory;
			}
		}

		public PathSetting() : this(@"lib\path-setting.json")
		{

		}

		public string RootDirectory
		{
			get => GetProperty(_RootDirectory);
			set => SetProperty(ref _RootDirectory, value);
		}
		private string _RootDirectory = string.Empty;

	}
}