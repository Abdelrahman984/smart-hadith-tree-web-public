using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHadithTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGawamiEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GawamiAlemId",
                table: "ScholarEvaluations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GawamiRawyId",
                table: "ScholarEvaluations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GawamiId",
                table: "Narrators",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasMukhtalit",
                table: "Narrators",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMudallis",
                table: "Narrators",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "HadithClusterId",
                table: "Hadiths",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HadithClusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GawamiClusterId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Taraf = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HadithClusters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hadiths_HadithClusterId",
                table: "Hadiths",
                column: "HadithClusterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Hadiths_HadithClusters_HadithClusterId",
                table: "Hadiths",
                column: "HadithClusterId",
                principalTable: "HadithClusters",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hadiths_HadithClusters_HadithClusterId",
                table: "Hadiths");

            migrationBuilder.DropTable(
                name: "HadithClusters");

            migrationBuilder.DropIndex(
                name: "IX_Hadiths_HadithClusterId",
                table: "Hadiths");

            migrationBuilder.DropColumn(
                name: "GawamiAlemId",
                table: "ScholarEvaluations");

            migrationBuilder.DropColumn(
                name: "GawamiRawyId",
                table: "ScholarEvaluations");

            migrationBuilder.DropColumn(
                name: "GawamiId",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "HasMukhtalit",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "IsMudallis",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "HadithClusterId",
                table: "Hadiths");
        }
    }
}
