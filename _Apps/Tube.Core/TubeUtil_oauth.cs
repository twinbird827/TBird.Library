using Codeplex.Data;
using Moviewer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using TBird.Core;
using TBird.Web;
using TBird.Wpf.Controls;

namespace Moviewer.Tube.Core
{
	public static partial class TubeUtil
	{
		public static string GetAPIKEY()
		{

			if (string.IsNullOrEmpty(TubeSetting.Instance.APIKEY))
			{
				using (var vm = new WpfMessageInputViewModel(AppConst.H_InputAPIKEY, AppConst.M_InputAPIKEY, AppConst.L_APIKEY, true))
				{
					if (vm.ShowDialog(() => new WpfMessageInputWindow()))
					{
						TubeSetting.Instance.APIKEY = vm.Value;
						TubeSetting.Instance.Save();
					}
				}
			}
			return TubeSetting.Instance.APIKEY;
		}

		public static async Task<string> GetAccessToken()
		{
			if (TubeSetting.Instance.RefreshDate < DateTime.Now)
			{
				TubeSetting.Instance.AccessToken = null;
				TubeSetting.Instance.Save();
			}

			SetClientIdAndSecret();

			if (string.IsNullOrEmpty(TubeSetting.Instance.RefreshToken))
			{
				await SetToken();
			}

			if (string.IsNullOrEmpty(TubeSetting.Instance.AccessToken))
			{
				await RefreshToken();
			}

			return TubeSetting.Instance.AccessToken;
		}

		private static void SetClientIdAndSecret()
		{
			if (string.IsNullOrEmpty(CoreUtil.Nvl(TubeSetting.Instance.ClientId, TubeSetting.Instance.ClientSecret)))
			{
				using (var vm = new TubeOAuthViewModel())
				{
					if (!vm.ShowDialog(() => new TubeOAuthWindow()))
					{
						SetClientIdAndSecret();
					}
				}
			}
		}

		private static async Task SetToken()
		{
			using (var listener = new WebListener())
			{
				var oauthurl = "https://accounts.google.com/o/oauth2/v2/auth";
				var dic = new Dictionary<string, string>()
				{
					{ "client_id", TubeSetting.Instance.ClientId },
                    //{ "redirect_uri", $@"http://localhost:{listener.Port}" },
                    { "redirect_uri", listener.Prefix },
					{ "response_type", "code" },
					{ "scope", scopes.GetString(" ")},
					{ "approval_prompt", "force" },
					{ "access_type", "offline" },
				};
				WebUtil.Browse(WebUtil.GetUrl(oauthurl, dic));

				var context = await listener.GetContextAsync();
				var request = context.Request;
				var parameters = request.RawUrl.Mid(2).Split('&')
					.Select(x => x.Split('='))
					.ToDictionary(x => x[0], x => HttpUtility.HtmlDecode(x[1]));

				using (var response = context.Response)
				{
					//var body = @"<html><body onload=""open(location, '_self').close();""></body></html>";
					await response.WriteAutoClose();
				}
				await SetToken(parameters.Get("code"), listener.Prefix);
			}
		}

		private static readonly string[] scopes = new string[]
		{
			"https://www.googleapis.com/auth/youtube",
			"https://www.googleapis.com/auth/youtube.channel-memberships.creator",
			"https://www.googleapis.com/auth/youtube.force-ssl",
			"https://www.googleapis.com/auth/youtube.readonly",
			"https://www.googleapis.com/auth/youtube.upload",
			"https://www.googleapis.com/auth/youtubepartner",
			"https://www.googleapis.com/auth/youtubepartner-channel-audit"
		};

		private static async Task SetToken(string authorizationcode, string prefix)
		{
			var url = "https://oauth2.googleapis.com/token";
			//var dic = new
			//{
			//    client_id = TubeSetting.Instance.ClientId,
			//    client_secret = TubeSetting.Instance.ClientSecret,
			//    code = authorizationcode,
			//    grant_type = "authorization_code",
			//    redirect_uri = @"http://localhost:50000",
			//};
			var dic = new Dictionary<string, string>
			{
				{ "client_id" ,TubeSetting.Instance.ClientId },
				{ "client_secret", TubeSetting.Instance.ClientSecret },
				{ "code", authorizationcode },
				{ "grant_type", "authorization_code" },
				{ "redirect_uri", prefix },
			};
			var urlparameter = dic.Select(x => $"{x.Key}={HttpUtility.UrlEncode(x.Value)}").GetString("&");

			dynamic json = await PostStringAsync(url, dic);

			TubeSetting.Instance.AccessToken = DynamicUtil.S(json, "access_token");
			TubeSetting.Instance.RefreshToken = DynamicUtil.S(json, "refresh_token");
			TubeSetting.Instance.RefreshDate = DateTime.Now.AddSeconds(DynamicUtil.I(json, "expires_in") * 0.75);
			TubeSetting.Instance.Save();
		}

		private static async Task RefreshToken()
		{
			var url = "https://oauth2.googleapis.com/token";
			var dic = new Dictionary<string, string>()
			{
				{ "client_id", TubeSetting.Instance.ClientId },
				{ "client_secret", TubeSetting.Instance.ClientSecret },
				{ "grant_type", "refresh_token" },
				{ "refresh_token", TubeSetting.Instance.RefreshToken },
			};

			dynamic json = await PostStringAsync(url, dic);

			TubeSetting.Instance.AccessToken = DynamicUtil.S(json, "access_token");
			TubeSetting.Instance.RefreshDate = DateTime.Now.AddSeconds(DynamicUtil.I(json, "expires_in") * 0.75);
			TubeSetting.Instance.Save();
		}

		private static async Task<dynamic> PostStringAsync(string url, Dictionary<string, string> dic)
		{
			var response = await WebUtil.PostStringAsync(url, WebUtil.ToParameter(dic), @"application/x-www-form-urlencoded").TryCatch();
			dynamic json = DynamicJson.Parse(response);
			return json;
		}
	}
}