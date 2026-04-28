using Moviewer.Core;
using Moviewer.Nico.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TBird.Core;
using TBird.Web;

namespace Moviewer.Nico.Core
{
	public static class NicoUtil
	{
		public const string NicoBlankUserUrl = "https://secure-dcdn.cdn.nimg.jp/nicoaccount/usericon/defaults/blank.jpg";

		private static readonly Dictionary<string, string> NvapiHeaders = new()
		{
			["X-Frontend-Id"] = "6",
			["X-Frontend-Version"] = "0",
		};

		public static Task<dynamic> GetNvapiJsonAsync(string url)
			=> WebUtil.GetJsonAsync(url, NvapiHeaders);

		public static string Url2Id(string url)
		{
			return CoreUtil.Nvl(url).Split('/').Last().Split('?').First();
		}

		public static async Task<NicoVideoModel> GetVideo(string videoid)
		{
			try
			{
				var json = await WebUtil.GetJsonAsync(GetNicoVideoUrl(videoid));
				if (json != null && (int)json.meta.status == 200)
				{
					return NicoVideoModel.FromWatchData(json.data);
				}
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
			}
			var video = new NicoVideoModel();
			video.ContentId = videoid;
			video.Status = VideoStatus.Delete;
			return video;
		}

		public static string GetNicoVideoUrl(string contentid)
		{
			var session = (long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds;
			var trackid = $"MOVIEWER_{session}";
			return $"https://www.nicovideo.jp/api/watch/v3_guest/{contentid}?_frontendId=6&_frontendVersion=0&actionTrackId={trackid}&skips=harmful&noSideEffect=false&t={session}";
		}

		public static IAsyncEnumerable<NicoVideoModel> GetVideoBySearchType(string word, NicoSearchType type, string order)
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

		public static async IAsyncEnumerable<NicoVideoModel> GetVideosByRanking(string genre, string tag, string term)
		{
			// 呼出側 (NicoRankingViewModel) は慣習的に "all" を渡してくるが、
			// nvapi では `tag=all` は「all という文字列タグでフィルタ」のセマンティクスを取り得るため
			// 無タグ呼出 (= ジャンル全体ランキング) と等価にならない。"all" を空扱いに正規化する。
			var effectiveTag = (string.IsNullOrEmpty(tag) || tag == "all") ? null : tag;
			var url = $"https://nvapi.nicovideo.jp/v1/ranking/genre/{genre}?term={term}&pageSize=100"
				+ (string.IsNullOrEmpty(effectiveTag) ? "" : $"&tag={Uri.EscapeDataString(effectiveTag)}");
			var json = await GetNvapiJsonAsync(url);
			if (json == null) yield break;
			int rank = 1;
			foreach (var item in json.data.items)
			{
				var video = NicoVideoModel.FromEssential(item);
				if (video.Status != VideoStatus.Delete && !string.IsNullOrEmpty(video.Title))
				{
					// OnLoaded 経由の `CoreUtil.Nvl(Title, m.Title)` は既存値優先のため
					// このﾌﾟﾚﾌｨｯｸｽは保持される
					video.Title = $"第{rank}位：{video.Title}";
				}
				rank++;
				yield return video;
			}
		}

		public static async IAsyncEnumerable<NicoVideoModel> GetVideosByMylist(string mylistid, string orderby)
		{
			var parts = orderby.Split(',');
			var sortKey = parts[0];
			var sortOrder = parts[1];
			int page = 1;
			while (true)
			{
				var url = $"https://nvapi.nicovideo.jp/v2/mylists/{mylistid}?sortKey={sortKey}&sortOrder={sortOrder}&pageSize=100&page={page}";
				var json = await GetNvapiJsonAsync(url);
				if (json == null) yield break;
				var ml = json.data.mylist;
				foreach (var it in ml.items)
				{
					yield return NicoVideoModel.FromMylistItem(it);
				}
				bool hasNext = false;
				try { hasNext = (bool)ml.hasNext; } catch { hasNext = false; }
				if (!hasNext) yield break;
				page++;
			}
		}

		public static IAsyncEnumerable<NicoVideoModel> GetVideosByNicouser(string userid, string order)
		{
			// user 動画では regdate(登録日時) と stadate(投稿日時) は同義 (= registeredAt)。
			// oyder_by_user XMLから regdate を削除したため、ここで stadate に正規化してマッピング不在を回避。
			var normalized = order switch
			{
				"regdate-" => "stadate-",
				"regdate+" => "stadate+",
				_ => order,
			};
			var orderbyuser = ComboUtil.GetNicoDisplay("oyder_by_user", normalized).Split(',');
			return GetVideosByNicouser(userid, orderbyuser[0], orderbyuser[1]);
		}

		private static async IAsyncEnumerable<NicoVideoModel> GetVideosByNicouser(string userid, string key, string order)
		{
			int page = 1;
			while (true)
			{
				var url = $"https://nvapi.nicovideo.jp/v2/users/{userid}/videos?sortKey={key}&sortOrder={order}&pageSize=100&page={page}";
				var json = await GetNvapiJsonAsync(url);
				if (json == null) yield break;
				int count = 0;
				foreach (var it in json.data.items)
				{
					count++;
					yield return NicoVideoModel.FromEssential(it.essential);
				}
				if (count < 100) yield break;
				page++;
			}
		}

		public static IAsyncEnumerable<NicoVideoModel> GetVideosByWord(string word, string order, int offset = 0, int limit = 50)
		{
			const string target = "title,description,tags";
			return SearchApiV2(word, target, order, offset, limit);
		}

		public static IAsyncEnumerable<NicoVideoModel> GetVideosByTag(string word, string order, int offset = 0, int limit = 50)
		{
			const string target = "tagsExact";
			return SearchApiV2(word, target, order, offset, limit);
		}

		private static async IAsyncEnumerable<NicoVideoModel> SearchApiV2(string word, string target, string order, int offset = 0, int limit = 50)
		{
			var context = CoreSetting.Instance.ApplicationKey;
			var orderbyapiv2 = ComboUtil.GetNicoDisplay("oyder_by_apiv2", order);
			var field = "contentId,title,description,userId,viewCounter,mylistCounter,lengthSeconds,thumbnailUrl,startTime,commentCounter,tags,channelId,thumbnailUrl";
			var url = $"https://snapshot.search.nicovideo.jp/api/v2/snapshot/video/contents/search?q={word}&targets={target}&fields={field}&_sort={orderbyapiv2}&_offset={offset}&_limit={limit}&_context={context}";

			var json = await WebUtil.GetJsonAsync(url);
			if (json == null) yield break;
			foreach (var item in json.data)
			{
				yield return new NicoVideoModel(item);
			}
		}
	}
}
