using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// Backfill for the trips branch of the menu catalog.
    ///
    /// Two independent fixes, both idempotent:
    ///
    /// 1. UPDATE the parent "trips" row's title to "Manifest &amp; Dispatch".
    ///    The seed catalog previously carried the title "Challan", which read
    ///    as redundant alongside the child row "Truck Challan". Any DB that
    ///    seeded under the old title gets corrected; a DB already carrying the
    ///    new title is a no-op.
    ///
    /// 2. INSERT the "trips.challan" child row if it is missing. The seed
    ///    catalog file was amended after SeedMenuItemsCatalog had already been
    ///    recorded in __EFMigrationsHistory on some environments (notably UAT),
    ///    so EF never re-ran the seed and that row never arrived. INSERT uses
    ///    ON CONFLICT (key) DO NOTHING, so DBs that already carry the row are
    ///    untouched.
    ///
    /// module_id 4 matches the "trips" module seeded by SeedModulesCatalog.
    /// permission_key, icon and path match the canonical catalog in
    /// MenuCatalogSeedData.cs.
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261012180000_BackfillTripsMenuCatalog")]
    public partial class BackfillTripsMenuCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE menu_items
   SET title = 'Manifest & Dispatch'
 WHERE key = 'trips'
   AND title <> 'Manifest & Dispatch';

INSERT INTO menu_items
    (key,              parent_key, title,           path,     icon,    permission_key, badge, display_order, visibility_rule, is_active, module_id)
VALUES
    ('trips.challan',  'trips',    'Truck Challan', '/trips', 'truck', 'trips.view',   NULL,  0,             NULL,            true,      4)
ON CONFLICT (key) DO NOTHING;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way data fix. We do not reinstate the misleading "Challan"
            // parent title, and we do not drop the trips.challan row — the
            // canonical catalog expects both. Down is a deliberate no-op.
        }
    }
}
