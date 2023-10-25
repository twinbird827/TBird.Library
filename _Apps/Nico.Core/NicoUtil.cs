using Moviewer.Core;
using Moviewer.Nico.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using TBird.Core;
using TBird.Web;

namespace Moviewer.Nico.Core
{
	public static class NicoUtil
	{
		public const string NicoBlankUserUrl = "https://secure-dcdn.cdn.nimg.jp/nicoaccount/usericon/defaults/blank.jpg";

		public static string Url2Id(string url)
		{
			return CoreUtil.Nvl(url).Split('/').Last().Split('?').First();
		}

		public static async Task<NicoVideoModel> GetVideo(string videoid)
		{
			//// TODO この方法でも可能だけどﾚｽﾎﾟﾝｽが悪いので旧式を使用する
			//// TODO 旧式はDescriptionが簡素になる
			//var body = await WebUtil.GetStringAsync(GetNicoVideoUrl(videoid));
			//var json = DynamicJson.Parse(body);

			//if ((int)json.meta.status == 200)
			//{
			//    return video.SetFromJsonVideo(json);
			//}
			//else
			//{
			//    video.ContentId = videoid;
			//    video.Status = VideoStatus.Delete;
			//    return video;
			//}

			var xml = await WebUtil.GetXmlAsync($"http://ext.nicovideo.jp/api/getthumbinfo/{videoid}").TryCatch();

			if (xml == null || xml.AttributeS("status") == "fail")
			{
				var video = new NicoVideoModel();
				video.ContentId = videoid;
				video.Status = VideoStatus.Delete;
				return video;
			}
			else
			{
				return new NicoVideoModel(xml);
			}
		}

		public static string GetNicoVideoUrl(string contentid)
		{
			var session = (long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds;
			var trackid = $"MOVIEWER_{session}";
			return $"https://www.nicovideo.jp/api/watch/v3_guest/{contentid}?_frontendId=6&_frontendVersion=0&actionTrackId={trackid}&skips=harmful&noSideEffect=false&t={session}";
		}

		/// <summary>
		/// URLのchannelﾀｸﾞの内容をXml形式で取得します。
		/// </summary>
		/// <param name="url">URL</param>
		public static async Task<XElement> GetXmlChannelAsync(string url)
		{
			var xml = await WebUtil.GetXmlAsync(url);
			var tmp = xml.Descendants("channel");
			return tmp.First();
		}

		private static async Task<IEnumerable<NicoVideoModel>> GetVideosFromXmlUrl(string url, string view, string mylist, string comment)
		{
			var xml = await GetXmlChannelAsync(url);

			return xml
				.Descendants("item")
				.Select(item => new NicoVideoModel(item, view, mylist, comment));
		}

		public static Task<IEnumerable<NicoVideoModel>> GetVideoBySearchType(string word, NicoSearchType type, string order)
		{
			switch (type)
			{
				case NicoSearchType.User:
					return GetVideosByNicouser(word, order);
				case NicoSearchType.Tag:
					return GetVideosByTag(word, order);
				case NicoSearchType.Mylist:
					return GetVideosByMylist(word, ComboUtil.GetNicoDisplay("oyder_by_mylist", order));
				//case NicoSearchType.Word:
				default:
					return GetVideosByWord(word, order);
			}
		}

		public static Task<IEnumerable<NicoVideoModel>> GetVideosByRanking(string genre, string tag, string term)
		{
			var url = $"https://www.nicovideo.jp/ranking/genre/{genre}?video_ranking_menu?tag={tag}&term={term}&rss=2.0&lang=ja-jp";

			return GetVideosFromXmlUrl(url,
				"nico-info-total-view",
				"nico-info-total-mylist",
				"nico-info-total-res"
			);
		}

		public static Task<IEnumerable<NicoVideoModel>> GetVideosByMylist(string mylistid, string orderby)
		{
			var url = $"http://www.nicovideo.jp/mylist/{mylistid}?rss=2.0&numbers=1&sort={orderby}";

			return GetVideosFromXmlUrl(url,
				"nico-numbers-view",
				"nico-numbers-mylist",
				"nico-numbers-res"
			);
		}

		public static Task<IEnumerable<NicoVideoModel>> GetVideosByNicouser(string userid, string order)
		{
			var orderbyuser = ComboUtil.GetNicoDisplay("oyder_by_user", order).Split(',');
			return GetVideosByNicouser(userid, orderbyuser[0], orderbyuser[1]);
		}

		private static Task<IEnumerable<NicoVideoModel>> GetVideosByNicouser(string userid, string key, string order)
		{
			var url = $"https://www.nicovideo.jp/user/{userid}/video?sortKey={key}&sortOrder={order}&rss=2.0";

			return GetVideosFromXmlUrl(url, null, null, null);
		}

		public static Task<IEnumerable<NicoVideoModel>> GetVideosByWord(string word, string order, int offset = 0, int limit = 50)
		{
			const string target = "title,description,tags";
			return SearchApiV2(word, target, order, offset, limit);
		}

		public static Task<IEnumerable<NicoVideoModel>> GetVideosByTag(string word, string order, int offset = 0, int limit = 50)
		{
			const string target = "tagsExact";
			return SearchApiV2(word, target, order, offset, limit);
		}

		private static async Task<IEnumerable<NicoVideoModel>> SearchApiV2(string word, string target, string order, int offset = 0, int limit = 50)
		{
			var context = CoreSetting.Instance.ApplicationKey;
			var orderbyapiv2 = ComboUtil.GetNicoDisplay("oyder_by_apiv2", order);
			var field = "contentId,title,description,userId,viewCounter,mylistCounter,lengthSeconds,thumbnailUrl,startTime,commentCounter,tags,channelId,thumbnailUrl";
			var url = $"https://api.search.nicovideo.jp/api/v2/snapshot/video/contents/search?q={word}&targets={target}&fields={field}&&_sort={orderbyapiv2}&_offset={offset}&_limit={limit}&_context={context}";

			return SearchApiV2(await WebUtil.GetJsonAsync(url));
		}

		private static IEnumerable<NicoVideoModel> SearchApiV2(dynamic json)
		{
			foreach (var item in json.data)
			{
				yield return new NicoVideoModel(item);
			}
		}
	}
}