using Codeplex.Data;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TBird.Core
{
    public static class WebUtil
    {
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
            return _factory.CreateClient(_name);
        }
        private static object _createclient = new object();
        private static string _name;
        private static ServiceCollection _service;
        private static IHttpClientFactory _factory;

        private static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            return await CreateClient().SendAsync(request);
        }

        public static async Task<byte[]> GetThumnailBytes(string url)
        {
            using (await Locker.LockAsync(_guid))
            {
                var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                else
                {
                    return null;
                }
            }
        }
        private static string _guid = Guid.NewGuid().ToString();

        /// <summary>
        /// URLの内容を取得します。
        /// </summary>
        /// <param name="url">URL</param>
        /// <returns></returns>
        public static async Task<string> GetStringAsync(string url)
        {
            var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
            if (response.IsSuccessStatusCode)
            {
                var txt = await response.Content.ReadAsStringAsync();

                //txt = txt.Replace("&copy;", "");
                //txt = txt.Replace("&nbsp;", " ");
                //txt = txt.Replace("&#x20;", " ");
                //txt = txt.Replace("&", "&amp;");

                return txt;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// URLの内容をJson形式で取得します。
        /// </summary>
        /// <param name="url">URL</param>
        public static async Task<dynamic> GetJsonAsync(string url)
        {
            return DynamicJson.Parse(await GetStringAsync(url));
        }

        /// <summary>
        /// URLの内容をXml形式で取得します。
        /// </summary>
        /// <param name="url">URL</param>
        public static async Task<XElement> GetXmlAsync(string url)
        {
            return ToXml(await GetStringAsync(url));
        }

        /// <summary>
        /// 文字列をXml形式に変換します。
        /// </summary>
        /// <param name="url">URL</param>
        public static XElement ToXml(string value)
        {
            using (var sr = new StringReader(value))
            {
                return XDocument.Load(sr).Root;
            }
        }

    }
}
