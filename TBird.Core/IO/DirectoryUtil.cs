using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TBird.Core
{
	public static class DirectoryUtil
	{
		/// <summary>
		/// ﾃﾞｨﾚｸﾄﾘを作成します。
		/// </summary>
		/// <param name="dir"></param>
		public static void Create(string? dir)
		{
			if (dir == null) throw new NullReferenceException($"{typeof(DirectoryUtil).Str()}.{nameof(Create)} {nameof(dir)}");
			Directory.CreateDirectory(dir);
		}

		/// <summary>
		/// ﾃﾞｨﾚｸﾄﾘを移動します。
		/// 別ﾎﾞﾘｭｰﾑへの移動はｺﾋﾟｰと削除で行います。
		/// 移動に失敗したときは、移動先にあったﾃﾞｨﾚｸﾄﾘを元に戻します。
		/// </summary>
		/// <param name="src">移動元</param>
		/// <param name="dst">移動先</param>
		public static void Move(string src, string dst, bool overwrite = true)
		{
			var exists = Directory.Exists(dst);
			if (exists && !overwrite) throw new IOException($"{typeof(DirectoryUtil).Str()}.{nameof(Move)} {dst} already exists.");

			// 移動先は削除せず、成功するまで同じ親ﾃﾞｨﾚｸﾄﾘへ退避しておく
			var backup = exists ? $"{Path.GetFullPath(dst).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}.{Guid.NewGuid()}" : null;
			if (backup != null) Directory.Move(dst, backup);

			var samevolume = string.Equals(
				Path.GetPathRoot(Path.GetFullPath(src)),
				Path.GetPathRoot(Path.GetFullPath(dst)),
				StringComparison.OrdinalIgnoreCase
			);

			try
			{
				if (samevolume) Directory.Move(src, dst);
				else Copy(src, dst);
			}
			catch
			{
				Delete(dst);
				if (backup != null) Directory.Move(backup, dst);
				throw;
			}

			if (backup != null) Delete(backup);
			if (!samevolume) Delete(src);
		}

		/// <summary>
		/// ﾃﾞｨﾚｸﾄﾘをｺﾋﾟｰします。
		/// </summary>
		/// <param name="src">ｺﾋﾟｰ元</param>
		/// <param name="dst">ｺﾋﾟｰ先</param>
		public static void Copy(string src, string dst)
		{
			DirectoryInfo srcdi = new DirectoryInfo(src);
			DirectoryInfo dstdi = new DirectoryInfo(dst);

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
				finfo.CopyTo(Path.Combine(dstdi.FullName, finfo.Name), true);
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
			var info = new DirectoryInfo(directory);

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
			return Directory.Exists(directory)
				? Directory.GetFiles(directory, pattern)
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
			return Directory.Exists(directory)
				? Directory.GetDirectories(directory, pattern)
				: new string[] { };
		}

		/// <summary>
		/// ﾌｧｲﾙ名を連番付きにします。
		/// </summary>
		/// <param name="directory">対象ﾌｧｲﾙを格納したﾃﾞｨﾚｸﾄﾘ</param>
		public static void OrganizeNumber(string directory)
		{
			foreach (var tmpfilebase in new[] { DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "-", string.Empty })
			{
				var i = 1; foreach (var x in GetFiles(directory).OrderBy(x => Regex.Replace(x, @"[0-9]{1,8}", m => m.Value.GetInt32().ToString(8))).ToArray())
				{
					FileUtil.Move(x, Path.Combine(directory, tmpfilebase + i++.ToString(8) + Path.GetExtension(x)));
				}
			}
		}
	}
}