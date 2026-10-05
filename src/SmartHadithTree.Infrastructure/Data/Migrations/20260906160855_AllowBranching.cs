using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHadithTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowBranching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transmissions_HadithId_StepOrder",
                table: "Transmissions");

            migrationBuilder.CreateIndex(
                name: "IX_Transmissions_HadithId_StepOrder",
                table: "Transmissions",
                columns: new[] { "HadithId", "StepOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transmissions_HadithId_StepOrder",
                table: "Transmissions");

            migrationBuilder.CreateIndex(
                name: "IX_Transmissions_HadithId_StepOrder",
                table: "Transmissions",
                columns: new[] { "HadithId", "StepOrder" },
                unique: true);
        }
    }
}
