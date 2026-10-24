using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace TradeAnalyzer.Data.External;

/// <summary>
/// J-Quants 等は数値を JSON number でも string("1234.0") でも、欠損を ""・null でも返しうる。
/// それらを寛容に double? へ変換する。
/// </summary>
public class FlexibleDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                return reader.GetDouble();
            case JsonTokenType.String:
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s)) return null;
                return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteNumberValue(value.Value);
        else writer.WriteNullValue();
    }
}
