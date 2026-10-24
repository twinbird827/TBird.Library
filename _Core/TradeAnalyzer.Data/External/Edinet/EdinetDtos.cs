using System.Text.Json.Serialization;

namespace TradeAnalyzer.Data.External.Edinet;

/// <summary>GET /documents.json のレスポンス。</summary>
public class EdinetDocumentListResponse
{
    [JsonPropertyName("metadata")] public EdinetMetadata? Metadata { get; set; }
    [JsonPropertyName("results")] public List<EdinetResultItem> Results { get; set; } = new();
}

public class EdinetMetadata
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class EdinetResultItem
{
    [JsonPropertyName("docID")] public string? DocId { get; set; }
    [JsonPropertyName("edinetCode")] public string? EdinetCode { get; set; }
    [JsonPropertyName("secCode")] public string? SecCode { get; set; }
    [JsonPropertyName("docTypeCode")] public string? DocTypeCode { get; set; }
    [JsonPropertyName("formCode")] public string? FormCode { get; set; }
    [JsonPropertyName("periodStart")] public string? PeriodStart { get; set; }
    [JsonPropertyName("periodEnd")] public string? PeriodEnd { get; set; }
    [JsonPropertyName("csvFlag")] public string? CsvFlag { get; set; }
    [JsonPropertyName("xbrlFlag")] public string? XbrlFlag { get; set; }
}
