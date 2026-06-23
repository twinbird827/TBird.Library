using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeAnalyzer.Data.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BacktestRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", nullable: true),
                    InSampleStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    InSampleEnd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    OutSampleStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    OutSampleEnd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    OptionsJson = table.Column<string>(type: "TEXT", nullable: true),
                    WinRate = table.Column<double>(type: "REAL", nullable: false),
                    AvgReturn = table.Column<double>(type: "REAL", nullable: false),
                    TradeCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyBars",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Open = table.Column<double>(type: "REAL", nullable: true),
                    High = table.Column<double>(type: "REAL", nullable: true),
                    Low = table.Column<double>(type: "REAL", nullable: true),
                    Close = table.Column<double>(type: "REAL", nullable: true),
                    Volume = table.Column<double>(type: "REAL", nullable: true),
                    TurnoverValue = table.Column<double>(type: "REAL", nullable: true),
                    AdjustmentFactor = table.Column<double>(type: "REAL", nullable: true),
                    AdjOpen = table.Column<double>(type: "REAL", nullable: true),
                    AdjHigh = table.Column<double>(type: "REAL", nullable: true),
                    AdjLow = table.Column<double>(type: "REAL", nullable: true),
                    AdjClose = table.Column<double>(type: "REAL", nullable: true),
                    AdjVolume = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyBars", x => new { x.Code, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "EarningsCalendars",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    FiscalYear = table.Column<string>(type: "TEXT", nullable: true),
                    FiscalQuarter = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarningsCalendars", x => new { x.Code, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "EdinetDocuments",
                columns: table => new
                {
                    DocId = table.Column<string>(type: "TEXT", nullable: false),
                    EdinetCode = table.Column<string>(type: "TEXT", nullable: true),
                    SecCode = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedCode = table.Column<string>(type: "TEXT", nullable: true),
                    DocTypeCode = table.Column<string>(type: "TEXT", nullable: true),
                    FormCode = table.Column<string>(type: "TEXT", nullable: true),
                    SubmitDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CsvFlag = table.Column<string>(type: "TEXT", nullable: true),
                    XbrlFlag = table.Column<string>(type: "TEXT", nullable: true),
                    Parsed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdinetDocuments", x => x.DocId);
                });

            migrationBuilder.CreateTable(
                name: "EdinetFinFacts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocId = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: true),
                    ElementId = table.Column<string>(type: "TEXT", nullable: false),
                    FactName = table.Column<string>(type: "TEXT", nullable: false),
                    ContextId = table.Column<string>(type: "TEXT", nullable: true),
                    IsConsolidated = table.Column<bool>(type: "INTEGER", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: true),
                    Unit = table.Column<string>(type: "TEXT", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdinetFinFacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinSummaries",
                columns: table => new
                {
                    DiscloseDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    DocType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Sales = table.Column<double>(type: "REAL", nullable: true),
                    OperatingProfit = table.Column<double>(type: "REAL", nullable: true),
                    NetProfit = table.Column<double>(type: "REAL", nullable: true),
                    Eps = table.Column<double>(type: "REAL", nullable: true),
                    Bps = table.Column<double>(type: "REAL", nullable: true),
                    TotalAssets = table.Column<double>(type: "REAL", nullable: true),
                    Equity = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinSummaries", x => new { x.Code, x.DiscloseDate, x.DocType });
                });

            migrationBuilder.CreateTable(
                name: "Signals",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Passed = table.Column<bool>(type: "INTEGER", nullable: false),
                    RuleScore = table.Column<double>(type: "REAL", nullable: false),
                    Rationale = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signals", x => new { x.Date, x.Code });
                });

            migrationBuilder.CreateTable(
                name: "Stocks",
                columns: table => new
                {
                    AsOfDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", nullable: true),
                    Sector17 = table.Column<string>(type: "TEXT", nullable: true),
                    Sector33 = table.Column<string>(type: "TEXT", nullable: true),
                    ScaleCategory = table.Column<string>(type: "TEXT", nullable: true),
                    MarketCode = table.Column<string>(type: "TEXT", nullable: true),
                    MarginCode = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => new { x.Code, x.AsOfDate });
                });

            migrationBuilder.CreateTable(
                name: "TradingCalendars",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    HolidayDivision = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingCalendars", x => x.Date);
                });

            migrationBuilder.CreateTable(
                name: "BacktestResults",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ExitDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EntryPrice = table.Column<double>(type: "REAL", nullable: false),
                    ExitPrice = table.Column<double>(type: "REAL", nullable: false),
                    Return = table.Column<double>(type: "REAL", nullable: false),
                    ExitReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BacktestResults_BacktestRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "BacktestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BacktestResults_RunId",
                table: "BacktestResults",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyBars_Code_Date",
                table: "DailyBars",
                columns: new[] { "Code", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyBars_Date",
                table: "DailyBars",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_EdinetDocuments_NormalizedCode_SubmitDate",
                table: "EdinetDocuments",
                columns: new[] { "NormalizedCode", "SubmitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EdinetDocuments_SubmitDate",
                table: "EdinetDocuments",
                column: "SubmitDate");

            migrationBuilder.CreateIndex(
                name: "IX_EdinetFinFacts_Code_FactName",
                table: "EdinetFinFacts",
                columns: new[] { "Code", "FactName" });

            migrationBuilder.CreateIndex(
                name: "IX_EdinetFinFacts_DocId_ElementId",
                table: "EdinetFinFacts",
                columns: new[] { "DocId", "ElementId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinSummaries_Code_DiscloseDate",
                table: "FinSummaries",
                columns: new[] { "Code", "DiscloseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Signals_Date",
                table: "Signals",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_AsOfDate",
                table: "Stocks",
                column: "AsOfDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacktestResults");

            migrationBuilder.DropTable(
                name: "DailyBars");

            migrationBuilder.DropTable(
                name: "EarningsCalendars");

            migrationBuilder.DropTable(
                name: "EdinetDocuments");

            migrationBuilder.DropTable(
                name: "EdinetFinFacts");

            migrationBuilder.DropTable(
                name: "FinSummaries");

            migrationBuilder.DropTable(
                name: "Signals");

            migrationBuilder.DropTable(
                name: "Stocks");

            migrationBuilder.DropTable(
                name: "TradingCalendars");

            migrationBuilder.DropTable(
                name: "BacktestRuns");
        }
    }
}
