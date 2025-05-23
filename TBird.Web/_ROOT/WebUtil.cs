using Codeplex.Data;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using TBird.Core;

namespace TBird.Web
{
	public static class WebUtil
	{
		public static void Browse(string url)
		{
			// ﾌﾞﾗｳｻﾞ起動
			Process.Start(WebSetting.Instance.BrowserPath, url);
		}

		public static string ToParameter(Dictionary<string, string> dic)
		{
			var urlparameter = dic.Select(x => $"{x.Key}={HttpUtility.UrlEncode(x.Value)}").GetString("&");
			return urlparameter;
		}

		public static string GetUrl(string baseurl, Dictionary<string, string> dic)
		{
			return $"{baseurl.TrimEnd('?')}?{ToParameter(dic)}";
		}

		public static HttpClient CreateClient()
		{
			lock (_createclient)
			{
				if (_service == null)
				{
					_name = Guid.NewGuid().ToString();
					_service = new ServiceCollection();
					_service
						.AddHttpClient(_name)
						.AddTransientHttpErrorPolicy(
							x => x.WaitAndRetryAsync(Enumerable.Range(1, 5).Select(i => TimeSpan.FromSeconds(i * 2)))
						);

					_factory = _service.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
				}
			}

			return _factory.CreateClient(_name).Run(x =>
			{
				x.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
			});
		}

		private static object _createclient = new object();
		private static string _name;
		private static ServiceCollection _service;
		private static IHttpClientFactory _factory;

		private static async Task<string> ResponseToStr(HttpResponseMessage response)
		{
			if (response.IsSuccessStatusCode)
			{
				return await response.Content.ReadAsStringAsync();
			}
			else
			{
				return null;
			}
		}

		public static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
		{
			return await CreateClient().SendAsync(request);
		}

		public static async Task<string> SendStringAsync(HttpRequestMessage request)
		{
			return await ResponseToStr(await SendAsync(request));
		}

		public static async Task<string> PostStringAsync(string url, string content, string mediatype)
		{
			var request = new HttpRequestMessage(HttpMethod.Post, url)
			{
				Content = new StringContent(content, Encoding.UTF8, mediatype)
			};
			return await SendStringAsync(request);
		}

		/// <summary>
		/// URLの内容を取得します。
		/// </summary>
		/// <param name="url">URL</param>
		/// <returns></returns>
		public static async Task<string> GetStringAsync(string url)
		{
			return await SendStringAsync(new HttpRequestMessage(HttpMethod.Get, url));
		}

		public static async Task<string> GetStringAsync(string url, Encoding srcenc, Encoding dstenc)
		{
			return dstenc.GetString(Encoding.Convert(srcenc, dstenc, await GetBytesAsync(url)));
		}

		public static async Task<byte[]> GetBytesAsync(string url)
		{
			var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
			if (response.IsSuccessStatusCode)
			{
				return await response.Content.ReadAsByteArrayAsync();
			}
			else
			{
				return await GetBytesAsync(url);
			}
		}

		/// <summary>
		/// URLの内容をJson形式で取得します。
		/// </summary>
		/// <param name="url">URL</param>
		public static async Task<dynamic> GetJsonAsync(string url)
		{
			return DynamicJson.Parse(await GetStringAsync(url).TryCatch());
		}

		/// <summary>
		/// URLの内容をXml形式で取得します。
		/// </summary>
		/// <param name="url">URL</param>
		public static async Task<XElement> GetXmlAsync(string url)
		{
			return XmlUtil.ToXml(await GetStringAsync(url));
		}
	}
}