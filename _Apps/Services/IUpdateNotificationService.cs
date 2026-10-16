using LanobeReader.Models;

namespace LanobeReader.Services;

/// <summary>
/// 新着更新の通知ロジックを 1 箇所に集約する抽象。全検出経路(フォアグラウンドの起動時
/// CheckAllAsync・バックグラウンドの WorkManager / 前面サービス)から同一実装を呼び、
/// 経路ごとの通知差異・ロジック重複を無くすために導入。
///
/// 役割分担: 新着の提示はアプリの前面/背面で異なる。
/// - 前面時(<c>AppForegroundTracker.IsForeground</c>): システム通知は出さない。アプリ内一覧の
///   NEW 表示で示す。前面で通知すると直後の <c>MainActivity</c> の CancelAll(バッジクリア)と
///   打ち消し合うため、実装側で前面時は早期 return する。
/// - 背面時: システム通知を投稿する(バックグラウンド検出の確実な通知はこの経路が担う)。
/// この設計上、起動時(コールドスタート)の検出は通常 NEW 表示で示され通知は出ない。起動直後に
/// アプリが背面化した稀なケースのみ、起動時経路からも通知が出る。
/// Android 実装が NotificationHelper を呼ぶ。
/// </summary>
public interface IUpdateNotificationService
{
	/// <summary>
	/// 検出された更新の一覧について新着通知を表示する。空なら何もしない。
	/// </summary>
	Task ShowUpdatesAsync(IReadOnlyList<(Novel novel, int newEpisodeCount)> updates);
}
