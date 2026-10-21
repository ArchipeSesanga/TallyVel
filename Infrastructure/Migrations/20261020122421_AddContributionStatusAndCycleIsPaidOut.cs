using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionStatusAndCycleIsPaidOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Contributions get a status. Every existing row was a payment the member
            // recorded and that already counted towards totals and payouts, so it becomes
            // "Paid": not "Pending" (which does not count and would change every total)
            // and not "Confirmed" (nobody has confirmed anything yet).
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Contributions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Paid");

            // A cycle's status is now worked out (PaidOut, else Closed if ClosedAt is set,
            // else Open). The only fact still stored is whether it was paid out.
            migrationBuilder.AddColumn<bool>(
                name: "IsPaidOut",
                table: "ContributionCycles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE \"ContributionCycles\" SET \"IsPaidOut\" = TRUE WHERE \"Status\" = 'PaidOut';");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ContributionCycles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ContributionCycles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Open");

            migrationBuilder.Sql(
                "UPDATE \"ContributionCycles\" SET \"Status\" = 'PaidOut' WHERE \"IsPaidOut\";");

            migrationBuilder.DropColumn(
                name: "IsPaidOut",
                table: "ContributionCycles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Contributions");
        }
    }
}
