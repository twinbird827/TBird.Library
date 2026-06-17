namespace LanobeReader.Platforms.Android;

/// <summary>
/// 実機での通知信頼性テスト用のデバッグ設定。値は DEBUG ビルドでのみ参照され、
/// Release ビルドには一切影響しない（参照箇所が #if DEBUG でガードされている）。
/// </summary>
internal static class DebugSchedulingConfig
{
    /// <summary>
    /// 0 より大きいとき、更新チェックアラームの発火間隔を「時間」ではなく、この「分」で上書きする。
    /// 例: 15 にすると約15分ごとに発火。<b>テスト後は必ず 0 に戻すこと。</b>
    /// 注意: Doze 中の AllowWhileIdle / exact アラームはシステムにより最短 ~9〜10 分へ
    /// レート制限されるため、9 分未満を指定しても実発火間隔はそれ以上空く。
    /// </summary>
    public static readonly int AlarmOverrideMinutes = 0;
}
