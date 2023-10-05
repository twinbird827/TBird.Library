using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Web
{
	public static class WebImageUtil
	{
		public static async Task<byte[]> GetBytesAsync(string url)
		{
			var response = await WebUtil.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
			if (response.IsSuccessStatusCode)
			{
				return await response.Content.ReadAsByteArrayAsync();
			}
			else
			{
				return null;
			}
		}

		public static async Task<byte[]> GetBytesAsync(string[] urls)
		{
			foreach (var url in urls)
			{
				if (string.IsNullOrEmpty(url)) continue;
				var bytes = await GetBytesAsync(url);
				if (bytes != null) return bytes;
			}
			return null;
		}

		public static async Task<byte[]> GetBytesAsync(string key, string[] urls)
		{
			using (await Locker.LockAsync(_lock))
			{
				var bytes = GetBytesFromFile(key) ?? await GetBytesAsync(urls);

				if (bytes == null) return null;

				SetBytesToFile(key, bytes);

				return bytes;
			}
		}

		private static string _lock = Locker.GetNewLockKey(typeof(WebImageUtil));

		private static byte[] GetBytesFromFile(string key)
		{
			var file = GetSavePath(key);

			DirectoryUtil.DeleteInFiles(Path.GetDirectoryName(file), info => info.CreationTime < DateTime.Now.AddDays(-7));

			return File.Exists(file)
				? File.ReadAllBytes(file)
				: null;
		}

		private static void SetBytesToFile(string key, byte[] bytes)
		{
			var file = GetSavePath(key);

			if (File.Exists(file)) return;

			FileUtil.BeforeCreate(file);

			File.WriteAllBytes(file, bytes);
		}

		private static string GetSavePath(string key)
		{
			return Directories.GetAbsolutePath(@"cache\bytes", key);
		}
	}
}