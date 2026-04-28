using Moviewer.Core.Controls;
using Moviewer.Nico.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using TBird.Core;

namespace Moviewer.Nico.Controls
{
	public class NicoUserModel : UserModel
	{
		public override void SetUserInfo(string id, string name = null, string url = null)
		{
			if (!id.StartsWith("ch"))
			{
				var url0 = id;
				var url1 = "https://secure-dcdn.cdn.nimg.jp/nicoaccount/usericon";
				var url2 = 4 < url0.Length ? url0.Left(url0.Length - 4) : "0";
				var url3 = url0;
				url = $"{url1}/{url2}/{url3}.jpg";
			}
			else
			{
				url = $"https://secure-dcdn.cdn.nimg.jp/comch/channel-icon/128x128/{id}.jpg";
			}

			base.SetUserInfo(id, name, url);
		}

		public static async Task<NicoUserModel> GetUserInfo(string id)
		{
			var info = new NicoUserModel();

			info.SetUserInfo(id, await GetNickname(id));

			return info;
		}

		private static async Task<string> GetNickname(string userid)
		{
			using (var _lock = Locker.Create(_nicknamelock))
			using (await _lock.LockAsync())
			{
				if (_nicknames.ContainsKey(userid)) return _nicknames[userid];

				try
				{
					var json = await NicoUtil.GetNvapiJsonAsync(
						$"https://nvapi.nicovideo.jp/v1/users/{userid}");
					if (json == null) return userid;

					var nickname = DynamicUtil.S(json, "data.user.nickname");

					// 毒キャッシュ防止: nickname が null/空の場合は _nicknames に保存しない。
					// 保存してしまうと次回以降ずっと null/空が返り続ける。
					if (string.IsNullOrEmpty(nickname)) return userid;
					return _nicknames[userid] = nickname;
				}
				catch
				{
					return userid;
				}
			}
		}

		private static Dictionary<string, string> _nicknames = new Dictionary<string, string>();
		private static string _nicknamelock = typeof(NicoUserModel).FullName;
	}
}