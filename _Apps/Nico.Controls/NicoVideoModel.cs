using Moviewer.Core;
using Moviewer.Core.Controls;
using Moviewer.Nico.Core;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using TBird.Core;

namespace Moviewer.Nico.Controls
{
	public class NicoVideoModel : VideoModel
	{
		public NicoVideoModel()
		{
			Counters.AddRange(Arr(_ViewCount, _MylistCount, _CommentCount));
		}

		public NicoVideoModel(string contentid) : this()
		{
			ContentId = contentid;
			Status = VideoStatus.Delete;

			_beforedisplay = true;
		}

		public NicoVideoModel(dynamic item) : this()
		{
			try
			{
				ContentId = DynamicUtil.S(item, "contentId");
				Title = DynamicUtil.S(item, "title");
				Description = DynamicUtil.S(item, "description");
				ThumbnailUrl = DynamicUtil.S(item, "thumbnailUrl");
				ViewCount = DynamicUtil.L(item, "viewCounter");
				CommentCount = DynamicUtil.L(item, "commentCounter");
				MylistCount = DynamicUtil.L(item, "mylistCounter");
				StartTime = DateTimeOffset.Parse(DynamicUtil.S(item, "startTime")).DateTime;
				Duration = TimeSpan.FromSeconds(DynamicUtil.L(item, "lengthSeconds"));
				Tags.AddRange((string[])DynamicUtil.O(item, "tags"));
				RefreshStatus();

				_beforedisplay = true;
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
				Status = VideoStatus.Delete;
			}
		}

		//public NicoVideoModel(dynamic json)
		//{
		//    ContentId = (string)json.data.video.id;
		//    Title = (string)json.data.video.title;
		//    Description = (string)json.data.video.description;
		//    ThumbnailUrl = (string)json.data.video.thumbnail.url;
		//    ViewCount = (long)json.data.video.count.view;
		//    CommentCount = (long)json.data.video.count.comment;
		//    MylistCount = (long)json.data.video.count.mylist;
		//    StartTime = DateTime.Parse((string)json.data.video.registeredAt);
		//    Duration = TimeSpan.FromSeconds((long)json.data.video.duration);
		//    Tags.AddRange(string.Join(' ', ((IEnumerable<object>)json.data.tag.items).Select(x => ((dynamic)x).name))));
		//    UserInfo = json.data.channel == null
		//        ? new NicoUserModel($"{json.data.owner.id}", (string)json.data.owner.nickname)
		//        : new NicoUserModel((string)json.data.channel.id, (string)json.data.channel.name);

		//    RefreshStatus();

		//    _beforedisplay = false;
		//}

		public NicoVideoModel(XElement xml) : this()
		{
			xml = xml.Descendants("thumb").First();
			ContentId = NicoUtil.Url2Id(xml.ElementS("watch_url"));
			Title = xml.ElementS("title");
			Description = xml.ElementS("description");
			ThumbnailUrl = xml.ElementS("thumbnail_url");
			ViewCount = xml.ElementL("view_counter");
			CommentCount = xml.ElementL("comment_num");
			MylistCount = xml.ElementL("mylist_counter");
			StartTime = DateTime.Parse(xml.ElementS("first_retrieve"));
			Duration = ToDuration(xml.ElementS("length"));
			Tags.AddRange(xml.Descendants("tags").First().Descendants("tag").Select(tag => (string)tag));
			UserInfo.SetUserInfo(
				CoreUtil.Nvl(xml.ElementS("user_id"), "ch" + xml.ElementS("ch_id")),
				CoreUtil.Nvl(xml.ElementS("user_nickname"), xml.ElementS("ch_name"))
			);
			RefreshStatus();

			_beforedisplay = false;
		}

		public NicoVideoModel(XElement item, string view, string mylist, string comment) : this()
		{
			try
			{
				// 明細部読み込み
				var descriptionString = item.Element("description").Value;
				descriptionString = descriptionString.Replace("&nbsp;", "&#x20;");
				//descriptionString = HttpUtility.HtmlDecode(descriptionString);
				descriptionString = descriptionString.Replace("&", "&amp;");
				descriptionString = descriptionString.Replace("'", "&apos;");
				var descriptionXml = XmlUtil.ToXml($"<root>{descriptionString}</root>");

				ContentId = NicoUtil.Url2Id(item.ElementS("link"));
				Title = item.Element("title").Value;
				Description = (string)descriptionXml.Descendants("p").FirstOrDefault(x => x.AttributeS("class") == "nico-description");
				ThumbnailUrl = descriptionXml.Descendants("img").First().AttributeS("src");
				ViewCount = ToCounter(descriptionXml, view);
				CommentCount = ToCounter(descriptionXml, comment);
				MylistCount = ToCounter(descriptionXml, mylist);
				StartTime = ToRankingDatetime(descriptionXml, "nico-info-date");
				Duration = ToDuration(descriptionXml);
				RefreshStatus();

				_beforedisplay = true;
			}
			catch
			{
				Status = VideoStatus.Delete;
			}
		}

		private TimeSpan ToDuration(string lengthSecondsStr)
		{
			var lengthSecondsIndex = 0;
			var lengthSeconds = lengthSecondsStr
					.Split(':')
					.Reverse()
					.Sum(s => int.Parse(s) * Math.Pow(60, lengthSecondsIndex++));
			return TimeSpan.FromSeconds((long)lengthSeconds);
		}

		private TimeSpan ToDuration(XElement xml)
		{
			var lengthSecondsStr = (string)xml
				.Descendants("strong")
				.Where(x => (string)x.Attribute("class") == "nico-info-length")
				.First();

			return ToDuration(lengthSecondsStr);
		}

		private string GetData(XElement e, string name)
		{
			return (string)e
				.Descendants("strong")
				.Where(x => (string)x.Attribute("class") == name)
				.FirstOrDefault();
		}

		private long ToCounter(XElement e, string name)
		{
			var s = string.IsNullOrEmpty(name) ? null : GetData(e, name);

			return string.IsNullOrEmpty(s)
				? 0
				: long.Parse(s.Replace(",", ""));
		}

		private DateTime ToRankingDatetime(XElement e, string name)
		{
			// 2018年02月27日 20：00：00
			var s = GetData(e, name);

			return DateTime.ParseExact(s,
				"yyyy年MM月dd日 HH：mm：ss",
				System.Globalization.DateTimeFormatInfo.InvariantInfo,
				System.Globalization.DateTimeStyles.None
			);
		}

		public override MenuMode Mode { get; } = MenuMode.Niconico;

		public long ViewCount
		{
			get => _ViewCount.Count;
			set => _ViewCount.Count = value;
		}
		private CounterModel _ViewCount = new CounterModel(CounterType.View, 0);

		public long MylistCount
		{
			get => _MylistCount.Count;
			set => _MylistCount.Count = value;
		}
		private CounterModel _MylistCount = new CounterModel(CounterType.Mylist, 0);

		public long CommentCount
		{
			get => _CommentCount.Count;
			set => _CommentCount.Count = value;
		}
		private CounterModel _CommentCount = new CounterModel(CounterType.Comment, 0);

		protected override UserModel CreateUserInfo()
		{
			return new NicoUserModel();
		}

		protected override async Task OnLoaded()
		{
			if (!_beforedisplay) return;

			var m = await NicoUtil.GetVideo(ContentId);
			if (m.Status != VideoStatus.Delete)
			{
				ContentId = CoreUtil.Nvl(ContentId, m.ContentId);
				Title = CoreUtil.Nvl(Title, m.Title);
				Description = CoreUtil.Nvl(m.Description, Description);
				ThumbnailUrl = CoreUtil.Nvl(ThumbnailUrl, m.ThumbnailUrl);
				ViewCount = Arr(ViewCount, m.ViewCount).Max();
				CommentCount = Arr(CommentCount, m.CommentCount).Max();
				MylistCount = Arr(MylistCount, m.MylistCount).Max();
				StartTime = Arr(StartTime, m.StartTime).Max();
				Duration = Arr(Duration, m.Duration).Max();
				Tags.AddRange(m.Tags);
				UserInfo.SetUserInfo(m.UserInfo);
				RefreshStatus();

				_beforedisplay = false;
			}
		}

		private bool _beforedisplay = false;

		public static NicoVideoModel FromHistory(VideoHistoryModel m)
		{
			var video = new NicoVideoModel(m.ContentId);

			m.AddOnPropertyChanged(video, (sender, e) =>
			{
				video.TempTime = m.Date;
				video.RefreshStatus();
			}, nameof(m.Date), true);

			video.AddOnPropertyChanged(m, (sender, e) =>
			{
				m.Date = video.TempTime;
				video.RefreshStatus();
			}, nameof(video.TempTime), false);

			return video;
		}

	}
}