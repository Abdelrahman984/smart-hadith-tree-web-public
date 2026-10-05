using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHadithTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIlalEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Schema drift ───────────────────────────────────────────
            // ItqanId/ItqanGrade and the Gawami columns were added to the model without
            // a migration, so some databases already have them (patched manually) and
            // others don't. Guard each change so the migration works on both.
            migrationBuilder.Sql(@"
IF COL_LENGTH('Narrators', 'ItqanId') IS NULL ALTER TABLE [Narrators] ADD [ItqanId] int NULL;
IF COL_LENGTH('Narrators', 'ItqanGrade') IS NULL ALTER TABLE [Narrators] ADD [ItqanGrade] nvarchar(50) NULL;
IF COL_LENGTH('Narrators', 'GawamiId') IS NULL ALTER TABLE [Narrators] ADD [GawamiId] int NULL;
IF COL_LENGTH('Narrators', 'IsMudallis') IS NULL ALTER TABLE [Narrators] ADD [IsMudallis] bit NOT NULL DEFAULT CAST(0 AS bit);
IF COL_LENGTH('Narrators', 'HasMukhtalit') IS NULL ALTER TABLE [Narrators] ADD [HasMukhtalit] bit NOT NULL DEFAULT CAST(0 AS bit);
IF COL_LENGTH('ScholarEvaluations', 'GawamiAlemId') IS NULL ALTER TABLE [ScholarEvaluations] ADD [GawamiAlemId] int NULL;
IF COL_LENGTH('ScholarEvaluations', 'GawamiRawyId') IS NULL ALTER TABLE [ScholarEvaluations] ADD [GawamiRawyId] int NULL;
IF COL_LENGTH('Hadiths', 'HadithClusterId') IS NULL ALTER TABLE [Hadiths] ADD [HadithClusterId] uniqueidentifier NULL;
IF OBJECT_ID(N'[HadithClusters]', N'U') IS NULL
    CREATE TABLE [HadithClusters] (
        [Id] uniqueidentifier NOT NULL,
        [GawamiClusterId] nvarchar(max) NOT NULL,
        [Taraf] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_HadithClusters] PRIMARY KEY ([Id])
    );
");

            // Indexes/FKs referencing the columns above must be created in a separate batch.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Narrators_ItqanId' AND object_id = OBJECT_ID('Narrators'))
    CREATE UNIQUE INDEX [IX_Narrators_ItqanId] ON [Narrators] ([ItqanId]) WHERE [ItqanId] IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Hadiths_HadithClusterId' AND object_id = OBJECT_ID('Hadiths'))
    CREATE INDEX [IX_Hadiths_HadithClusterId] ON [Hadiths] ([HadithClusterId]);
IF OBJECT_ID(N'[FK_Hadiths_HadithClusters_HadithClusterId]', N'F') IS NULL
    ALTER TABLE [Hadiths] ADD CONSTRAINT [FK_Hadiths_HadithClusters_HadithClusterId]
        FOREIGN KEY ([HadithClusterId]) REFERENCES [HadithClusters] ([Id]);
");

            // ── Ilal engine ────────────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "IkhtilatNote",
                table: "Narrators",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                collation: "Arabic_100_CI_AI");

            migrationBuilder.AddColumn<int>(
                name: "MudallisTier",
                table: "Narrators",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MukhtalitHearings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MukhtalitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timing = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MukhtalitHearings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MukhtalitHearings_Narrators_MukhtalitId",
                        column: x => x.MukhtalitId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MukhtalitHearings_Narrators_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NarratorRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NarratorRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NarratorRelations_Narrators_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NarratorRelations_Narrators_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MukhtalitHearings_MukhtalitId_StudentId",
                table: "MukhtalitHearings",
                columns: new[] { "MukhtalitId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MukhtalitHearings_StudentId",
                table: "MukhtalitHearings",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_NarratorRelations_StudentId_TeacherId",
                table: "NarratorRelations",
                columns: new[] { "StudentId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_NarratorRelations_TeacherId_StudentId",
                table: "NarratorRelations",
                columns: new[] { "TeacherId", "StudentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the Ilal engine objects are reverted; the drift columns belong to the
            // model from earlier commits and are left in place.
            migrationBuilder.DropTable(
                name: "MukhtalitHearings");

            migrationBuilder.DropTable(
                name: "NarratorRelations");

            migrationBuilder.DropColumn(
                name: "IkhtilatNote",
                table: "Narrators");

            migrationBuilder.DropColumn(
                name: "MudallisTier",
                table: "Narrators");
        }
    }
}
