using System.Diagnostics;
using System.Text;
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
        var psi = new ProcessStartInfo
        {
            FileName = _opt.ExecutablePath,
            // ProcessRunner の契約: stdout/stderr の Encoding は呼び手が psi に設定する（RunPythonAsync と同型）。
            // claude CLI（Node）は UTF-8 出力で、stdout はそのまま QualitativeJson の書戻しペイロード。未設定だと
            // コンソール非接続（タスクスケジューラ/exe 直叩き）で既定 cp932 復号となり、日本語 summary の文字化け
            // 永続化や多バイト途切れによる JSON 破壊→全銘柄スキップ（ExitCode=0）で定性層が無言死する。
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
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

        // エンベロープは1回だけパースし、候補・is_error・診断1行（result は有界化済み）をここから引き回す。
        var (candidate, isError, errorInfo) = ParseEnvelope(result.Stdout);

        if (result.ExitCode != 0)
        {
            _logger.LogWarning("Claude が ExitCode={Code} で失敗（{Sym}・認証切れ/クレジット枯渇の可能性）。スキップします。\nstderr:\n{Err}\nエンベロープ: {Env}",
                result.ExitCode, facts.Code, result.Stderr, errorInfo ?? "（抽出不可）");
            return null;
        }

        if (isError)
        {
            // API エラーは ExitCode=0＋is_error:true で返ることもある（exit code 契約は docs 未記載）。
            // result に brace 含みテキスト（error_during_execution の部分出力等）が載ると防御的パーサが偶然
            // summary を取り出しエラー応答を正規結果として保存しうるため、パース前にここで受理拒否する。
            _logger.LogWarning("Claude がエラー応答（is_error）を返しました（{Code}）。スキップします。\nエンベロープ: {Env}",
                facts.Code, errorInfo ?? "（抽出不可）");
            return null;
        }

        var model = ParseModelCandidate(candidate);
        if (model?.Summary is not { Length: > 0 })
        {
            _logger.LogWarning("Claude 出力をパースできませんでした（{Code}）。スキップします。\nエンベロープ: {Env}",
                facts.Code, errorInfo ?? "（抽出不可）");
            return null;
        }

        var risks = model.Risks ?? new List<string>();
        bool unverified = QualitativeNumberGuard.HasUnverifiedNumbers(model.Summary, risks, facts);
        if (unverified)
            _logger.LogWarning("Claude 出力に注入外の数値が混入した疑い（{Code}）。numericUnverified を立てます。", facts.Code);

        return new ClaudeAnalysisResult(model.Summary, risks, model.UsedFacts ?? new List<string>(),
            _opt.Model, unverified);
    }

    // 診断ログへ載せる result の上限長。パース不能分岐では「機微含むから Debug 降格」したモデル全文（数 KB の
    // prose になりうる）が result に載るため、有界化しないと既定 Information ログへ全量流出し降格判断と矛盾する。
    private const int ErrorResultCap = 300;

    /// <summary>stdout エンベロープ（--output-format json）を1回だけパースし、モデル JSON 候補・is_error・
    /// 診断1行（is_error/subtype/result。result は先頭 <see cref="ErrorResultCap"/> 字で有界化）をまとめて返す。
    /// 受理拒否ゲートは「is_error プロパティが存在し true」のときのみ＝is_error を返さない版 CLI の正常系は不変。
    /// エンベロープが JSON でない/オブジェクトでない版は stdout 全体を候補に降格し isError=false（防御）。
    /// internal static は SelfTest が純関数として直接検証するため（<see cref="Commands.ResolveTodayJst"/> の
    /// 内部公開と同じ前例）。</summary>
    internal static (string Candidate, bool IsError, string? ErrorInfo) ParseEnvelope(string stdout)
    {
        try
        {
            using var env = JsonDocument.Parse(stdout);
            if (env.RootElement.ValueKind != JsonValueKind.Object) return (stdout, false, null);

            string candidate = stdout;
            bool isError = false;
            var parts = new List<string>();
            if (env.RootElement.TryGetProperty("is_error", out var e))
            {
                isError = e.ValueKind == JsonValueKind.True;
                parts.Add("is_error=" + e.GetRawText());
            }
            if (env.RootElement.TryGetProperty("subtype", out var s) && s.ValueKind == JsonValueKind.String)
                parts.Add("subtype=" + s.GetString());
            if (env.RootElement.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.String)
            {
                candidate = r.GetString() ?? stdout;
                parts.Add("result=" + (candidate.Length > ErrorResultCap ? candidate[..ErrorResultCap] + "…" : candidate));
            }
            return (candidate, isError, parts.Count > 0 ? string.Join(" ", parts) : null);
        }
        catch (JsonException)
        {
            return (stdout, false, null); // エンベロープが JSON でない版もありうる＝素の出力から抽出を試す。
        }
    }

    /// <summary>エンベロープ→result→モデル JSON を防御的にパースする。失敗は null。
    /// internal static は SelfTest が CLI 版ブレ（エンベロープ有無・フェンス・非JSON）の4フォールバックを
    /// 直接検証するため（実行時の AnalyzeAsync は ParseEnvelope を1回だけ呼び、候補を
    /// <see cref="ParseModelCandidate"/> へ渡す）。</summary>
    internal static ModelOutput? ParseModelOutput(string stdout) =>
        ParseModelCandidate(ParseEnvelope(stdout).Candidate);

    /// <summary>モデル JSON 候補（エンベロープ解決済み）からフェンス除去＋抽出＋デシリアライズ。失敗は null。</summary>
    private static ModelOutput? ParseModelCandidate(string candidate)
    {
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
    /// 警告へ真因のみ併記する（stdout 全体の盲目切詰めは serialize 順次第で result が切れるので抽出方式。
    /// result フィールド自体も <see cref="ErrorResultCap"/> で有界化＝真因の先頭は必ず載る）。
    /// 実体は <see cref="ParseEnvelope"/> への委譲（SelfTest 互換の seam）。</summary>
    internal static string? ExtractErrorInfo(string stdout) => ParseEnvelope(stdout).ErrorInfo;

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
