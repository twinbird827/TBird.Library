using SMBLibrary;

namespace ComicViewer.Sources;

/// <summary>SMB ファイルのシーク可能な読み取りストリーム。zip の中央ディレクトリと必要なエントリだけを読むために使う。</summary>
public sealed class SmbReadStream : Stream
{
    private const int BufferSize = 64 * 1024;

    private readonly SmbFileSource _source;
    private readonly string _path;
    private object _handle;
    private int _generation;
    private long _position;
    private readonly byte[] _buffer = new byte[BufferSize];
    private long _bufferStart = -1;
    private int _bufferLength;
    private bool _disposed;

    internal SmbReadStream(SmbFileSource source, string path, object handle, int generation, long length)
    {
        _source = source;
        _path = path;
        _handle = handle;
        _generation = generation;
        Length = length;
    }

    public override bool CanRead => !_disposed;

    public override bool CanSeek => !_disposed;

    public override bool CanWrite => false;

    public override long Length { get; }

    public override long Position
    {
        get => _position;
        set => _position = value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (count == 0 || _position >= Length) return 0;
        if (_position < _bufferStart || _position >= _bufferStart + _bufferLength) Fill();

        var start = (int)(_position - _bufferStart);
        var n = Math.Min(count, _bufferLength - start);
        Array.Copy(_buffer, start, buffer, offset, n);
        _position += n;
        return n;
    }

    private void Fill()
    {
        var data = _source.Execute(store =>
        {
            // 再接続でハンドルが無効になっていたら開き直し、同じ位置から読む。
            if (_generation != _source.Generation)
            {
                _handle = _source.OpenHandle(store, _path, directory: false);
                _generation = _source.Generation;
            }
            var size = (int)Math.Min(BufferSize, store.MaxReadSize);
            var status = store.ReadFile(out var bytes, _handle, _position, size);
            if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_END_OF_FILE) SmbFileSource.Throw(status, _path);
            return bytes ?? Array.Empty<byte>();
        });
        if (data.Length == 0) throw new EndOfStreamException(_path);
        Array.Copy(data, _buffer, data.Length);
        _bufferStart = _position;
        _bufferLength = data.Length;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            _ => Length + offset,
        };
        return _position;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
            var handle = _handle;
            var generation = _generation;
            try
            {
                _source.Execute(store =>
                {
                    if (generation == _source.Generation) store.CloseFile(handle);
                    return true;
                });
            }
            catch (Exception ex)
            {
                TBird.Core.MessageService.Debug($"SMB close: {_path} {ex.Message}");
            }
        }
        base.Dispose(disposing);
    }
}
