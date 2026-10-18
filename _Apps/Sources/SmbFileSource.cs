using System.Net;
using System.Net.Sockets;
using ComicViewer.Models;
using SMBLibrary;
using SMBLibrary.Client;
using TBird.Core;
using FileAttributes = SMBLibrary.FileAttributes;

namespace ComicViewer.Sources;

/// <summary>
/// 1 つの SMB 読み込み元への接続。SMBLibrary クライアントのスレッド安全性が未確認のため、
/// 各リクエストは SourceKey ごとの Locker で直列化する。
/// </summary>
public class SmbFileSource : TBirdObject, IFileSource
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private const int ResponseTimeoutMs = 10000;

    private readonly SmbSource _source;
    private readonly string _password;
    private readonly Locker _locker;
    private PortSmb2Client? _client;
    private ISMBFileStore? _store;

    public SmbFileSource(SmbSource source, string password)
    {
        _source = source;
        _password = password;
        _locker = Locker.Create($"smb:{SourceKey}");
    }

    public string SourceKey => _source.SourceKey;

    public string DisplayName => _source.Name;

    public bool IsRemote => true;

    /// <summary>再接続のたびに増える。開いていたハンドルが古い接続のものか判定するのに使う。</summary>
    internal int Generation { get; private set; }

    public Task<IReadOnlyList<FileEntry>> ListAsync(string path, CancellationToken ct) => ExecuteAsync<IReadOnlyList<FileEntry>>(store =>
    {
        var handle = OpenHandle(store, path, directory: true);
        try
        {
            var status = store.QueryDirectory(out var items, handle, "*", FileInformationClass.FileDirectoryInformation);
            if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_NO_MORE_FILES) Throw(status, path);
            return items.OfType<FileDirectoryInformation>()
                .Where(x => x.FileName is not "." and not "..")
                .Select(x => new FileEntry(
                    x.FileName,
                    x.FileAttributes.HasFlag(FileAttributes.Directory),
                    x.EndOfFile,
                    new DateTimeOffset(DateTime.SpecifyKind(x.LastWriteTime, DateTimeKind.Utc)).ToUnixTimeMilliseconds()))
                .ToList();
        }
        finally
        {
            store.CloseFile(handle);
        }
    }, ct);

    public async Task<Stream> OpenReadAsync(string path, CancellationToken ct)
    {
        var (handle, generation, length) = await ExecuteAsync(store =>
        {
            var h = OpenHandle(store, path, directory: false);
            var status = store.GetFileInformation(out var info, h, FileInformationClass.FileStandardInformation);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                store.CloseFile(h);
                Throw(status, path);
            }
            return (h, Generation, ((FileStandardInformation)info).EndOfFile);
        }, ct).ConfigureAwait(false);
        return new SmbReadStream(this, path, handle, generation, length);
    }

    /// <summary>接続 → ログイン → 共有 → RootPath の存在確認。失敗は SmbException / DirectoryNotFoundException。</summary>
    public Task TestAsync(CancellationToken ct) => ExecuteAsync(store =>
    {
        store.CloseFile(OpenHandle(store, "", directory: true));
        return true;
    }, ct);

    internal async Task<T> ExecuteAsync<T>(Func<ISMBFileStore, T> operation, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using (await _locker.LockAsync().ConfigureAwait(false))
            {
                ThrowIfDisposed();
                if (_store is null || _client?.IsConnected != true)
                {
                    DisconnectCore();
                    await ConnectCoreAsync(ct).ConfigureAwait(false);
                }
                try
                {
                    var store = _store!;
                    return await Task.Run(() => operation(store), ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt == 0 && IsDisconnected(ex))
                {
                    MessageService.Warn($"SMB disconnected, reconnecting: {SourceKey} {ex.Message}");
                    DisconnectCore();
                }
            }
        }
    }

    // ZipArchive の Stream.Read は同期のため。UI スレッドから呼ばないこと。
    internal T Execute<T>(Func<ISMBFileStore, T> operation) => ExecuteAsync(operation, CancellationToken.None).GetAwaiter().GetResult();

    internal object OpenHandle(ISMBFileStore store, string path, bool directory)
    {
        var status = store.CreateFile(out var handle, out _, ToSmbPath(path),
            directory ? AccessMask.GENERIC_READ : AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
            directory ? FileAttributes.Directory : FileAttributes.Normal,
            ShareAccess.Read | ShareAccess.Write,
            CreateDisposition.FILE_OPEN,
            directory ? CreateOptions.FILE_DIRECTORY_FILE : CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
            null);
        if (status != NTStatus.STATUS_SUCCESS) Throw(status, path);
        return handle;
    }

    internal static void Throw(NTStatus status, string path)
    {
        throw status switch
        {
            NTStatus.STATUS_OBJECT_NAME_NOT_FOUND or NTStatus.STATUS_OBJECT_PATH_NOT_FOUND or NTStatus.STATUS_NOT_A_DIRECTORY
                => new DirectoryNotFoundException($"{path}: {status}"),
            NTStatus.STATUS_NETWORK_NAME_DELETED or NTStatus.STATUS_USER_SESSION_DELETED or NTStatus.STATUS_INVALID_HANDLE
                => new SmbException(SmbErrorKind.Disconnected, $"{path}: {status}"),
            _ => new SmbException(SmbErrorKind.Other, $"{path}: {status}"),
        };
    }

    private static bool IsDisconnected(Exception ex) => ex
        is SmbException { Kind: SmbErrorKind.Disconnected }
        or SocketException
        or ObjectDisposedException;

    private string ToSmbPath(string path)
    {
        var root = _source.RootPath.Trim('/');
        var full = root.Length == 0 ? path : path.Length == 0 ? root : $"{root}/{path}";
        return full.Replace('/', '\\');
    }

    private async Task ConnectCoreAsync(CancellationToken ct)
    {
        IPAddress? address;
        if (!IPAddress.TryParse(_source.Host, out address))
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(_source.Host, ct).ConfigureAwait(false);
                address = addresses.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
            }
            catch (SocketException ex)
            {
                throw new SmbException(SmbErrorKind.Connect, ex.Message);
            }
        }
        if (address is null) throw new SmbException(SmbErrorKind.Connect, $"Cannot resolve {_source.Host}");

        var client = new PortSmb2Client(ResponseTimeoutMs, enableSMB311Support: true, resolveDfsNamespace: false);
        bool connected;
        try
        {
            connected = await Task.Run(() => client.Connect(address, _source.Port), ct).WaitAsync(ConnectTimeout, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or SocketException)
        {
            client.Disconnect();
            throw new SmbException(SmbErrorKind.Connect, ex.Message);
        }
        if (!connected) throw new SmbException(SmbErrorKind.Connect, $"Connect failed: {_source.Host}:{_source.Port}");

        var login = await Task.Run(() => client.Login(string.Empty, _source.UserName, _password), ct).ConfigureAwait(false);
        if (login != NTStatus.STATUS_SUCCESS)
        {
            client.Disconnect();
            throw new SmbException(SmbErrorKind.Auth, $"Login failed: {login}");
        }

        var store = await Task.Run(() => client.TreeConnect(_source.Share, out var status) is { } s && status == NTStatus.STATUS_SUCCESS ? s : null, ct).ConfigureAwait(false);
        if (store is null)
        {
            client.Logoff();
            client.Disconnect();
            throw new SmbException(SmbErrorKind.ShareNotFound, $"TreeConnect failed: {_source.Share}");
        }

        _client = client;
        _store = store;
        Generation++;
    }

    private void DisconnectCore()
    {
        try
        {
            _store?.Disconnect();
            _client?.Logoff();
            _client?.Disconnect();
        }
        catch (Exception ex)
        {
            MessageService.Debug($"SMB disconnect: {ex.Message}");
        }
        _store = null;
        _client = null;
    }

    protected override void DisposeManagedResource()
    {
        // 実行中のリクエストを待ってから切断する（Locker は SourceKey 共有のため破棄しない）。
        _ = Task.Run(async () =>
        {
            using (await _locker.LockAsync().ConfigureAwait(false))
            {
                DisconnectCore();
            }
        });
        base.DisposeManagedResource();
    }
}
