using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Moviewer.Core
{
	public class PathSetting : JsonBase<PathSetting>
	{
		private const string _path = @"lib\path-setting.json";

		public static PathSetting Instance
		{
			get => _Instance = _Instance ?? new PathSetting();
		}
		private static PathSetting _Instance;

		public PathSetting() : base(_path)
		{
			if (!Load())
			{
				RootDirectory = Directories.RootDirectory;
			}
		}

		public string RootDirectory
		{
			get => GetProperty(_RootDirectory);
			set => SetProperty(ref _RootDirectory, value);
		}
		private string _RootDirectory;

		public string GetFullPath(params string[] paths) => Directories.GetAbsolutePath(RootDirectory, paths);
	}
}