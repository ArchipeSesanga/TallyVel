using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyVel.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionPagingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contributions_StokvelId_Cycle",
                table: "Contributions");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_StokvelId_Cycle_RecordedAt_Id",
                table: "Contributions",
                columns: new[] { "StokvelId", "Cycle", "RecordedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contributions_StokvelId_Cycle_RecordedAt_Id",
                table: "Contributions");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_StokvelId_Cycle",
                table: "Contributions",
                columns: new[] { "StokvelId", "Cycle" });
        }
    }
}
