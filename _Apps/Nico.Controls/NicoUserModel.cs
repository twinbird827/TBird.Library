using Moviewer.Core.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TBird.Core;
using TBird.Web;

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
			using (await Locker.LockAsync(_nicknamelock))
			{
				if (_nicknames.ContainsKey(userid)) return _nicknames[userid];

				try
				{
					var url = $"https://seiga.nicovideo.jp/api/user/info?id={userid}";
					var xml = await WebUtil.GetXmlAsync(url);
					return _nicknames[userid] = (string)xml.Descendants("user")
						.SelectMany(x => x.Descendants("nickname"))
						.FirstOrDefault();
				}
				catch
				{
					return userid;
				}
			}
			/*
            <?xml version="1.0" encoding="UTF-8"?>
            <response>
                <user>
                    <id>1</id>
                    <nickname>しんの</nickname>
                </user>
            </response>
            */
		}

		private static Dictionary<string, string> _nicknames = new Dictionary<string, string>();
		private static string _nicknamelock = Locker.GetNewLockKey(typeof(NicoUserModel));
	}
}