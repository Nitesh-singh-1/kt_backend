using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-046 Phase 1 — Backfill tenant_modules from existing
    /// tenant_entitlement_subscriptions.enabled_feature_keys. For every
    /// tenant with an active subscription, we map each feature key to its
    /// module code and INSERT a tenant_modules row (idempotent via the
    /// partial unique index tenant_modules_tenant_module_active_uq).
    ///
    /// The source text[] column is NOT touched — it stays populated as a
    /// one-release rollback cushion per the owner's §Y rule.
    ///
    /// Expected row count: #active-subscriptions × #distinct-modules-from-keys,
    /// typically 1-15 per tenant.
    /// </summary>
    public partial class BackfillTenantModules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One SQL block — a CTE expands enabled_feature_keys to module codes
            // using a static mapping, then LEFT JOINs the modules catalog so an
            // unmapped key is dropped silently. The INSERT ... ON CONFLICT clause
            // guards the partial unique index.
            migrationBuilder.Sql(@"
WITH feature_to_module AS (
    SELECT * FROM (VALUES
        ('billing',                 'billing'),
        ('billing.bill_book',       'billing'),
        ('billing.invoices',        'billing'),
        ('billing.receipts',        'billing'),
        ('consignments',            'bilty'),
        ('consignments.create',     'bilty'),
        ('consignments.all',        'bilty'),
        ('delivery_settlement',     'delivery_settlement'),
        ('trips',                   'trips'),
        ('trip_settlement',         'trip_settlement'),
        ('empty_trips',             'trips'),
        ('pod',                     'pod'),
        ('master_data',             'master_data'),
        ('vendors',                 'vendors'),
        ('claims',                  'claims'),
        ('quotations',              'quotations'),
        ('reports',                 'reports'),
        ('tracking',                'tracking'),
        ('analytics',               'analytics'),
        ('dashboard',               'dashboard'),
        ('system',                  'system')
    ) AS t(feature_key, module_code)
),
expanded AS (
    SELECT s.tenant_id,
           CASE
               WHEN fkey LIKE 'master_data.%' THEN 'master_data'
               WHEN fkey LIKE 'reports.%'     THEN 'reports'
               WHEN fkey LIKE 'system.%'      THEN 'system'
               WHEN fkey LIKE 'billing.%'     THEN 'billing'
               WHEN fkey LIKE 'consignments.%' THEN 'bilty'
               ELSE (SELECT module_code FROM feature_to_module WHERE feature_key = fkey)
           END AS module_code
      FROM tenant_entitlement_subscriptions s,
           LATERAL UNNEST(COALESCE(s.enabled_feature_keys, ARRAY[]::text[])) AS fkey
     WHERE s.effective_until IS NULL
)
INSERT INTO tenant_modules (tenant_id, module_id, is_enabled, enabled_from, enabled_until, created_at)
SELECT DISTINCT e.tenant_id, m.id, TRUE, CURRENT_TIMESTAMP, NULL::timestamp, CURRENT_TIMESTAMP
  FROM expanded e
  JOIN modules m ON m.code = e.module_code
 WHERE e.module_code IS NOT NULL
ON CONFLICT (tenant_id, module_id) WHERE enabled_until IS NULL DO NOTHING;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // TASK-046 §Y: backfilled rows stay. Rollback is a schema-level revert
            // (which this migration intentionally does not perform — see
            // AddRbacPhase1Tables.Down for the shared rationale).
        }
    }
}
