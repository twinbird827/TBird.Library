using System;
using TBird.Core;

namespace Moviewer.Tube.Core
{
	public class TubeSetting : JsonBase<TubeSetting>
	{
		private const string _path = @"lib\tube-setting.json";

		public static TubeSetting Instance
		{
			get => _Instance = _Instance ?? new TubeSetting();
		}
		private static TubeSetting _Instance;

		public TubeSetting() : base(_path)
		{
			if (!Load())
			{

			}
		}

		public string APIKEY
		{
			get => GetProperty(_APIKEY);
			set => SetProperty(ref _APIKEY, value);
		}
		private string _APIKEY;

		public string ClientId
		{
			get => GetProperty(_ClientId);
			set => SetProperty(ref _ClientId, value);
		}
		private string _ClientId;

		public string ClientSecret
		{
			get => GetProperty(_ClientSecret);
			set => SetProperty(ref _ClientSecret, value);
		}
		private string _ClientSecret;

		public string AccessToken
		{
			get => GetProperty(_AccessToken);
			set => SetProperty(ref _AccessToken, value);
		}
		private string _AccessToken;

		public string RefreshToken
		{
			get => GetProperty(_RefreshToken);
			set => SetProperty(ref _RefreshToken, value);
		}
		private string _RefreshToken;

		public DateTime RefreshDate
		{
			get => GetProperty(_RefreshDate);
			set => SetProperty(ref _RefreshDate, value);
		}
		private DateTime _RefreshDate;

		public string TubePopularCategory
		{
			get => GetProperty(_TubePopularCategory);
			set => SetProperty(ref _TubePopularCategory, value);
		}
		private string _TubePopularCategory;

	}
}