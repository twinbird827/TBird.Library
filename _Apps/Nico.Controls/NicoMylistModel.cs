using Moviewer.Core;
using Moviewer.Core.Controls;
using Moviewer.Nico.Core;
using System;
using System.Linq;
using System.Threading.Tasks;
using TBird.Core;

namespace Moviewer.Nico.Controls
{
	public class NicoMylistModel : ControlModel, IThumbnailUrl
	{
		public static async Task<dynamic> GetNicoMylistData(string id)
		{
			var cleanId = NicoUtil.Url2Id(id);
			// 最新 addedAt を MylistDate に使うため、addedAt降順で1件取得
			return await NicoUtil.GetNvapiJsonAsync(
				$"https://nvapi.nicovideo.jp/v2/mylists/{cleanId}?sortKey=addedAt&sortOrder=desc&pageSize=1&page=1");
		}

		public NicoMylistModel(string id, dynamic json)
		{
			// json 非 null でも data または mylist が null/未定義のケース (権限不足時に
			// meta だけ返るレスポンス等) をガード。
			dynamic ml = null;
			try
			{
				if (json != null && json.IsDefined("data") && json.data != null
					&& json.data.IsDefined("mylist") && json.data.mylist != null)
				{
					ml = json.data.mylist;
				}
			}
			catch { ml = null; }

			if (ml == null)
			{
				MylistId = id;
				MylistTitle = "";
				MylistDate = DateTime.MinValue;
				MylistDescription = "";
				UserInfo = new NicoUserModel();
				return;
			}

			MylistId = id;
			MylistTitle = GetMylistTitle(DynamicUtil.S(ml, "name") ?? "");
			MylistDescription = DynamicUtil.S(ml, "description");

			// RSS lastBuildDate 相当: 最新追加アイテムの addedAt を採用
			// (nvapi v2 mylist には createdAt/updatedAt フィールドが存在しない)
			string addedAt = null;
			if (ml.IsDefined("items"))
			{
				foreach (var it in ml.items)
				{
					addedAt = DynamicUtil.S(it, "addedAt");
					break;
				}
			}
			MylistDate = string.IsNullOrEmpty(addedAt)
				? DateTime.MinValue
				: DateTimeOffset.Parse(addedAt).DateTime;

			UserInfo = new NicoUserModel();
			// owner は essential の owner と同形 (ownerType, id, name, iconUrl)。
			// owner.id がチャンネル形式 "ch..." でも SetUserInfo が正しく分岐する。
			UserInfo.SetUserInfo(
				DynamicUtil.S(ml, "owner.id"),
				DynamicUtil.S(ml, "owner.name"));

			UserInfo.AddOnPropertyChanged(this, (sender, e) =>
			{
				ThumbnailUrl = UserInfo.ThumbnailUrl;
			}, nameof(UserInfo.ThumbnailUrl), true);
		}

		private string GetMylistTitle(string value)
		{
			return ComboUtil.GetNicos("mylist_title_removes")
				.Aggregate(value, (s, c) => s.Replace(c.Display, ""));
		}

		public string MylistId
		{
			get => _MylistId;
			set => SetProperty(ref _MylistId, value);
		}
		private string _MylistId;

		public string MylistTitle
		{
			get => _MylistTitle;
			set => SetProperty(ref _MylistTitle, value);
		}
		private string _MylistTitle;

		public string MylistDescription
		{
			get => _MylistDescription;
			set => SetProperty(ref _MylistDescription, value);
		}
		private string _MylistDescription = null;

		public DateTime MylistDate
		{
			get => _MylistDate;
			set => SetProperty(ref _MylistDate, value);
		}
		private DateTime _MylistDate;

		public string ThumbnailUrl
		{
			get => _ThumbnailUrl;
			set => SetProperty(ref _ThumbnailUrl, value);
		}
		private string _ThumbnailUrl;

		public NicoUserModel UserInfo
		{
			get => _UserInfo;
			set => SetProperty(ref _UserInfo, value);
		}
		private NicoUserModel _UserInfo;
	}
}
