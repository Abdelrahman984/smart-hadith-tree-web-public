using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHadithTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNarratorGeographyAndStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GawamiRank",
                table: "Narrators",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidencePlaces",
                table: "Narrators",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalNarrationsCount",
                table: "Narrators",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UniqueHadithCount",
                table: "Narrators",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GawamiRank",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "ResidencePlaces",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "TotalNarrationsCount",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "UniqueHadithCount",
                table: "Narrators");
        }
    }
}
