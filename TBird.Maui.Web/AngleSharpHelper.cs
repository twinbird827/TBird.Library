using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;

namespace TBird.Maui.Web;

/// <summary>
/// AngleSharp の Configuration / BrowsingContext / OpenAsync 3 行パターンを集約するヘルパ。
/// 呼出側は <c>var doc = await AngleSharpHelper.ParseAsync(html, ct);</c> で済む。
/// </summary>
public static class AngleSharpHelper
{
    public static async Task<IDocument> ParseAsync(string html, CancellationToken ct = default)
    {
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        return await context.OpenAsync(req => req.Content(html), ct).ConfigureAwait(false);
    }
}
