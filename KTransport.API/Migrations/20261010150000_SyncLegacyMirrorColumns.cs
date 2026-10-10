using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-049b — one-time sync of legacy mirror columns on
    /// <c>tenant_entitlement_subscriptions</c>.
    ///
    /// Context: TASK-049 moved the authoritative tenant entitlement set to the
    /// normalized <c>tenant_modules</c> table but kept
    /// <c>enabled_feature_keys text[]</c> populated as a mirror for Phase A
    /// readers. For tenants that have NOT been saved since the rewrite, that
    /// column still contains pre-049 legacy dotted strings such as
    /// <c>consignments.delivery_settlement</c>. The GET endpoint has already
    /// been changed to derive from <c>tenant_modules</c>, but we also normalize
    /// the mirror column once so any other reader (and future diagnostics) sees
    /// the canonical module-code set.
    ///
    /// Also nulls out legacy JSON blob columns
    /// <c>role_overrides_json</c> / <c>user_overrides_json</c> if either still
    /// exists on the table — they were superseded by structured overrides in
    /// TASK-044 Phase 3 and are no longer serialized by the DTO. The column
    /// existence is checked through <c>information_schema</c> so the migration
    /// is a no-op if the columns were already dropped.
    ///
    /// Idempotent: all statements are safe to re-run.
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261010150000_SyncLegacyMirrorColumns")]
    public partial class SyncLegacyMirrorColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------
            // 1. Rewrite enabled_feature_keys to the authoritative module-code
            //    set for every active subscription row.
            //
            //    "Active" = effective_until IS NULL.
            //    "Authoritative set" = modules.code where tenant_modules row is
            //    active (enabled_until IS NULL) for that tenant.
            //
            //    When a tenant has no active tenant_modules rows the column is
            //    rewritten to an empty array — that is the correct Phase A
            //    mirror of "no modules enabled".
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
UPDATE tenant_entitlement_subscriptions s
SET enabled_feature_keys = COALESCE(mc.codes, ARRAY[]::text[])
FROM (
    SELECT tm.tenant_id,
           array_agg(m.code ORDER BY m.code) AS codes
    FROM tenant_modules tm
    JOIN modules m ON m.id = tm.module_id
    WHERE tm.enabled_until IS NULL
    GROUP BY tm.tenant_id
) mc
WHERE s.effective_until IS NULL
  AND s.tenant_id = mc.tenant_id;

-- Also handle tenants with an active subscription but zero active modules —
-- the join above would skip them. Rewrite their mirror to the empty array if
-- it still carries legacy content.
UPDATE tenant_entitlement_subscriptions s
SET enabled_feature_keys = ARRAY[]::text[]
WHERE s.effective_until IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM tenant_modules tm
      WHERE tm.tenant_id = s.tenant_id
        AND tm.enabled_until IS NULL
  )
  AND s.enabled_feature_keys IS DISTINCT FROM ARRAY[]::text[];
");

            // ---------------------------------------------------------------
            // 2. NULL legacy role_overrides_json / user_overrides_json columns
            //    if they still exist. Guard each via information_schema so the
            //    migration passes cleanly whether or not the columns were
            //    already dropped in a prior release.
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
DO $do$
BEGIN
    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_name = 'tenant_entitlement_subscriptions'
           AND column_name = 'role_overrides_json'
    ) THEN
        EXECUTE 'UPDATE tenant_entitlement_subscriptions
                    SET role_overrides_json = NULL
                  WHERE role_overrides_json IS NOT NULL';
    END IF;

    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_name = 'tenant_entitlement_subscriptions'
           AND column_name = 'user_overrides_json'
    ) THEN
        EXECUTE 'UPDATE tenant_entitlement_subscriptions
                    SET user_overrides_json = NULL
                  WHERE user_overrides_json IS NOT NULL';
    END IF;
END $do$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way data cleanup — the legacy dotted strings this migration
            // overwrote are not recoverable, and the nulled JSON blobs are
            // deliberately obsolete. Down is a deliberate no-op.
        }
    }
}
