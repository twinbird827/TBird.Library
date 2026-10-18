using ComicViewer.Data;
using ComicViewer.Models;
using ComicViewer.Services;
using ComicViewer.Sources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TBird.Core;
using TBird.Maui.ViewModels;

namespace ComicViewer.ViewModels;

public partial class SmbEditViewModel : ErrorAwareViewModel, IQueryAttributable
{
    private readonly SmbSourceRepository _repository;
    private readonly SmbConnectionPool _pool;
    private readonly ToastService _toast;
    private int _id;

    public SmbEditViewModel(SmbSourceRepository repository, SmbConnectionPool pool, ToastService toast)
    {
        _repository = repository;
        _pool = pool;
        _toast = toast;
    }

    [ObservableProperty] public partial string Title { get; set; } = "SMB を追加";
    [ObservableProperty] public partial bool IsEdit { get; set; }
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string NameError { get; set; } = "";
    [ObservableProperty] public partial string Host { get; set; } = "";
    [ObservableProperty] public partial string HostError { get; set; } = "";
    [ObservableProperty] public partial string PortText { get; set; } = "445";
    [ObservableProperty] public partial string PortError { get; set; } = "";
    [ObservableProperty] public partial string UserName { get; set; } = "";
    [ObservableProperty] public partial string UserNameError { get; set; } = "";
    [ObservableProperty] public partial string Password { get; set; } = "";
    [ObservableProperty] public partial string Share { get; set; } = "";
    [ObservableProperty] public partial string ShareError { get; set; } = "";
    [ObservableProperty] public partial string RootPath { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestCommand), nameof(SaveCommand))]
    public partial bool IsBusy { get; set; }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            _id = query.TryGetValue("id", out var id) ? (int)id : 0;
            if (_id == 0) return;
            var row = await _repository.GetAsync(_id);
            if (row is null) return;
            IsEdit = true;
            Title = "SMB を編集";
            Name = row.Name;
            Host = row.Host;
            PortText = row.Port.ToString();
            UserName = row.UserName;
            Share = row.Share;
            RootPath = row.RootPath;
            Password = await SmbConnectionPool.GetPasswordAsync(_id);
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
            SetError("データを読み込めませんでした");
        }
    }

    private bool CanRun() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task TestAsync()
    {
        if (Validate() is not { } source) return;
        IsBusy = true;
        try
        {
            var error = await TestConnectionAsync(source);
            if (error is null) _toast.Show("接続できました");
            else await Dialogs.AlertAsync(error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task SaveAsync()
    {
        if (Validate() is not { } source) return;
        IsBusy = true;
        try
        {
            var error = await TestConnectionAsync(source);
            if (error is not null && !await Dialogs.ConfirmAsync($"接続できませんでした（{error}）。保存しますか？", "保存")) return;

            source.Id = _id;
            await _repository.SaveAsync(source);
            try
            {
                await SecureStorage.SetAsync(SmbConnectionPool.PasswordKey(source.Id), Password);
            }
            catch (Exception ex)
            {
                MessageService.Exception(ex);
                _toast.Show("パスワードを保存できませんでした");
            }
            _pool.Remove(source.SourceKey);
            await Navigator.BackAsync();
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
            SetError("保存できませんでした");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>成功なら null、失敗なら理由。</summary>
    private async Task<string?> TestConnectionAsync(SmbSource source)
    {
        using var smb = new SmbFileSource(source, Password);
        try
        {
            await smb.TestAsync(CancellationToken.None);
            return null;
        }
        catch (Exception ex)
        {
            MessageService.Warn($"SMB test failed: {ex.Message}");
            return ex switch
            {
                SmbException { Kind: SmbErrorKind.Auth } => "ユーザー名またはパスワードが違います",
                SmbException { Kind: SmbErrorKind.ShareNotFound } or DirectoryNotFoundException => "共有またはパスが見つかりません",
                _ => "接続できませんでした",
            };
        }
    }

    private SmbSource? Validate()
    {
        var name = Name.Trim();
        var host = Host.Trim();
        var user = UserName.Trim();
        var share = Share.Trim();
        NameError = name.Length is >= 1 and <= 50 ? "" : "1〜50 文字で入力してください";
        HostError = host.Length is >= 1 and <= 253 && !host.Any(char.IsWhiteSpace) ? "" : "1〜253 文字で、空白を含まずに入力してください";
        PortError = int.TryParse(PortText.Trim(), out var port) && port is >= 1 and <= 65535 ? "" : "1〜65535 の数値を入力してください";
        UserNameError = user.Length is >= 1 and <= 256 ? "" : "1〜256 文字で入力してください";
        ShareError = share.Length is >= 1 and <= 80 && share.IndexOfAny(['\\', '/']) < 0 ? "" : "1〜80 文字で、\\ / を含まずに入力してください";
        var passwordOk = Password.Length <= 256;
        if (!passwordOk) SetError("パスワードは 256 文字以内で入力してください");
        else ClearError();

        if (NameError.Length + HostError.Length + PortError.Length + UserNameError.Length + ShareError.Length > 0 || !passwordOk) return null;
        return new SmbSource
        {
            Name = name,
            Host = host,
            Port = port,
            UserName = user,
            Share = share,
            RootPath = string.Join('/', RootPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)),
        };
    }
}
