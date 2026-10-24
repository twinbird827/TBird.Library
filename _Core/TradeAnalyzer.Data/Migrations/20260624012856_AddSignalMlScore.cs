using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeAnalyzer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalMlScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MlScore",
                table: "Signals",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MlScore",
                table: "Signals");
        }
    }
}
