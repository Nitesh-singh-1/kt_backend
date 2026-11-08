using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// Migration A: Deduplicate role_permissions and add unique constraint
    /// uq_role_permissions_role_perm on (role_id, permission_id).
    /// Idempotent.
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261013220000_AddRolePermissionsUniqueConstraintAndDedupe")]
    public partial class AddRolePermissionsUniqueConstraintAndDedupe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Delete exact duplicate (role_id, permission_id) rows (keep MIN(ctid))
DELETE FROM role_permissions a
USING role_permissions b
WHERE a.ctid > b.ctid
  AND a.role_id = b.role_id
  AND a.permission_id = b.permission_id;

-- Add unique constraint to prevent future dupes (guarded by IF NOT EXISTS)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'uq_role_permissions_role_perm'
  ) THEN
    ALTER TABLE role_permissions
      ADD CONSTRAINT uq_role_permissions_role_perm UNIQUE (role_id, permission_id);
  END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE role_permissions
  DROP CONSTRAINT IF EXISTS uq_role_permissions_role_perm;
");
        }
    }
}
