using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TradeAnalyzer.Worker.Claude;

/// <summary>
/// 当日 Top-K 候補に実データを注入して定性根拠文／リスクを生成する定性層（<c>claude -p --output-format json</c>）。
/// プロンプトを stdin へ流し、stdout の JSON エンベロープから <c>result</c>（モデル応答）を取り出し、その中の
/// JSON（summary/risks/used_facts）を防御的にパースする。<see cref="ProcessRunner"/> の堅牢コア
/// （timeout/kill/stderr/UTF-8）を共有する。現状 CLI 直結の1実装のみ（公式 SDK 経路が要件化した時点で
/// interface seam を再導入して差し替え可能にする）。
/// <para>
/// 失敗は throw せず <c>null</c>（フォールバック契約）。捕捉対象: (i) 非0終了（認証切れ・クレジット枯渇）、
/// (ii) RunAsync が投げる例外＝CLI 不在/起動失敗（<see cref="InvalidOperationException"/>）・timeout
/// （<see cref="TimeoutException"/>）、(iii) パース不能（<see cref="JsonException"/>）。config 誤設定の
/// <see cref="ArgumentOutOfRangeException"/>（timeout 範囲外）は捕捉しない＝ループ前 ValidateClaudeConfig で
/// fail-fast 済み（誤設定を飲んで全銘柄スキップ→ExitCode=0 に偽装しない）。
/// </para>
/// </summary>
internal sealed class ClaudeCliAnalysisService
{
    private readonly ClaudeOptions _opt;
    private readonly ILogger<ClaudeCliAnalysisService> _logger;

    public ClaudeCliAnalysisService(IOptions<ClaudeOptions> opt, ILogger<ClaudeCliAnalysisService> logger)
    {
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task<ClaudeAnalysisResult?> AnalyzeAsync(ClaudeFacts facts, CancellationToken ct = default)
    {
        string prompt = ClaudePromptBuilder.Build(facts);
        var psi = new ProcessStartInfo { FileName = _opt.ExecutablePath };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("json");
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(_opt.Model);

        ProcessResult result;
        try
        {
            result = await ProcessRunner.RunAsync(
                psi, _logger, TimeSpan.FromMinutes(_opt.TimeoutMinutes), stdin: prompt,
                stdoutLogPrefix: "[claude]", stderrLogPrefix: "[claude:err]",
                displayName: $"Claude 実行: {facts.Code}", captureStdout: true,
                // stdout=巨大 JSON エンベロープ（機微含む）は Debug へ降格し既定 Information ログへ流さない。
                stdoutLogLevel: LogLevel.Debug,
                startErrorHint: "Claude:ExecutablePath を確認（Windows は claude.cmd）", ct: ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            // CLI 不在/起動失敗・timeout。非致命＝当該銘柄スキップし ML のみで継続。
            _logger.LogWarning(ex, "Claude 実行に失敗（{Code}）。当該銘柄をスキップします。", facts.Code);
            return null;
        }

        if (result.ExitCode != 0)
        {
            _logger.LogWarning("Claude が ExitCode={Code} で失敗（{Sym}・認証切れ/クレジット枯渇の可能性）。スキップします。\nstderr:\n{Err}\nエンベロープ: {Env}",
                result.ExitCode, facts.Code, result.Stderr, ExtractErrorInfo(result.Stdout) ?? "（抽出不可）");
            return null;
        }

        var model = ParseModelOutput(result.Stdout);
        if (model?.Summary is not { Length: > 0 })
        {
            // API エラーは ExitCode=0＋is_error:true で返ることもあり（exit code 契約は docs 未記載）、
            // その場合 summary 空でこの分岐に落ちるため、ここでもエンベロープの真因を併記する。
            _logger.LogWarning("Claude 出力をパースできませんでした（{Code}）。スキップします。\nエンベロープ: {Env}",
                facts.Code, ExtractErrorInfo(result.Stdout) ?? "（抽出不可）");
            return null;
        }

        var risks = model.Risks ?? new List<string>();
        bool unverified = QualitativeNumberGuard.HasUnverifiedNumbers(model.Summary, risks, facts);
        if (unverified)
            _logger.LogWarning("Claude 出力に注入外の数値が混入した疑い（{Code}）。numericUnverified を立てます。", facts.Code);

        return new ClaudeAnalysisResult(model.Summary, risks, model.UsedFacts ?? new List<string>(),
            _opt.Model, unverified);
    }

    /// <summary>エンベロープ→result→モデル JSON を防御的にパースする。失敗は null。
    /// internal static は SelfTest が CLI 版ブレ（エンベロープ有無・フェンス・非JSON）の4フォールバックを
    /// 直接検証するため（<see cref="Commands.ResolveTodayJst"/> の内部公開と同じ前例）。</summary>
    internal static ModelOutput? ParseModelOutput(string stdout)
    {
        string candidate;
        try
        {
            // 外側エンベロープ（--output-format json）から result を取り出す。CLI 版差に備え、result が無ければ
            // stdout 全体をモデル JSON 候補として扱う（防御）。
            using var env = JsonDocument.Parse(stdout);
            candidate = env.RootElement.ValueKind == JsonValueKind.Object
                && env.RootElement.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.String
                    ? r.GetString() ?? stdout
                    : stdout;
        }
        catch (JsonException)
        {
            candidate = stdout; // エンベロープが JSON でない版もありうる＝素の出力から抽出を試す。
        }

        string? json = ExtractJsonObject(candidate);
        if (json == null) return null;
        try
        {
            var md = JsonSerializer.Deserialize<ModelOutput>(json);
            // risks/used_facts の null 要素はここ（JSON→ModelOutput の唯一の信頼境界）で除去する。NRT は
            // コンパイル時のみで、["リスクA",null] は既定 JsonSerializer が無例外で List<string> に混入させ、
            // 数値ガード照合の ArgumentNullException がバッチ全滅（非致命契約違反）に至るため。
            return md is null ? null : md with
            {
                Risks = md.Risks?.Where(r => r is not null).ToList(),
                UsedFacts = md.UsedFacts?.Where(u => u is not null).ToList(),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>非 success 経路の診断用に、stdout エンベロープから is_error/subtype/result を1行へ抽出する。
    /// 失敗理由（認証切れ authentication_failed・クレジット枯渇 billing_error・model_not_found）は stderr でなく
    /// stdout の JSON エンベロープ側に載り、stdout は Debug 降格済み＝既定 Information ログに出ないため、
    /// 警告へ真因のみ併記する（stdout 全体の盲目切詰めは serialize 順次第で result が切れるので抽出方式）。
    /// internal static は SelfTest が純関数として直接検証するため（ParseModelOutput の内部公開と同じ前例）。
    /// エンベロープが JSON でない/対象キーが無いときは null。</summary>
    internal static string? ExtractErrorInfo(string stdout)
    {
        try
        {
            using var env = JsonDocument.Parse(stdout);
            if (env.RootElement.ValueKind != JsonValueKind.Object) return null;
            var parts = new List<string>();
            if (env.RootElement.TryGetProperty("is_error", out var e)) parts.Add("is_error=" + e.GetRawText());
            if (env.RootElement.TryGetProperty("subtype", out var s) && s.ValueKind == JsonValueKind.String)
                parts.Add("subtype=" + s.GetString());
            if (env.RootElement.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.String)
                parts.Add("result=" + r.GetString());
            return parts.Count > 0 ? string.Join(" ", parts) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>コードフェンス除去＋先頭 '{'〜末尾 '}' 抽出（モデル/CLI 出力のブレに耐える）。
    /// 既知限界: prose 中に別の brace ペアが混在すると抽出範囲が不正 JSON 化し per-銘柄スキップに落ちる
    /// （「JSON のみ」指示への二重違反が前提の狭い tail＝ログ付きスキップで許容）。</summary>
    private static string? ExtractJsonObject(string s)
    {
        int start = s.IndexOf('{');
        int end = s.LastIndexOf('}');
        return start >= 0 && end > start ? s.Substring(start, end - start + 1) : null;
    }

    internal sealed record ModelOutput(
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("risks")] List<string>? Risks,
        [property: JsonPropertyName("used_facts")] List<string>? UsedFacts);
}
