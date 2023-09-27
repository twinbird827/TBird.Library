using System;
using System.IO;

namespace TBird.Core
{
	public static class Directories
	{
		/// <summary>ｱﾌﾟﾘｹｰｼｮﾝ実行ﾃﾞｨﾚｸﾄﾘ</summary>
		public static string RootDirectory => AppDomain.CurrentDomain.BaseDirectory;

		/// <summary>ﾄﾞｷｭﾒﾝﾄﾌｫﾙﾀﾞ</summary>
		public static string DocumentsDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

		/// <summary>ﾃﾞｽｸﾄｯﾌﾟﾌｫﾙﾀﾞ</summary>
		public static string DesktopDirectory => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

		/// <summary>ﾋﾟｸﾁｬﾌｫﾙﾀﾞ</summary>
		public static string PicturesDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

		/// <summary>ﾋﾞﾃﾞｵﾌｫﾙﾀﾞ</summary>
		public static string VideosDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

		/// <summary>ﾐｭｰｼﾞｯｸﾌｫﾙﾀﾞ</summary>
		public static string MusicDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

		/// <summary>ﾀﾞｳﾝﾛｰﾄﾞﾌｫﾙﾀﾞ</summary>
		public static string DownloadDirectory
		{
			get
			{
				var instanceType = Type.GetTypeFromProgID("Shell.Application");
				dynamic shell = Activator.CreateInstance(instanceType);
				var folder = shell.Namespace("shell:Downloads");
				return folder.Self.Path;
			}
		}

		public static string GetAbsolutePath(string path)
		{
			return Path.Combine(RootDirectory, path);
		}

		public static string GetAbsolutePath(string directory, string file)
		{
			return Path.Combine(GetAbsolutePath(directory), file);
		}
	}
}