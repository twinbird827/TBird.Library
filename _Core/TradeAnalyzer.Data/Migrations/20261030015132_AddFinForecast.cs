using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeAnalyzer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinForecast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "CurrentFiscalYearEnd",
                table: "FinSummaries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ForecastNetProfit",
                table: "FinSummaries",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ForecastNonConsolidatedNetProfit",
                table: "FinSummaries",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ForecastNonConsolidatedOperatingProfit",
                table: "FinSummaries",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ForecastOperatingProfit",
                table: "FinSummaries",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NextFiscalYearEnd",
                table: "FinSummaries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NextForecastNetProfit",
                table: "FinSummaries",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NextForecastOperatingProfit",
                table: "FinSummaries",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentFiscalYearEnd",
                table: "FinSummaries");

            migrationBuilder.DropColumn(
                name: "ForecastNetProfit",
                table: "FinSummaries");

            migrationBuilder.DropColumn(
                name: "ForecastNonConsolidatedNetProfit",
                table: "FinSummaries");

            migrationBuilder.DropColumn(
                name: "ForecastNonConsolidatedOperatingProfit",
                table: "FinSummaries");

            migrationBuilder.DropColumn(
                name: "ForecastOperatingProfit",
                table: "FinSummaries");

            migrationBuilder.DropColumn(
                name: "NextFiscalYearEnd",
                table: "FinSummaries");

            migrationBuilder.DropColumn(
                name: "NextForecastNetProfit",
                table: "FinSummaries");

            migrationBuilder.DropColumn(
                name: "NextForecastOperatingProfit",
                table: "FinSummaries");
        }
    }
}
