using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeAnalyzer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStandardMarketData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarginInterests",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    IssueType = table.Column<string>(type: "TEXT", nullable: true),
                    ShortVolume = table.Column<double>(type: "REAL", nullable: true),
                    LongVolume = table.Column<double>(type: "REAL", nullable: true),
                    ShortNegotiableVolume = table.Column<double>(type: "REAL", nullable: true),
                    LongNegotiableVolume = table.Column<double>(type: "REAL", nullable: true),
                    ShortStandardizedVolume = table.Column<double>(type: "REAL", nullable: true),
                    LongStandardizedVolume = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarginInterests", x => new { x.Code, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "SectorShortRatios",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Sector33 = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SellExShortValue = table.Column<double>(type: "REAL", nullable: true),
                    ShortWithRestrictionValue = table.Column<double>(type: "REAL", nullable: true),
                    ShortNoRestrictionValue = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorShortRatios", x => new { x.Sector33, x.Date });
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarginInterests_Date",
                table: "MarginInterests",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_SectorShortRatios_Date",
                table: "SectorShortRatios",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarginInterests");

            migrationBuilder.DropTable(
                name: "SectorShortRatios");
        }
    }
}
