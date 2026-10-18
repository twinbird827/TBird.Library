using System.Net;
using SMBLibrary;
using SMBLibrary.Client;

namespace ComicViewer.Sources;

/// <summary>SMB2Client の公開 Connect はポート 445 / 139 固定のため、protected internal のポート指定版を公開する。</summary>
public class PortSmb2Client : SMB2Client
{
    public PortSmb2Client(int responseTimeoutInMilliseconds, bool enableSMB311Support, bool resolveDfsNamespace)
        : base(responseTimeoutInMilliseconds, enableSMB311Support, resolveDfsNamespace)
    {
    }

    public bool Connect(IPAddress address, int port) => Connect(address, SMBTransportType.DirectTCPTransport, port);
}
