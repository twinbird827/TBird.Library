using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TradeAnalyzer.Data;

/// <summary>
/// 設計時（dotnet ef migrations add 等）に使う DbContext ファクトリ。
/// Worker のホスト DI に依存せずマイグレーションを生成できるようにする。
/// 実行時の接続文字列は Worker の DI 側で appsettings から注入される。
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // 実行時と同じ _Tools/TradeAnalyzer/trade.db を指す（設計時 ef database update が CWD に
        // 迷子の trade.db を作らないよう AppPaths に一元化。migrations add はモデルのみで DB は開かない）。
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(AppPaths.TradeDbConnectionString)
            .Options;
        return new AppDbContext(options);
    }
}
