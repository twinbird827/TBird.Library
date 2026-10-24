using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeAnalyzer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalQualitative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QualitativeJson",
                table: "Signals",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QualitativeJson",
                table: "Signals");
        }
    }
}
