using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHadithTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShamelaRegistryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourcePage",
                table: "ScholarEvaluations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceVolume",
                table: "ScholarEvaluations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IbnHajarRank",
                table: "Narrators",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IkhtilatSeverity",
                table: "Narrators",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NoHearingAfterIkhtilat",
                table: "Narrators",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ShamelaManId",
                table: "Narrators",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "Narrators",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Verdict",
                table: "Narrators",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "Arabic_100_CI_AI");

            migrationBuilder.AddColumn<string>(
                name: "Evidence",
                table: "MukhtalitHearings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                collation: "Arabic_100_CI_AI");

            migrationBuilder.AddColumn<string>(
                name: "SourceBook",
                table: "MukhtalitHearings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                collation: "Arabic_100_CI_AI");

            migrationBuilder.CreateTable(
                name: "MukhtalitGroupRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MukhtalitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false, collation: "Arabic_100_CI_AI"),
                    Timing = table.Column<int>(type: "int", nullable: false),
                    Quote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, collation: "Arabic_100_CI_AI"),
                    SourceBook = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true, collation: "Arabic_100_CI_AI")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MukhtalitGroupRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MukhtalitGroupRules_Narrators_MukhtalitId",
                        column: x => x.MukhtalitId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_ShamelaManId",
                table: "Narrators",
                column: "ShamelaManId");

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_SourceKey",
                table: "Narrators",
                column: "SourceKey",
                unique: true,
                filter: "[SourceKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MukhtalitGroupRules_MukhtalitId",
                table: "MukhtalitGroupRules",
                column: "MukhtalitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MukhtalitGroupRules");

            migrationBuilder.DropIndex(
                name: "IX_Narrators_ShamelaManId",
                table: "Narrators");

            migrationBuilder.DropIndex(
                name: "IX_Narrators_SourceKey",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "SourcePage",
                table: "ScholarEvaluations");

            migrationBuilder.DropColumn(
                name: "SourceVolume",
                table: "ScholarEvaluations");

            migrationBuilder.DropColumn(
                name: "IbnHajarRank",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "IkhtilatSeverity",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "NoHearingAfterIkhtilat",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "ShamelaManId",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "Verdict",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "Evidence",
                table: "MukhtalitHearings");

            migrationBuilder.DropColumn(
                name: "SourceBook",
                table: "MukhtalitHearings");
        }
    }
}
