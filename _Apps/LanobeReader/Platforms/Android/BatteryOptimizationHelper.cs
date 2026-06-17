using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Microsoft.Maui.Storage;
using TBird.Core;

namespace LanobeReader.Platforms.Android;

/// <summary>
/// 電池最適化(Doze / OEM の積極的な省電力)からの除外をユーザに一度だけ依頼するヘルパ。
/// OEM 実機ではこれが WorkManager 定期実行が動かない最大要因となるため、緩和策として提供。
/// ※効果は OEM により限定的。Google Play 配布時は REQUEST_IGNORE_BATTERY_OPTIMIZATIONS の
///   ポリシー制限があるが、本アプリはサイドロード運用のため利用可能。
/// </summary>
public static class BatteryOptimizationHelper
{
	private const string AskedKey = "battery_opt_prompted";

	/// <summary>
	/// 未除外かつ未プロンプトであれば、説明ダイアログ→システムの除外要求ダイアログを一度だけ表示する。
	/// </summary>
	public static void PromptOnceIfNeeded(Activity activity)
	{
		if (Build.VERSION.SdkInt < BuildVersionCodes.M) return;
		if (Preferences.Get(AskedKey, false)) return;

		var pm = activity.GetSystemService(Context.PowerService) as PowerManager;
		var pkg = activity.PackageName;
		if (pm is null || pkg is null) return;
		if (pm.IsIgnoringBatteryOptimizations(pkg)) return;

		MainThread.BeginInvokeOnMainThread(async () =>
		{
			try
			{
				// Shell 未準備(起動直後)なら今回は見送り、次回 OnResume で再試行する。
				if (Shell.Current is null) return;
				if (Preferences.Get(AskedKey, false)) return;
				Preferences.Set(AskedKey, true);

				var ok = await Shell.Current.DisplayAlert(
					"バックグラウンド更新の許可",
					"新着をバックグラウンドで確実に通知するため、電池の最適化を無効にすることを推奨します。設定画面を開きますか？",
					"設定を開く", "後で");
				if (!ok) return;

				var intent = new Intent(
					Settings.ActionRequestIgnoreBatteryOptimizations,
					global::Android.Net.Uri.Parse($"package:{pkg}"));
				intent.SetFlags(ActivityFlags.NewTask);
				activity.StartActivity(intent);
			}
			catch (Exception ex)
			{
				MessageService.Warn($"Battery optimization prompt failed: {ex.Message}");
			}
		});
	}
}
