using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewReleaseChecker.App.Models;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using TBird.Core;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>
/// 巻一覧の一括選択・一括操作の共通基底（F-015）。
/// 選択モードの定型処理（モード開始/終了・行トグル・全選択・件数・一括適用）を集約し、
/// 差分（対象コレクション・永続 Book への解決・再読込・通常タップ遷移）は抽象フックで各 VM が提供する。
/// CollectionView 標準の SelectionMode/SelectedItems は使わず、行アイテムの IsSelected をトグルする方式
/// （既存の行ジェスチャ→VM コマンド経路を流用するため。プラン §2.1）。
/// </summary>
public abstract partial class SelectableBookListViewModel : ObservableObject
{
    protected readonly IBookRepository BookRepo;
    protected SelectableBookListViewModel(IBookRepository bookRepo) => BookRepo = bookRepo;

    [ObservableProperty] private bool _isSelectionMode;
    [ObservableProperty] private int _selectedCount;

    /// <summary>選択対象のコレクション（各 VM の Books / Items を返す）。</summary>
    protected abstract ObservableCollection<BookListItem> SelectionItems { get; }

    /// <summary>行アイテムを永続 Book に解決する（永続=BookId 取得 / 非永続=EnsurePersist）。null は対象外。</summary>
    protected abstract Task<Book?> ResolveAsync(BookListItem item);

    /// <summary>一括適用後の再読込（不要な画面は Task.CompletedTask）。</summary>
    protected abstract Task ReloadAsync();

    /// <summary>通常タップ時の遷移（各 VM の巻詳細遷移）。</summary>
    protected abstract Task OpenBookAsync(BookListItem item);

    [RelayCommand] private void EnterSelectionMode() => IsSelectionMode = true;

    [RelayCommand]
    private void ExitSelectionMode()
    {
        foreach (var it in SelectionItems) it.IsSelected = false;
        SelectedCount = 0;
        IsSelectionMode = false;
    }

    [RelayCommand]
    private void RowTapped(BookListItem? item)
    {
        if (item is null) return;
        if (IsSelectionMode)
        {
            item.IsSelected = !item.IsSelected;
            SelectedCount = SelectionItems.Count(i => i.IsSelected);
        }
        else _ = OpenBookAsync(item);
    }

    [RelayCommand]
    private void SelectAll()
    {
        var allSelected = SelectionItems.Count > 0 && SelectionItems.All(i => i.IsSelected);
        foreach (var it in SelectionItems) it.IsSelected = !allSelected;
        SelectedCount = SelectionItems.Count(i => i.IsSelected);
    }

    /// <summary>選択巻に mutate を適用：解決(EnsurePersist含む)→mutate→UpdateFlags→再読込→モード解除。</summary>
    protected async Task ApplyToSelectedAsync(Action<Book> mutate)
    {
        var targets = SelectionItems.Where(i => i.IsSelected).ToList();
        if (targets.Count == 0) { ExitSelectionMode(); return; }
        try
        {
            foreach (var item in targets)
            {
                var b = await ResolveAsync(item);
                if (b is null) continue;
                mutate(b);
                await BookRepo.UpdateFlagsAsync(b);
            }
        }
        catch (Exception ex) { MessageService.Exception(ex); }
        finally
        {
            ExitSelectionMode();
            await ReloadAsync();
        }
    }
}
