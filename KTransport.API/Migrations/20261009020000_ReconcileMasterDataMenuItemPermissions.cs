using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// Reconciles the menu_items table permission_key values for master_data items
    /// so each child menu item uses its canonical granular permission key
    /// (e.g. master_data.tyres.view instead of the shared fleet.view).
    /// </summary>
    public partial class ReconcileMasterDataMenuItemPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE menu_items SET permission_key = 'master_data.parties.view'    WHERE key = 'master_data.parties';
                UPDATE menu_items SET permission_key = 'master_data.fleet.view'      WHERE key = 'master_data.fleet';
                UPDATE menu_items SET permission_key = 'master_data.compliance.view' WHERE key = 'master_data.compliance';
                UPDATE menu_items SET permission_key = 'master_data.tyres.view'      WHERE key = 'master_data.tyres';
                UPDATE menu_items SET permission_key = 'master_data.spares.view'     WHERE key = 'master_data.spares';
                UPDATE menu_items SET permission_key = 'master_data.loans.view'      WHERE key = 'master_data.loans';
                UPDATE menu_items SET permission_key = 'master_data.rates.view'      WHERE key = 'master_data.rates';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE menu_items SET permission_key = 'parties.view' WHERE key = 'master_data.parties';
                UPDATE menu_items SET permission_key = 'fleet.view'   WHERE key = 'master_data.fleet';
                UPDATE menu_items SET permission_key = 'fleet.view'   WHERE key = 'master_data.compliance';
                UPDATE menu_items SET permission_key = 'fleet.view'   WHERE key = 'master_data.tyres';
                UPDATE menu_items SET permission_key = 'fleet.view'   WHERE key = 'master_data.spares';
                UPDATE menu_items SET permission_key = 'fleet.view'   WHERE key = 'master_data.loans';
                UPDATE menu_items SET permission_key = 'rates.view'   WHERE key = 'master_data.rates';
            ");
        }
    }
}
