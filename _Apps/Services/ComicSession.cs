using ComicViewer.Models;

namespace ComicViewer.Services;

/// <summary>フォルダ一覧からビューアへ渡す、同じフォルダの漫画グループ（前後の漫画の決定に使う）。</summary>
public class ComicSession
{
    public IReadOnlyList<ComicRef> Siblings { get; set; } = Array.Empty<ComicRef>();
}
