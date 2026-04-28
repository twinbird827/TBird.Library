using Moviewer.Core;
using Moviewer.Core.Controls;
using Moviewer.Nico.Core;
using System;
using System.Linq;
using System.Threading.Tasks;
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
				var tagsStr = DynamicUtil.S(item, "tags");
				if (!string.IsNullOrEmpty(tagsStr)) Tags.AddRange(tagsStr.Split(' '));
				RefreshStatus();

				_beforedisplay = true;
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
				Status = VideoStatus.Delete;
			}
		}

		public static NicoVideoModel FromEssential(dynamic essential)
		{
			var m = new NicoVideoModel();
			try
			{
				m.ContentId = DynamicUtil.S(essential, "id");
				m.Title = DynamicUtil.S(essential, "title");
				m.Description = DynamicUtil.S(essential, "shortDescription");
				m.ThumbnailUrl = DynamicUtil.S(essential, "thumbnail.url");
				m.ViewCount = DynamicUtil.L(essential, "count.view");
				m.CommentCount = DynamicUtil.L(essential, "count.comment");
				m.MylistCount = DynamicUtil.L(essential, "count.mylist");
				m.StartTime = DateTimeOffset.Parse(DynamicUtil.S(essential, "registeredAt")).DateTime;
				m.Duration = TimeSpan.FromSeconds(DynamicUtil.L(essential, "duration"));

				if (essential.IsDefined("owner") && essential.owner != null)
				{
					var ownerId = DynamicUtil.S(essential.owner, "id");
					var ownerName = DynamicUtil.S(essential.owner, "name");
					if (!string.IsNullOrEmpty(ownerId))
					{
						m.UserInfo.SetUserInfo(ownerId, ownerName);
					}
				}

				m.RefreshStatus();
				m._beforedisplay = true;
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
				m.Status = VideoStatus.Delete;
			}
			return m;
		}

		public static NicoVideoModel FromMylistItem(dynamic item)
		{
			var m = FromEssential(item.video);
			if (m.Status == VideoStatus.Delete) return m;
			try
			{
				var addedAt = DynamicUtil.S(item, "addedAt");
				if (!string.IsNullOrEmpty(addedAt))
				{
					m.MylistAddedAt = DateTimeOffset.Parse(addedAt).DateTime;
				}
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
			}
			return m;
		}

		public static NicoVideoModel FromWatchData(dynamic data)
		{
			var m = new NicoVideoModel();
			try
			{
				m.ContentId = DynamicUtil.S(data, "video.id");
				m.Title = DynamicUtil.S(data, "video.title");
				m.Description = DynamicUtil.S(data, "video.description");
				m.ThumbnailUrl = DynamicUtil.S(data, "video.thumbnail.url");
				m.ViewCount = DynamicUtil.L(data, "video.count.view");
				m.CommentCount = DynamicUtil.L(data, "video.count.comment");
				m.MylistCount = DynamicUtil.L(data, "video.count.mylist");
				m.StartTime = DateTimeOffset.Parse(DynamicUtil.S(data, "video.registeredAt")).DateTime;
				m.Duration = TimeSpan.FromSeconds(DynamicUtil.L(data, "video.duration"));

				if (data.IsDefined("tag") && data.tag != null)
				{
					foreach (var t in data.tag.items)
						m.Tags.Add(DynamicUtil.S(t, "name"));
				}

				// data.channel が非null → チャンネル動画 (id は既に "ch..." 形式)
				if (data.IsDefined("channel") && data.channel != null)
				{
					m.UserInfo.SetUserInfo(
						DynamicUtil.S(data.channel, "id"),
						DynamicUtil.S(data.channel, "name"));
				}
				else if (data.IsDefined("owner") && data.owner != null)
				{
					m.UserInfo.SetUserInfo(
						DynamicUtil.S(data.owner, "id"),
						DynamicUtil.S(data.owner, "nickname"));
				}

				m.RefreshStatus();
				m._beforedisplay = false;
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
				m.Status = VideoStatus.Delete;
			}
			return m;
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

		// Mylist 経由で取得した場合の追加日時。お気に入り巡回(PatrolFavorites)で
		// addedAt 順ソートと整合させるため。それ以外の経路では null。
		public DateTime? MylistAddedAt { get; set; }

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
