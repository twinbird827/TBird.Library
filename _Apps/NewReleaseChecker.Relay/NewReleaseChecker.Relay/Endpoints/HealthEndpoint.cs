namespace NewReleaseChecker.Relay.Endpoints;

/// <summary>GET /healthz — IIS 死活監視・接続確認用。認証・レート制限の対象外。</summary>
public static class HealthEndpoint
{
    public static IResult Handle() => Results.Ok(new { status = "ok" });
}
