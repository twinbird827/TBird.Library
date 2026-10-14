using System;

namespace TBird.Maui.Background;

/// <summary>
/// バックグラウンドジョブ基底。ジョブ種別ごとに派生する。
/// 優先度は <see cref="PriorityJobQueue{TJob, TKey}.EnqueueAsync"/> の第二引数で渡す設計のため、
/// このクラス自体は Priority プロパティを持たない（二重管理を避けるため）。
/// </summary>
public abstract class BackgroundJobBase
{
    public DateTime EnqueuedAt { get; } = DateTime.UtcNow;
    public abstract string Description { get; }
}
