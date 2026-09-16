using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace uCredit.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLegacyCompanyScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantLegacyCompanyScopes",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantLegacyCompanyScopes", x => new { x.TenantId, x.CompanyId });
                    table.ForeignKey(
                        name: "FK_TenantLegacyCompanyScopes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantLegacyCompanyScopes");
        }
    }
}
