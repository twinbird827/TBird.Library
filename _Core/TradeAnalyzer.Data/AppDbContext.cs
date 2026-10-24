using Microsoft.EntityFrameworkCore;
using TradeAnalyzer.Data.Entities;

namespace TradeAnalyzer.Data;

/// <summary>
/// 段階1の永続層。ポイントインタイム設計:
/// マスタは (Code, AsOfDate)、価格は (Code, Date)、財務は (Code, DiscDate, DocType)。
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<DailyBar> DailyBars => Set<DailyBar>();
    public DbSet<FinSummary> FinSummaries => Set<FinSummary>();
    public DbSet<EarningsCalendar> EarningsCalendars => Set<EarningsCalendar>();
    public DbSet<TradingCalendar> TradingCalendars => Set<TradingCalendar>();
    public DbSet<EdinetDocument> EdinetDocuments => Set<EdinetDocument>();
    public DbSet<EdinetFinFact> EdinetFinFacts => Set<EdinetFinFact>();
    public DbSet<Signal> Signals => Set<Signal>();
    public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();
    public DbSet<BacktestResult> BacktestResults => Set<BacktestResult>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Stock>(e =>
        {
            e.HasKey(x => new { x.Code, x.AsOfDate });
            e.HasIndex(x => x.AsOfDate);
            e.Property(x => x.Code).HasMaxLength(10);
        });

        b.Entity<DailyBar>(e =>
        {
            e.HasKey(x => new { x.Code, x.Date });
            e.HasIndex(x => x.Date);
            e.HasIndex(x => new { x.Code, x.Date });
            e.Property(x => x.Code).HasMaxLength(10);
        });

        b.Entity<FinSummary>(e =>
        {
            e.HasKey(x => new { x.Code, x.DiscloseDate, x.DocType });
            e.HasIndex(x => new { x.Code, x.DiscloseDate });
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.DocType).HasMaxLength(40);
        });

        b.Entity<EarningsCalendar>(e =>
        {
            e.HasKey(x => new { x.Code, x.Date });
            e.Property(x => x.Code).HasMaxLength(10);
        });

        b.Entity<TradingCalendar>(e =>
        {
            e.HasKey(x => x.Date);
        });

        b.Entity<EdinetDocument>(e =>
        {
            e.HasKey(x => x.DocId);
            e.HasIndex(x => x.SubmitDate);
            e.HasIndex(x => new { x.NormalizedCode, x.SubmitDate });
        });

        b.Entity<EdinetFinFact>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.DocId, x.ElementId });
            e.HasIndex(x => new { x.Code, x.FactName });
        });

        b.Entity<Signal>(e =>
        {
            e.HasKey(x => new { x.Date, x.Code });
            e.HasIndex(x => x.Date);
        });

        b.Entity<BacktestRun>(e =>
        {
            e.HasKey(x => x.Id);
        });

        b.Entity<BacktestResult>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RunId);
            e.HasOne(x => x.Run).WithMany(r => r.Results).HasForeignKey(x => x.RunId);
            e.Property(x => x.Code).HasMaxLength(10);
        });
    }
}
