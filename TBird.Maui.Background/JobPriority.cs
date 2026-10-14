namespace TBird.Maui.Background;

/// <summary>
/// <see cref="PriorityJobQueue{TJob, TKey}"/> でジョブの優先度を指定する。
/// 段階は High / Normal の 2 つのみ（現行 BackgroundJobQueue 仕様の踏襲）。
/// </summary>
public enum JobPriority
{
    Normal = 0,
    High = 1,
}
