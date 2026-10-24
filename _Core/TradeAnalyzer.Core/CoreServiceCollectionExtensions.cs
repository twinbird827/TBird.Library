using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeAnalyzer.Core.Backtest;
using TradeAnalyzer.Core.Ingest;
using TradeAnalyzer.Core.Rules;

namespace TradeAnalyzer.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>ドメインロジック（ルール・バックテスト・取得）を DI 登録する。</summary>
    public static IServiceCollection AddTradeAnalyzerCore(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RuleOptions>(config.GetSection(RuleOptions.SectionName));
        services.Configure<BacktestOptions>(config.GetSection(BacktestOptions.SectionName));

        // AppDbContext（scoped）に依存するためスコープド登録。
        services.AddScoped<RuleEngine>();
        services.AddScoped<BacktestService>();
        services.AddScoped<IngestService>();

        return services;
    }
}
