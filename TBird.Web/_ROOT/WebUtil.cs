using Codeplex.Data;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
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

        public static string GetUrl(string baseurl, Dictionary<string, string> dic)
        {
            baseurl = baseurl.TrimEnd('?');
            var urlparameter = dic.Select(x => $"{x.Key}={HttpUtility.UrlEncode(x.Value)}").GetString("&");
            return $"{baseurl}?{urlparameter}";
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
            return _factory.CreateClient(_name);
        }

        private static object _createclient = new object();
        private static string _name;
        private static ServiceCollection _service;
        private static IHttpClientFactory _factory;

        public static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            return await CreateClient().SendAsync(request);
        }

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
            return XmlUtil.Str2Xml(await GetStringAsync(url));
        }
    }
}