namespace LanobeReader.Services;

/// <summary>
/// いずれかの経路(起動時/手動更新/WorkManager/前面サービス)の更新チェックで新着を検出したことを
/// 通知するメッセージ。前面に居る一覧画面が受け取り、システム通知が抑止される前面時でも
/// アプリ内一覧を即時に再読込して NEW 表示を反映するために使う。
/// <para>
/// 全経路の合流点 <see cref="UpdateCheckService"/> から WeakReferenceMessenger 経由で送る。
/// 受信側は弱参照で保持されるため明示的な購読解除は不要(画面破棄時に自動回収)。
/// </para>
/// </summary>
/// <param name="DetectedCount">新着が検出された小説数。</param>
public sealed record UpdatesDetectedMessage(int DetectedCount);
