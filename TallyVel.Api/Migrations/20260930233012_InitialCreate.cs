using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallyVel.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stokvels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContributionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Cycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stokvels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContributionCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StokvelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContributionCycles", x => x.Id);
                    table.UniqueConstraint("AK_ContributionCycles_StokvelId_Id", x => new { x.StokvelId, x.Id });
                    table.UniqueConstraint("AK_ContributionCycles_StokvelId_Label", x => new { x.StokvelId, x.Label });
                    table.ForeignKey(
                        name: "FK_ContributionCycles_Stokvels_StokvelId",
                        column: x => x.StokvelId,
                        principalTable: "Stokvels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StokvelMembers",
                columns: table => new
                {
                    StokvelId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StokvelMembers", x => new { x.StokvelId, x.UserId });
                    table.ForeignKey(
                        name: "FK_StokvelMembers_Stokvels_StokvelId",
                        column: x => x.StokvelId,
                        principalTable: "Stokvels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StokvelMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StokvelId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contributions_ContributionCycles_StokvelId_Cycle",
                        columns: x => new { x.StokvelId, x.Cycle },
                        principalTable: "ContributionCycles",
                        principalColumns: new[] { "StokvelId", "Label" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contributions_Users_MemberUserId",
                        column: x => x.MemberUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StokvelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payouts_ContributionCycles_StokvelId_ContributionCycleId",
                        columns: x => new { x.StokvelId, x.ContributionCycleId },
                        principalTable: "ContributionCycles",
                        principalColumns: new[] { "StokvelId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payouts_StokvelMembers_StokvelId_RecipientUserId",
                        columns: x => new { x.StokvelId, x.RecipientUserId },
                        principalTable: "StokvelMembers",
                        principalColumns: new[] { "StokvelId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_MemberUserId",
                table: "Contributions",
                column: "MemberUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_StokvelId_Cycle",
                table: "Contributions",
                columns: new[] { "StokvelId", "Cycle" });

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_StokvelId_MemberUserId_Cycle",
                table: "Contributions",
                columns: new[] { "StokvelId", "MemberUserId", "Cycle" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_ContributionCycleId",
                table: "Payouts",
                column: "ContributionCycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_StokvelId_ContributionCycleId",
                table: "Payouts",
                columns: new[] { "StokvelId", "ContributionCycleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_StokvelId_RecipientUserId",
                table: "Payouts",
                columns: new[] { "StokvelId", "RecipientUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_StokvelMembers_UserId",
                table: "StokvelMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contributions");

            migrationBuilder.DropTable(
                name: "Payouts");

            migrationBuilder.DropTable(
                name: "ContributionCycles");

            migrationBuilder.DropTable(
                name: "StokvelMembers");

            migrationBuilder.DropTable(
                name: "Stokvels");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
