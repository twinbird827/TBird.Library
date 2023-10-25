using TBird.Core;

namespace Moviewer.Core
{
	public static class AppConst
	{
		/// <summary>ﾀﾞｳﾝﾛｰﾄﾞ完了通知</summary>
		public static string H_CompleteDownload { get; } = Lang.Instance.Get();

		/// <summary>ﾀﾞｳﾝﾛｰﾄﾞ失敗通知</summary>
		public static string H_FailedDownload { get; } = Lang.Instance.Get();

		/// <summary>Youtube APIｷｰ入力</summary>
		public static string H_InputAPIKEY { get; } = Lang.Instance.Get();

		/// <summary>お気に入り追加</summary>
		public static string L_AddFavorite { get; } = Lang.Instance.Get();

		/// <summary>ﾃﾝﾎﾟﾗﾘ追加</summary>
		public static string L_AddTemporary { get; } = Lang.Instance.Get();

		/// <summary>APIｷｰ</summary>
		public static string L_APIKEY { get; } = Lang.Instance.Get();

		/// <summary>お気に入り削除</summary>
		public static string L_DelFavorite { get; } = Lang.Instance.Get();

		/// <summary>URL or ID</summary>
		public static string L_UrlOrId { get; } = Lang.Instance.Get();

		/// <summary>ﾃﾝﾎﾟﾗﾘに追加する情報を入力してください。</summary>
		public static string M_AddTemporary { get; } = Lang.Instance.Get();

		/// <summary>{0}のﾀﾞｳﾝﾛｰﾄﾞが完了しました。</summary>
		public static string M_CompleteDownload { get; } = Lang.Instance.Get();

		/// <summary>何らかの原因で{0}のﾀﾞｳﾝﾛｰﾄﾞが失敗しました。</summary>
		public static string M_FailedDownload { get; } = Lang.Instance.Get();

		/// <summary>Youtube APIｷｰを入力してください。</summary>
		public static string M_InputAPIKEY { get; } = Lang.Instance.Get();

	}
}