using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Web
{
	public static class WebImageUtil
	{
		private const string SaveDir = @"cache\bytes";

		static WebImageUtil()
		{
			// 起動時に一回だけ古いｷｬｯｼｭを削除する。
			DirectoryUtil.DeleteInFiles(SaveDir, info => info.CreationTime < DateTime.Now.AddDays(-7));
		}

		/// <summary>
		/// URLからﾊﾞｲﾄﾃﾞｰﾀを取得する。
		/// </summary>
		/// <param name="urls">URLﾘｽﾄ</param>
		/// <returns></returns>
		public static async Task<byte[]> GetBytesAsync(params string[] urls)
		{
			foreach (var url in urls)
			{
				if (string.IsNullOrEmpty(url)) continue;

				var response = await WebUtil.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
				if (response.IsSuccessStatusCode)
				{
					return await response.Content.ReadAsByteArrayAsync();
				}
			}
			return null;
		}

		/// <summary>
		/// ﾊﾞｲﾄﾃﾞｰﾀを取得する。
		/// </summary>
		/// <param name="key">ｷｰ情報</param>
		/// <param name="urls">URLﾘｽﾄ</param>
		/// <returns></returns>
		public static async Task<byte[]> GetBytesAsync(string key, string[] urls)
		{
			using (await Locker.LockAsync(key))
			{
				byte[] bytes = GetBytesFromFile(key);

				if (bytes == null) bytes = await GetBytesAsync(urls);

				if (bytes == null) return null;

				SetBytesToFile(key, bytes);

				return bytes;
			}
		}

		private static string _lock = Locker.GetNewLockKey(typeof(WebImageUtil));

		/// <summary>
		/// ｷｰに紐づくﾌｧｲﾙからﾊﾞｲﾄﾃﾞｰﾀを取得する。
		/// </summary>
		/// <param name="key">ｷｰ情報</param>
		/// <returns></returns>
		private static byte[] GetBytesFromFile(string key)
		{
			var file = GetSavePath(key);

			return File.Exists(file)
				? File.ReadAllBytes(file)
				: null;
		}

		/// <summary>
		/// ﾊﾞｲﾄﾃﾞｰﾀをｷｰﾌｧｲﾙに保存する。
		/// </summary>
		/// <param name="key">ｷｰ情報</param>
		/// <param name="bytes">ﾊﾞｲﾄﾃﾞｰﾀ</param>
		private static void SetBytesToFile(string key, byte[] bytes)
		{
			var file = GetSavePath(key);

			if (File.Exists(file)) return;

			FileUtil.BeforeCreate(file);

			File.WriteAllBytes(file, bytes);
		}

		/// <summary>
		/// 保存ﾌｧｲﾙﾊﾟｽを取得する。
		/// </summary>
		/// <param name="key">ｷｰ情報</param>
		/// <returns></returns>
		private static string GetSavePath(string key)
		{
			return Directories.GetAbsolutePath(SaveDir, key);
		}
	}
}