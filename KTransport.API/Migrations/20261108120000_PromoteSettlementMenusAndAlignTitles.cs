using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// Menu/entitlement alignment fix (RBAC clean-slate follow-up).
    ///
    /// Problem observed in production testing: the two "settlement" modules
    /// (<c>trip_settlement</c>, <c>delivery_settlement</c>) are independently
    /// provisionable in <c>tenant_modules</c> and are listed as first-class
    /// modules in the platform-admin provisioning UI, yet their ONLY menu rows
    /// were seeded as CHILDREN of a different module's menu item
    /// (trip_settlement under trips, delivery_settlement under consignments).
    /// Two defects follow from that cross-module nesting:
    ///   1. Cosmetic: an admin who grants "Trip Settlement" sees no matching
    ///      top-level entry — it is buried inside "Trips &amp; Manifests".
    ///   2. Correctness: <see cref="Services.MenuCatalogService"/> renders a
    ///      strict two-level tree and drops a parent whose own module is not
    ///      entitled. A tenant granted <c>trip_settlement</c> but NOT <c>trips</c>
    ///      loses the parent row and the child orphans — the page becomes
    ///      unreachable from the menu entirely.
    ///
    /// Fix (forward-only, idempotent, data-only — no schema change):
    ///   A. Promote both settlement rows to top-level (parent_id = NULL) so each
    ///      enabled module has its own menu presence and can never orphan.
    ///   B. Re-number the top-level display_order deterministically so each
    ///      settlement sits immediately after its operational sibling and no two
    ///      rows tie (ties render in non-deterministic order).
    ///   C. Keep every top-level menu title identical to its module name — the
    ///      same string the provisioning UI and the entitlement-ceiling view
    ///      show — so "what I granted" and "what the tenant sees" read the same.
    ///      Derived from modules.name (single source of truth); module_id is
    ///      already populated by 20261010103000_MenuRbacNormalization and the
    ///      parent_key→parent_id conversion is already done by
    ///      20261107191607_RbacMenuConsolidation, both of which precede this.
    ///
    /// Runs on existing and freshly-wiped databases alike. Down is a deliberate
    /// no-op: the nested/mis-titled layout was the defect, not a state to restore.
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261108120000_PromoteSettlementMenusAndAlignTitles")]
    public partial class PromoteSettlementMenusAndAlignTitles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- A. Promote the settlement modules to their own top-level menu entries.
UPDATE menu_items
   SET parent_id = NULL
 WHERE key IN ('trip_settlement', 'delivery_settlement')
   AND parent_id IS NOT NULL;

-- B. Deterministic top-level ordering (settlement follows its operational parent).
UPDATE menu_items SET display_order = CASE key
    WHEN 'dashboard'           THEN 0
    WHEN 'analytics'           THEN 1
    WHEN 'consignments'        THEN 2
    WHEN 'quotations'          THEN 3
    WHEN 'trips'               THEN 4
    WHEN 'trip_settlement'     THEN 5
    WHEN 'pod'                 THEN 6
    WHEN 'delivery_settlement' THEN 7
    WHEN 'billing'             THEN 8
    WHEN 'master_data'         THEN 9
    WHEN 'vendors'             THEN 10
    WHEN 'claims'              THEN 11
    WHEN 'reports'             THEN 12
    WHEN 'tracking'            THEN 13
    WHEN 'accounting'          THEN 14
    WHEN 'clients'             THEN 15
    WHEN 'system'              THEN 16
    ELSE display_order
  END
WHERE parent_id IS NULL;

-- C. Keep every top-level menu title equal to its module name.
UPDATE menu_items mi
   SET title = m.name
  FROM modules m
 WHERE mi.module_id = m.id
   AND mi.parent_id IS NULL
   AND mi.title <> m.name;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way data fix. Re-nesting the settlement modules or restoring the
            // old descriptive titles would reinstate the defect this corrects.
        }
    }
}
