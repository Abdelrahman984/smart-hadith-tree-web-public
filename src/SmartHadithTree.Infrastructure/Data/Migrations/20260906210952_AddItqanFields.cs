using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHadithTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddItqanFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItqanGrade",
                table: "Narrators",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItqanId",
                table: "Narrators",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_ItqanId",
                table: "Narrators",
                column: "ItqanId",
                unique: true,
                filter: "[ItqanId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Narrators_ItqanId",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "ItqanGrade",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "ItqanId",
                table: "Narrators");
        }
    }
}
