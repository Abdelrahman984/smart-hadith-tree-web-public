using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHadithTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeSearchCollationAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NormalizedMatn",
                table: "Hadiths",
                type: "nvarchar(max)",
                nullable: false,
                collation: "Arabic_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedBookName",
                table: "Hadiths",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                collation: "Arabic_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "FullIsnadText",
                table: "Hadiths",
                type: "nvarchar(max)",
                nullable: true,
                collation: "Arabic_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldCollation: "Arabic_100_CI_AI");

            migrationBuilder.CreateIndex(
                name: "IX_Hadiths_NormalizedBookName",
                table: "Hadiths",
                column: "NormalizedBookName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Hadiths_NormalizedBookName",
                table: "Hadiths");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedMatn",
                table: "Hadiths",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldCollation: "Arabic_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedBookName",
                table: "Hadiths",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldCollation: "Arabic_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "FullIsnadText",
                table: "Hadiths",
                type: "nvarchar(max)",
                nullable: true,
                collation: "Arabic_100_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldCollation: "Arabic_100_BIN2");
        }
    }
}
