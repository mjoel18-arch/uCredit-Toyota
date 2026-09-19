using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace uCredit.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyUserCodeToUserTenantMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyUserCode",
                table: "UserTenantMemberships",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LegacyUserCode",
                table: "UserTenantMemberships");
        }
    }
}
