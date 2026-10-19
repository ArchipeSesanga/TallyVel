using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCycleDatesAndArrears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "ContributionCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndsOn",
                table: "ContributionCycles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartsOn",
                table: "ContributionCycles",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Arrears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StokvelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortfallCents = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arrears", x => x.Id);
                    table.CheckConstraint("CK_Arrears_Shortfall", "\"ShortfallCents\" > 0");
                    table.ForeignKey(
                        name: "FK_Arrears_ContributionCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "ContributionCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Arrears_Stokvels_StokvelId",
                        column: x => x.StokvelId,
                        principalTable: "Stokvels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Arrears_Users_MemberUserId",
                        column: x => x.MemberUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContributionCycles_EndsOn_ClosedAt",
                table: "ContributionCycles",
                columns: new[] { "EndsOn", "ClosedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContributionCycles_StokvelId_StartsOn",
                table: "ContributionCycles",
                columns: new[] { "StokvelId", "StartsOn" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContributionCycles_Dates",
                table: "ContributionCycles",
                sql: "\"EndsOn\" >= \"StartsOn\"");

            migrationBuilder.CreateIndex(
                name: "IX_Arrears_CycleId_MemberUserId",
                table: "Arrears",
                columns: new[] { "CycleId", "MemberUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Arrears_MemberUserId",
                table: "Arrears",
                column: "MemberUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Arrears_StokvelId",
                table: "Arrears",
                column: "StokvelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Arrears");

            migrationBuilder.DropIndex(
                name: "IX_ContributionCycles_EndsOn_ClosedAt",
                table: "ContributionCycles");

            migrationBuilder.DropIndex(
                name: "IX_ContributionCycles_StokvelId_StartsOn",
                table: "ContributionCycles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContributionCycles_Dates",
                table: "ContributionCycles");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "ContributionCycles");

            migrationBuilder.DropColumn(
                name: "EndsOn",
                table: "ContributionCycles");

            migrationBuilder.DropColumn(
                name: "StartsOn",
                table: "ContributionCycles");
        }
    }
}
