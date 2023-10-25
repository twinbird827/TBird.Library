using Moviewer.Core;
using Moviewer.Core.Controls;
using Moviewer.Tube.Core;
using System;
using System.Threading.Tasks;
using System.Xml;
using TBird.Core;

namespace Moviewer.Tube.Controls
{
	public class TubeVideoModel : VideoModel
	{
		public override MenuMode Mode => MenuMode.Youtube;

		public TubeVideoModel()
		{
			Counters.AddRange(Arr(_ViewCount, _LikeCount, _CommentCount));
		}

		public TubeVideoModel(string id) : this()
		{
			ContentId = id;
			Status = VideoStatus.Delete;

			_beforedisplay = true;
		}

		public TubeVideoModel(dynamic json) : this()
		{
			ContentId = DynamicUtil.S(json, "id");
			Title = DynamicUtil.S(json, "snippet.title");
			Description = DynamicUtil.S(json, "snippet.description");
			ThumbnailUrl = CoreUtil.Nvl(
				DynamicUtil.S(json, "snippet.thumbnails.standard.url"),
				DynamicUtil.S(json, "snippet.thumbnails.high.url"),
				DynamicUtil.S(json, "snippet.thumbnails.medium.url")
			);
			ViewCount = DynamicUtil.L(json, "statistics.viewCount");
			LikeCount = DynamicUtil.L(json, "statistics.likeCount");
			CommentCount = DynamicUtil.L(json, "statistics.commentCount");
			StartTime = DateTime.Parse(DynamicUtil.S(json, "snippet.publishedAt"));
			TempTime = default;
			Duration = XmlConvert.ToTimeSpan(DynamicUtil.S(json, "contentDetails.duration"));
			Tags.AddRange((string[])DynamicUtil.T<string[]>(json, "snippet.tags"));
			UserInfo.SetUserInfo(
				DynamicUtil.S(json, "snippet.channelId"),
				DynamicUtil.S(json, "snippet.channelTitle")
			);

			RefreshStatus();

			_beforedisplay = false;
		}

		public long ViewCount
		{
			get => _ViewCount.Count;
			set => _ViewCount.Count = value;
		}
		private CounterModel _ViewCount = new CounterModel(CounterType.View, 0);

		public long LikeCount
		{
			get => _LikeCount.Count;
			set => _LikeCount.Count = value;
		}
		private CounterModel _LikeCount = new CounterModel(CounterType.Like, 0);

		public long CommentCount
		{
			get => _CommentCount.Count;
			set => _CommentCount.Count = value;
		}
		private CounterModel _CommentCount = new CounterModel(CounterType.Comment, 0);

		protected override async Task OnLoaded()
		{
			if (!_beforedisplay) return;

			var m = await TubeUtil.GetVideo(ContentId);

			SetModel(m);
		}

		private bool _beforedisplay = false;

		public void SetModel(TubeVideoModel m)
		{
			Title = m.Title;
			Description = m.Description;
			ThumbnailUrl = m.ThumbnailUrl;
			ViewCount = m.ViewCount;
			LikeCount = m.LikeCount;
			CommentCount = m.CommentCount;
			StartTime = m.StartTime;
			TempTime = m.TempTime;
			Duration = m.Duration;
			Tags.AddRange(m.Tags);
			UserInfo.SetUserInfo(m.UserInfo);

			RefreshStatus();

			_beforedisplay = false;
		}

		public static TubeVideoModel FromHistory(VideoHistoryModel m)
		{
			var video = new TubeVideoModel(m.ContentId);

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