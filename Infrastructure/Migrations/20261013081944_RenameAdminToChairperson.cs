using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAdminToChairperson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Role is stored as text, so no schema change: just rename the old value.
            migrationBuilder.Sql("UPDATE \"StokvelMembers\" SET \"Role\" = 'Chairperson' WHERE \"Role\" = 'Admin';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Treasurer has no old equivalent: fold it back into Member.
            migrationBuilder.Sql("UPDATE \"StokvelMembers\" SET \"Role\" = 'Admin' WHERE \"Role\" = 'Chairperson';");
            migrationBuilder.Sql("UPDATE \"StokvelMembers\" SET \"Role\" = 'Member' WHERE \"Role\" = 'Treasurer';");
        }
    }
}
