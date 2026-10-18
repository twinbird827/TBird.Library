namespace ComicViewer.Sources;

public enum SmbErrorKind
{
    Connect,
    Auth,
    ShareNotFound,
    Disconnected,
    Other,
}

public class SmbException : IOException
{
    public SmbException(SmbErrorKind kind, string message) : base(message)
    {
        Kind = kind;
    }

    public SmbErrorKind Kind { get; }
}
