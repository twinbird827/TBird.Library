using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TBird.Core
{
	public static class DirectoryUtil
	{
		private static string ToShort(string s) => Win32Methods.GetShortPathName(s);

		/// <summary>
		/// ﾃﾞｨﾚｸﾄﾘを作成します。
		/// </summary>
		/// <param name="dir"></param>
		public static void Create(string dir)
		{
			Directory.CreateDirectory(ToShort(dir));
		}

		/// <summary>
		/// ﾃﾞｨﾚｸﾄﾘを移動します。
		/// </summary>
		/// <param name="src">移動元</param>
		/// <param name="dst">移動先</param>
		public static void Move(string src, string dst, bool overwrite = true)
		{
			if (overwrite) Delete(dst);

			Directory.Move(ToShort(src), ToShort(dst));
		}

		/// <summary>
		/// ﾃﾞｨﾚｸﾄﾘをｺﾋﾟｰします。
		/// </summary>
		/// <param name="src">ｺﾋﾟｰ元</param>
		/// <param name="dst">ｺﾋﾟｰ先</param>
		public static void Copy(string src, string dst)
		{
			DirectoryInfo srcdi = new DirectoryInfo(ToShort(src));
			DirectoryInfo dstdi = new DirectoryInfo(ToShort(dst));

			//ｺﾋﾟｰ先のﾃﾞｨﾚｸﾄﾘがなければ作成する
			if (!dstdi.Exists)
			{
				dstdi.Create();
				dstdi.Attributes = srcdi.Attributes;
			}

			//ﾌｧｲﾙのｺﾋﾟｰ
			foreach (var finfo in srcdi.GetFiles())
			{
				//同じﾌｧｲﾙが存在していたら、常に上書きする
				finfo.CopyTo(ToShort(Path.Combine(dstdi.FullName, finfo.Name)), true);
			}

			// ﾃﾞｨﾚｸﾄﾘのｺﾋﾟｰ（再帰を使用）
			foreach (var diinfo in srcdi.GetDirectories())
			{
				Copy(diinfo.FullName, Path.Combine(dstdi.FullName, diinfo.Name));
			}
		}

		/// <summary>
		/// 指定したﾃﾞｨﾚｸﾄﾘを削除します。
		/// </summary>
		/// <param name="info">ﾃﾞｨﾚｸﾄﾘ</param>
		public static void Delete(string directory)
		{
			var info = new DirectoryInfo(ToShort(directory));

			if (!info.Exists) return;

			// ﾃﾞｨﾚｸﾄﾘ内のﾌｧｲﾙ、またはﾃﾞｨﾚｸﾄﾘを削除可能な属性にする。
			foreach (var file in info.GetFileSystemInfos("*", SearchOption.AllDirectories))
			{
				if (file.Attributes.HasFlag(FileAttributes.Directory))
				{
					file.Attributes = FileAttributes.Directory;
				}
				else
				{
					file.Attributes = FileAttributes.Normal;
				}
			}

			// ﾃﾞｨﾚｸﾄﾘの削除
			info.Delete(true);
		}

		/// <summary>
		/// ﾃﾞｨﾚｸﾄﾘ内の条件に合致するﾌｧｲﾙを削除します。
		/// </summary>
		/// <param name="directory">ﾃﾞｨﾚｸﾄﾘ</param>
		/// <param name="func">削除条件</param>
		public static void DeleteInFiles(string directory, Func<FileInfo, bool> func)
		{
			foreach (var info in GetFiles(directory).Select(x => new FileInfo(x)).Where(func))
			{
				info.Delete();
			}
		}

		/// <summary>
		/// ﾃﾞｨﾚｸﾄﾘが存在するか非同期で確認します。
		/// </summary>
		/// <param name="directory">確認するﾃﾞｨﾚｸﾄﾘﾊﾟｽ</param>
		/// <returns></returns>
		public static Task<bool> Exists(string directory)
		{
			return TaskUtil.WaitAsync(directory, s => Directory.Exists(s));
		}

		/// <summary>
		/// ﾃﾞｨﾚｸﾄﾘ内のﾌｧｲﾙﾘｽﾄを取得します。
		/// </summary>
		/// <param name="directory">ﾃﾞｨﾚｸﾄﾘﾊﾟｽ</param>
		/// <param name="pattern">取得するﾌｧｲﾙのﾊﾟﾀｰﾝ</param>
		/// <returns></returns>
		public static string[] GetFiles(string directory, string pattern = "*")
		{
			return Directory.Exists(ToShort(directory))
				? Directory.GetFiles(ToShort(directory), pattern)
				: new string[] { };
		}

		/// <summary>
		/// ﾃﾞｨﾚｸﾄﾘ内のﾃﾞｨﾚｸﾄﾘﾘｽﾄを取得します。
		/// </summary>
		/// <param name="directory">ﾃﾞｨﾚｸﾄﾘﾊﾟｽ</param>
		/// <param name="pattern">取得するﾃﾞｨﾚｸﾄﾘのﾊﾟﾀｰﾝ</param>
		/// <returns></returns>
		public static string[] GetDirectories(string directory, string pattern = "*")
		{
			return Directory.Exists(ToShort(directory))
				? Directory.GetDirectories(ToShort(directory), pattern)
				: new string[] { };
		}
	}
}