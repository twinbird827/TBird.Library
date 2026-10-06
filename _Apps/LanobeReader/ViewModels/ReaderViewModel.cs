using TBird.Maui.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services;
using LanobeReader.Services.Database;
using LanobeReader.Services.Network;

namespace LanobeReader.ViewModels;

public partial class ReaderViewModel(
    EpisodeRepository episodeRepo,
    EpisodeContentService contentService,
    AppSettingsRepository settingsRepo,
    NetworkPolicyService networkPolicy) : ErrorAwareViewModel, IQueryAttributable
{
    private int _novelDbId;
    private int _currentEpisodeId;
    private int _siteType;
    private string _siteNovelId = string.Empty;
    // 読み込み時に解決した前話・次話の DB Id。前へ/次へで再クエリしないために保持する。
    private int? _prevEpisodeId;
    private int? _nextEpisodeId;

    [ObservableProperty]
    public partial string EpisodeTitle { get; set; } = string.Empty;

    private string _episodeContent = string.Empty;

    [ObservableProperty]
    public partial string EpisodeHtml { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial double FontSize { get; set; } = 16;

    [ObservableProperty]
    public partial int BackgroundThemeIndex { get; set; }

    [ObservableProperty]
    public partial int LineSpacingIndex { get; set; } = SettingsKeys.DEFAULT_LINE_SPACING;

    [ObservableProperty]
    public partial bool IsVerticalWriting { get; set; }

    [ObservableProperty]
    public partial ReaderCssState? ReaderCss { get; set; }

    [ObservableProperty]
    public partial bool IsCurrentEpisodeFavorite { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrevEpisodeCommand))]
    public partial bool HasPrevEpisode { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextEpisodeCommand))]
    public partial bool HasNextEpisode { get; set; }

    [ObservableProperty]
    public partial bool AutoMarkReadEnabled { get; set; } = true;

    private Episode? _episode;

    partial void OnIsVerticalWritingChanged(bool value)
    {
        if (!string.IsNullOrEmpty(_episodeContent))
        {
            RefreshHtml();
        }
    }

    partial void OnFontSizeChanged(double value) => UpdateCssStateIfReady();
    partial void OnBackgroundThemeIndexChanged(int value) => UpdateCssStateIfReady();
    partial void OnLineSpacingIndexChanged(int value) => UpdateCssStateIfReady();

    private void UpdateCssStateIfReady()
    {
        if (ReaderCss is not null)
        {
            ReaderCss = BuildCssState();
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("novelId", out var nid)) int.TryParse(nid?.ToString(), out _novelDbId);
        if (query.TryGetValue("episodeId", out var eid)) int.TryParse(eid?.ToString(), out _currentEpisodeId);
        if (query.TryGetValue("siteType", out var st)) int.TryParse(st?.ToString(), out _siteType);
        if (query.TryGetValue("siteNovelId", out var snid)) _siteNovelId = snid?.ToString() ?? "";

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        await LoadEpisodeAsync(_currentEpisodeId);
    }

    private async Task LoadSettingsAsync()
    {
        FontSize = await settingsRepo.GetIntValueAsync(SettingsKeys.FONT_SIZE_SP, SettingsKeys.DEFAULT_FONT_SIZE_SP);
        BackgroundThemeIndex = await settingsRepo.GetIntValueAsync(SettingsKeys.BACKGROUND_THEME, SettingsKeys.DEFAULT_BACKGROUND_THEME);
        LineSpacingIndex = await settingsRepo.GetIntValueAsync(SettingsKeys.LINE_SPACING, SettingsKeys.DEFAULT_LINE_SPACING);
        var vertical = await settingsRepo.GetIntValueAsync(SettingsKeys.VERTICAL_WRITING, SettingsKeys.DEFAULT_VERTICAL_WRITING);
        AutoMarkReadEnabled = await settingsRepo.GetIntValueAsync(
            SettingsKeys.AUTO_MARK_READ_ENABLED,
            SettingsKeys.DEFAULT_AUTO_MARK_READ_ENABLED) == 1;

        IsVerticalWriting = vertical == 1;
        ReaderCss = BuildCssState();
    }

    public Task ReloadSettingsAsync() => LoadSettingsAsync();

    private void RefreshHtml()
    {
        var state = BuildCssState();
        // EpisodeHtml 先・ReaderCss 後: 古い document への無駄な JS 適用を防ぐ
        EpisodeHtml = ReaderHtmlBuilder.Build(_episodeContent, state, IsVerticalWriting);
        ReaderCss = state;
    }

    private ReaderCssState BuildCssState() => new(
        FontSizePx: FontSize,
        LineSpacingIndex: LineSpacingIndex,
        BackgroundThemeIndex: BackgroundThemeIndex);

    private async Task LoadEpisodeAsync(int episodeId)
    {
        IsLoading = true;
        ClearError();
        // 失敗時に前話の本文・タイトルが残るのを防ぐためここで一括クリアする。
        // 成功時は下で上書きされる。
        _episodeContent = string.Empty;
        EpisodeTitle = string.Empty;
        EpisodeHtml = string.Empty;
        _prevEpisodeId = null;
        _nextEpisodeId = null;
        HasPrevEpisode = false;
        HasNextEpisode = false;
        try
        {
            _episode = await episodeRepo.GetByIdAsync(episodeId);
            if (_episode is null) return;

            var prev = await episodeRepo.GetPreviousEpisodeAsync(_novelDbId, _episode.EpisodeNo);
            var next = await episodeRepo.GetNextEpisodeAsync(_novelDbId, _episode.EpisodeNo);
            // 本文取得の前に入れる。本文取得に失敗しても、この話の前後へ移れるようにするため。
            _prevEpisodeId = prev?.Id;
            _nextEpisodeId = next?.Id;
            HasPrevEpisode = prev is not null;
            HasNextEpisode = next is not null;

            // 本文取得・キャッシュ命中判定・cacheable 保存は EpisodeContentService に集約。cacheable 契約は
            // ファサード内で消費され、ここには漏れない(位置依存フォールバックの誤話本文を恒久キャッシュしない
            // 不変条件は中央化済み)。networkAllowed=IsOnline で未命中かつオフラインなら null を受ける。
            // NetworkPolicyService.IsOnline は INetworkPolicy 経由で Connectivity.Current を例外ガード付きで
            // 包む(MainActivity 起動前/特定端末状態で当該 API が throw するため)。INetworkPolicy をアプリ層 VM が
            // 直接 DI で受け取ることは TBird.Maui.Background/CLAUDE.md で禁止されているため、消費アプリ側
            // ラッパー(NetworkPolicyService)経由で参照する。
            var content = await contentService.GetContentAsync(
                episodeId, (SiteType)_siteType, _siteNovelId, _episode.EpisodeNo, _episode.SiteEpisodeId,
                networkAllowed: networkPolicy.IsOnline);
            if (content is null)
            {
                // キャッシュ未命中かつオフライン。ユーザは目次/戻るボタンで自分で抜ける(自動遷移は採用しない)。
                SetError("オフラインのため表示できません。キャッシュもありません");
                return;
            }

            EpisodeTitle = _episode.Title;
            _episodeContent = content;
            IsCurrentEpisodeFavorite = _episode.IsFavorite;

            RefreshHtml();
        }
        catch (TaskCanceledException)
        {
            SetError("タイムアウトしました");
        }
        catch (HttpRequestException ex)
        {
            SetError($"本文の取得に失敗しました（HTTPエラー: {ex.Message}）");
        }
        catch (Exception ex)
        {
            SetError($"本文の取得に失敗しました（{ex.Message}）");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoPrev))]
    private async Task PrevEpisodeAsync()
    {
        if (_prevEpisodeId is not int prevId) return;
        _currentEpisodeId = prevId;
        await LoadEpisodeAsync(prevId);
    }

    private bool CanGoPrev() => HasPrevEpisode;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextEpisodeAsync()
    {
        if (_nextEpisodeId is not int nextId) return;
        _currentEpisodeId = nextId;
        await LoadEpisodeAsync(nextId);
    }

    private bool CanGoNext() => HasNextEpisode;

    [RelayCommand]
    private async Task NavigateToTocAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (_episode is null) return;
        var newValue = !IsCurrentEpisodeFavorite;
        await episodeRepo.SetFavoriteAsync(_episode.Id, newValue);
        _episode.IsFavorite = newValue;
        IsCurrentEpisodeFavorite = newValue;
    }

    [RelayCommand]
    private Task MarkAsReadAsync() => ApplyMarkAsReadAsync();

    [RelayCommand]
    private Task MarkAsReadFromAutoAsync()
    {
        // 自動経路 (WebView read-end)。設定 OFF なら no-op。
        if (!AutoMarkReadEnabled) return Task.CompletedTask;
        return ApplyMarkAsReadAsync();
    }

    private async Task ApplyMarkAsReadAsync()
    {
        if (_episode is null) return;
        // N-2 仕様: 既読でも N+1 以降の未読化を走らせるため IsRead チェックは外す。
        await episodeRepo.SetReadStateUpToAsync(_novelDbId, _episode.EpisodeNo);
        _episode.IsRead = true;
    }
}
