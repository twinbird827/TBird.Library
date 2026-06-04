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
    private readonly IUserNotifier _notifier;

    protected SelectableBookListViewModel(IBookRepository bookRepo, IUserNotifier notifier)
    {
        BookRepo = bookRepo;
        _notifier = notifier;
    }

    [ObservableProperty] private bool _isSelectionMode;
    [ObservableProperty] private int _selectedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectAllLabel))]
    private bool _allSelected;

    /// <summary>「全選択」⇄「全解除」トグルボタンの表示文言。</summary>
    public string SelectAllLabel => AllSelected ? "全解除" : "全選択";

    /// <summary>選択対象のコレクション（各 VM の Books / Items を返す）。</summary>
    protected abstract ObservableCollection<BookListItem> SelectionItems { get; }

    /// <summary>
    /// 行アイテムを永続 Book に解決する。null は対象外（スキップ）。
    /// <paramref name="createIfMissing"/>=true は未永続巻を必要なら INSERT（付与系）、
    /// false は既存の永続巻のみ返す（解除系。未永続巻に無駄な孤児行を作らない）。
    /// </summary>
    protected abstract Task<Book?> ResolveAsync(BookListItem item, bool createIfMissing);

    /// <summary>一括適用後の再読込（不要な画面は Task.CompletedTask）。</summary>
    protected abstract Task ReloadAsync();

    /// <summary>通常タップ時の遷移（各 VM の巻詳細遷移）。</summary>
    protected abstract Task OpenBookAsync(BookListItem item);

    [RelayCommand(CanExecute = nameof(CanEnterSelectionMode))]
    private void EnterSelectionMode() => IsSelectionMode = true;

    /// <summary>選択開始は対象が1件以上ある時のみ可能（空一覧では「選択」を無効化）。</summary>
    private bool CanEnterSelectionMode() => SelectionItems.Count > 0;

    /// <summary>一覧の再構築後に呼び、選択開始可否（空一覧での無効化）を再評価する（F-015）。</summary>
    protected void NotifyListChanged() => EnterSelectionModeCommand.NotifyCanExecuteChanged();

    [RelayCommand] private void ExitSelectionMode() => ResetSelection();

    /// <summary>
    /// 選択状態をクリアして選択モードを抜ける。一覧の再構築（並替/絞込/タブ切替）や画面再表示時にも呼び、
    /// Singleton VM でのモード残留・件数/ラベルの陳腐化を防ぐ（F-015）。
    /// </summary>
    protected void ResetSelection()
    {
        foreach (var it in SelectionItems) it.IsSelected = false;
        SelectedCount = 0;
        AllSelected = false;
        IsSelectionMode = false;
    }

    // 通常タップの遷移は await する（async RelayCommand の既定で実行中は再入不可となり、連打による二重遷移を防ぐ）。
    [RelayCommand]
    private async Task RowTapped(BookListItem? item)
    {
        if (item is null) return;
        if (IsSelectionMode)
        {
            item.IsSelected = !item.IsSelected;
            RefreshSelectionState();
        }
        else await OpenBookAsync(item);
    }

    [RelayCommand]
    private void SelectAll()
    {
        var selectAll = !(SelectionItems.Count > 0 && SelectionItems.All(i => i.IsSelected));
        foreach (var it in SelectionItems) it.IsSelected = selectAll;
        RefreshSelectionState();
    }

    /// <summary>選択件数・全選択状態（ボタン文言）を現在の IsSelected から再計算する。</summary>
    private void RefreshSelectionState()
    {
        SelectedCount = SelectionItems.Count(i => i.IsSelected);
        AllSelected = SelectionItems.Count > 0 && SelectedCount == SelectionItems.Count;
    }

    /// <summary>
    /// 選択巻に mutate を適用：解決→mutate→UpdateFlags→再読込→モード解除。
    /// <paramref name="createIfMissing"/>=true（付与系）は未永続巻を必要なら INSERT、false（解除系）は既存巻のみ対象。
    /// </summary>
    /// <remarks>各巻は逐次適用で原子的ではない。失敗した巻はスキップして残りを継続適用し、失敗件数を再読込後にトースト表示する。</remarks>
    protected async Task ApplyToSelectedAsync(Action<Book> mutate, bool createIfMissing = true)
    {
        // 付与系/解除系は別コマンド（AsyncRelayCommand）のため既定の単一コマンド再入抑止が効かず、
        // 適用中に別系統ボタンを連打すると同一選択へ複数操作が重複適用されうる。再入ガードで防ぐ。
        if (_isApplyingBulk) return;

        var targets = SelectionItems.Where(i => i.IsSelected).ToList();
        if (targets.Count == 0) { ResetSelection(); return; }
        _isApplyingBulk = true;
        var failCount = 0;
        try
        {
            foreach (var item in targets)
            {
                // 1件の失敗で全体を止めず、残りは継続試行する（途中中断による未着手をなくす）。
                try
                {
                    var b = await ResolveAsync(item, createIfMissing);
                    if (b is null) continue;
                    mutate(b);
                    await BookRepo.UpdateFlagsAsync(b);
                }
                catch (Exception ex)
                {
                    failCount++;
                    MessageService.Exception(ex); // logcat/ファイルへ記録（失敗件数は finally でまとめて通知）。
                }
            }
        }
        finally
        {
            _isApplyingBulk = false;
            ResetSelection();
            await ReloadAsync();
            // 前景の手動操作なので、失敗があればユーザーにもトーストで知らせる（無言の部分適用を避ける。要件 §6.7）。
            if (failCount > 0) await _notifier.ShowToastAsync($"一括操作中に{failCount}件失敗しました");
        }
    }

    /// <summary>一括適用中の再入ガード（UI バインドしないため非 ObservableProperty）。</summary>
    private bool _isApplyingBulk;
}
