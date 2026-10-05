using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHadithTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hadiths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    MatnArabic = table.Column<string>(type: "nvarchar(max)", nullable: false, collation: "Arabic_100_CI_AI"),
                    BookName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false, collation: "Arabic_100_CI_AI"),
                    HadithNumber = table.Column<int>(type: "int", nullable: false),
                    Volume = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Chapter = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, collation: "Arabic_100_CI_AI"),
                    FullIsnadText = table.Column<string>(type: "nvarchar(max)", nullable: true, collation: "Arabic_100_CI_AI")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hadiths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Narrators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    FullName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, collation: "Arabic_100_CI_AI"),
                    KnownAs = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, collation: "Arabic_100_CI_AI"),
                    Kunyah = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, collation: "Arabic_100_CI_AI"),
                    GenerationTier = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true, collation: "Arabic_100_CI_AI"),
                    BirthYearHijri = table.Column<int>(type: "int", nullable: true),
                    DeathYearHijri = table.Column<int>(type: "int", nullable: true),
                    BirthPlace = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, collation: "Arabic_100_CI_AI"),
                    DeathPlace = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, collation: "Arabic_100_CI_AI"),
                    Biography = table.Column<string>(type: "nvarchar(max)", nullable: true, collation: "Arabic_100_CI_AI")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Narrators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScholarEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    NarratorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScholarName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false, collation: "Arabic_100_CI_AI"),
                    EvaluationText = table.Column<string>(type: "nvarchar(max)", nullable: false, collation: "Arabic_100_CI_AI"),
                    SourceBook = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true, collation: "Arabic_100_CI_AI"),
                    VerdictRating = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, collation: "Arabic_100_CI_AI")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScholarEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScholarEvaluations_Narrators_NarratorId",
                        column: x => x.NarratorId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    HadithId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SheikhId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    TransmissionTerm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, collation: "Arabic_100_CI_AI")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transmissions_Hadiths_HadithId",
                        column: x => x.HadithId,
                        principalTable: "Hadiths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transmissions_Narrators_SheikhId",
                        column: x => x.SheikhId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transmissions_Narrators_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Narrators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hadiths_BookName_HadithNumber",
                table: "Hadiths",
                columns: new[] { "BookName", "HadithNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Hadiths_HadithNumber",
                table: "Hadiths",
                column: "HadithNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_DeathYearHijri",
                table: "Narrators",
                column: "DeathYearHijri");

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_FullName",
                table: "Narrators",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_Narrators_KnownAs",
                table: "Narrators",
                column: "KnownAs");

            migrationBuilder.CreateIndex(
                name: "IX_ScholarEvaluations_NarratorId",
                table: "ScholarEvaluations",
                column: "NarratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ScholarEvaluations_ScholarName",
                table: "ScholarEvaluations",
                column: "ScholarName");

            migrationBuilder.CreateIndex(
                name: "IX_Transmissions_HadithId_StepOrder",
                table: "Transmissions",
                columns: new[] { "HadithId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transmissions_SheikhId_StudentId",
                table: "Transmissions",
                columns: new[] { "SheikhId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transmissions_StudentId_SheikhId",
                table: "Transmissions",
                columns: new[] { "StudentId", "SheikhId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScholarEvaluations");

            migrationBuilder.DropTable(
                name: "Transmissions");

            migrationBuilder.DropTable(
                name: "Hadiths");

            migrationBuilder.DropTable(
                name: "Narrators");
        }
    }
}
