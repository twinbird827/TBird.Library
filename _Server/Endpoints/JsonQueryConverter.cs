using System.Text.Json;

namespace NewReleaseChecker.Relay.Endpoints;

/// <summary>
/// クライアントの JSON 本文（楽天APIクエリと 1:1 対応のオブジェクト）を、上流クエリ用の文字列辞書へ変換する。
/// 数値はそのまま文字列化、真偽値は 1/0 に、null はスキップ対象（値 null）として扱う。
/// </summary>
internal static class JsonQueryConverter
{
    public static async Task<IDictionary<string, string?>> ReadAsync(HttpContext context)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var parsed = await context.Request.ReadFromJsonAsync<JsonElement>(context.RequestAborted);
            if (parsed is { ValueKind: JsonValueKind.Object } body)
            {
                foreach (var prop in body.EnumerateObject())
                {
                    query[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.GetRawText(),
                        JsonValueKind.True => "1",
                        JsonValueKind.False => "0",
                        JsonValueKind.Null => null,
                        _ => prop.Value.GetRawText(),
                    };
                }
            }
        }
        catch (JsonException)
        {
            // 空・不正 JSON は空クエリ扱い（上流が 400 を返し、それがそのまま透過される）。
        }
        return query;
    }
}
