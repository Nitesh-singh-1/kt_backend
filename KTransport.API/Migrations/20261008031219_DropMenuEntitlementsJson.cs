using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class DropMenuEntitlementsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "menu_entitlements_json",
                table: "tenant_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // TASK-044 Phase 3: Down just re-adds an empty text column.
            // Data is NOT restorable via migration rollback — use the pre-apply pg_dump snapshot.
            migrationBuilder.AddColumn<string>(
                name: "menu_entitlements_json",
                table: "tenant_settings",
                type: "text",
                nullable: true);
        }
    }
}
