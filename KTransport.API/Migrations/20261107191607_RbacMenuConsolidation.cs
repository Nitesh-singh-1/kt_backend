using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class RbacMenuConsolidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- 0. drop the old string self-FK + the retired subscriptions table ----
            migrationBuilder.DropForeignKey(
                name: "fk_menu_items_parent",
                table: "menu_items");

            migrationBuilder.DropTable(
                name: "tenant_entitlement_subscriptions");

            // ---- 1. add the new structural columns FIRST (so we can backfill them
            //         from the legacy columns before those are dropped) ----
            migrationBuilder.AddColumn<string>(
                name: "page_key",
                table: "permissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "parent_id",
                table: "menu_items",
                type: "integer",
                nullable: true);

            // ---- 2. CANONICAL CATALOG RECONCILE + BACKFILL (clean-slate) ----
            // Ensure the 17 canonical modules exist (prior migrations seed 16;
            // this guarantees `clients` + any gap). Canonical codes only — no aliases.
            migrationBuilder.Sql(@"
                INSERT INTO modules (code, name, description, is_premium, is_active, display_order, created_at) VALUES
                  ('dashboard','Dashboard','Operational dashboard',FALSE,TRUE,0,CURRENT_TIMESTAMP),
                  ('bilty','Bilty / Consignments','Consignment booking, GR/LR',FALSE,TRUE,1,CURRENT_TIMESTAMP),
                  ('pod','Proof of Delivery','POD capture and audit',FALSE,TRUE,2,CURRENT_TIMESTAMP),
                  ('trips','Trips & Manifests','Trip planning and manifests',FALSE,TRUE,3,CURRENT_TIMESTAMP),
                  ('billing','Billing & Invoicing','Bill book, invoices, receipts',FALSE,TRUE,4,CURRENT_TIMESTAMP),
                  ('master_data','Master Data','Parties, fleet, rates, compliance',FALSE,TRUE,5,CURRENT_TIMESTAMP),
                  ('reports','Reports','Operational and financial reports',FALSE,TRUE,6,CURRENT_TIMESTAMP),
                  ('vendors','Vendors & Lorry Hire','Vendor master and payables',FALSE,TRUE,7,CURRENT_TIMESTAMP),
                  ('claims','Claims','Cargo and vehicle claims',FALSE,TRUE,8,CURRENT_TIMESTAMP),
                  ('quotations','Quotations','Freight quotations',FALSE,TRUE,9,CURRENT_TIMESTAMP),
                  ('tracking','Live Tracking','Vehicle live tracking',TRUE,TRUE,10,CURRENT_TIMESTAMP),
                  ('analytics','Analytics','Advanced analytics',TRUE,TRUE,11,CURRENT_TIMESTAMP),
                  ('system','System','Settings, users, onboarding',FALSE,TRUE,12,CURRENT_TIMESTAMP),
                  ('trip_settlement','Trip Settlement','Trip-level settlement',FALSE,TRUE,13,CURRENT_TIMESTAMP),
                  ('delivery_settlement','Delivery Settlement','Delivery-side settlement',FALSE,TRUE,14,CURRENT_TIMESTAMP),
                  ('accounting','Accounting & Finance','Double-entry ledger',TRUE,TRUE,15,CURRENT_TIMESTAMP),
                  ('clients','Client Management','Platform tenant provisioning',FALSE,TRUE,16,CURRENT_TIMESTAMP)
                ON CONFLICT (code) DO NOTHING;
            ");

            // Backfill menu_items.parent_id from the legacy parent_key self-ref.
            migrationBuilder.Sql(@"
                UPDATE menu_items c SET parent_id = p.id
                FROM menu_items p
                WHERE c.parent_key IS NOT NULL AND lower(c.parent_key) = lower(p.key);
            ");

            // Backfill permissions.page_key: a permission maps to the owning CHILD
            // menu_item (feature_key == a child menu_items.key). Module-level
            // permissions (reports.view, dashboard.view, module parents) stay NULL.
            migrationBuilder.Sql(@"
                UPDATE permissions pr SET page_key = mi.key
                FROM menu_items mi
                WHERE lower(mi.key) = lower(pr.feature_key) AND mi.parent_id IS NOT NULL;
            ");

            // Clean-slate: drop any legacy grant/override rows that never got a
            // structural FK (DB wipe authorized) so the NOT NULL alters are safe.
            migrationBuilder.Sql("DELETE FROM role_permissions WHERE role_id IS NULL OR permission_id IS NULL;");
            migrationBuilder.Sql("DELETE FROM user_permission_overrides WHERE permission_id IS NULL;");

            // ---- 3. drop legacy indexes ----
            migrationBuilder.DropIndex(
                name: "IX_users_tenant_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "users_username_key",
                table: "users");

            migrationBuilder.DropIndex(
                name: "user_perm_overrides_tenant_user_perm_key",
                table: "user_permission_overrides");

            migrationBuilder.DropIndex(
                name: "role_permissions_tenant_role_active_idx",
                table: "role_permissions");

            migrationBuilder.DropIndex(
                name: "role_permissions_tenant_role_perm_key",
                table: "role_permissions");

            migrationBuilder.DropIndex(
                name: "permissions_feature_action_idx",
                table: "permissions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_menu_items_key",
                table: "menu_items");

            migrationBuilder.DropIndex(
                name: "menu_items_parent_key_idx",
                table: "menu_items");

            // ---- 4. drop legacy columns ----
            migrationBuilder.DropColumn(
                name: "role",
                table: "users");

            migrationBuilder.DropColumn(
                name: "permission_key",
                table: "user_permission_overrides");

            migrationBuilder.DropColumn(
                name: "permission_key",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "role_name",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "feature_key",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "parent_key",
                table: "menu_items");

            // ---- 5. make the structural FKs NOT NULL ----
            migrationBuilder.AlterColumn<int>(
                name: "permission_id",
                table: "user_permission_overrides",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "role_id",
                table: "role_permissions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "permission_id",
                table: "role_permissions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "plan_module_pages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_id = table.Column<int>(type: "integer", nullable: false),
                    page_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("plan_module_pages_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_module_pages_module",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_module_pages_plan",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_modules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("plan_modules_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_modules_module",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_modules_plan",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_module_pages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    module_id = table.Column<int>(type: "integer", nullable: false),
                    page_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    enabled_from = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    enabled_until = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    enabled_by = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tenant_module_pages_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_module_pages_module",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_module_pages_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "users_tenant_username_key",
                table: "users",
                columns: new[] { "tenant_id", "username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "user_perm_overrides_tenant_user_perm_key",
                table: "user_permission_overrides",
                columns: new[] { "tenant_id", "user_id", "permission_id" },
                unique: true,
                filter: "\"superseded_by\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "role_permissions_tenant_role_active_idx",
                table: "role_permissions",
                columns: new[] { "tenant_id", "role_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "role_permissions_tenant_role_perm_key",
                table: "role_permissions",
                columns: new[] { "tenant_id", "role_id", "permission_id" },
                unique: true,
                filter: "\"revoked_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "permissions_page_key_idx",
                table: "permissions",
                column: "page_key");

            migrationBuilder.CreateIndex(
                name: "menu_items_parent_id_idx",
                table: "menu_items",
                columns: new[] { "parent_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_module_pages_module_id",
                table: "plan_module_pages",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "plan_module_pages_uq",
                table: "plan_module_pages",
                columns: new[] { "plan_id", "module_id", "page_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_modules_module_id",
                table: "plan_modules",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "plan_modules_plan_module_uq",
                table: "plan_modules",
                columns: new[] { "plan_id", "module_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_module_pages_module_id",
                table: "tenant_module_pages",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "tenant_module_pages_active_uq",
                table: "tenant_module_pages",
                columns: new[] { "tenant_id", "module_id", "page_key" },
                unique: true,
                filter: "\"enabled_until\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "tenant_module_pages_tenant_module_idx",
                table: "tenant_module_pages",
                columns: new[] { "tenant_id", "module_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_menu_items_parent",
                table: "menu_items",
                column: "parent_id",
                principalTable: "menu_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // Per-tenant, case-insensitive username uniqueness (two tenants may
            // each have an "admin"). Functional index — the EF model carries the
            // (tenant_id, username) stand-in index created above.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS users_tenant_lower_username_key ON users (tenant_id, lower(username));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_menu_items_parent",
                table: "menu_items");

            migrationBuilder.DropTable(
                name: "plan_module_pages");

            migrationBuilder.DropTable(
                name: "plan_modules");

            migrationBuilder.DropTable(
                name: "tenant_module_pages");

            migrationBuilder.DropIndex(
                name: "users_tenant_username_key",
                table: "users");

            migrationBuilder.DropIndex(
                name: "user_perm_overrides_tenant_user_perm_key",
                table: "user_permission_overrides");

            migrationBuilder.DropIndex(
                name: "role_permissions_tenant_role_active_idx",
                table: "role_permissions");

            migrationBuilder.DropIndex(
                name: "role_permissions_tenant_role_perm_key",
                table: "role_permissions");

            migrationBuilder.DropIndex(
                name: "permissions_page_key_idx",
                table: "permissions");

            migrationBuilder.DropIndex(
                name: "menu_items_parent_id_idx",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "page_key",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "menu_items");

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "permission_id",
                table: "user_permission_overrides",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "permission_key",
                table: "user_permission_overrides",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "role_id",
                table: "role_permissions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "permission_id",
                table: "role_permissions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "permission_key",
                table: "role_permissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "role_name",
                table: "role_permissions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "feature_key",
                table: "permissions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "parent_key",
                table: "menu_items",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_menu_items_key",
                table: "menu_items",
                column: "key");

            migrationBuilder.CreateTable(
                name: "tenant_entitlement_subscriptions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    effective_from = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    effective_until = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    enabled_feature_keys = table.Column<List<string>>(type: "text[]", nullable: false),
                    plan_tier = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tenant_entitlement_subscriptions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_ent_subs_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: 1,
                column: "role",
                value: "admin");

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id",
                table: "users",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "users_username_key",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "user_perm_overrides_tenant_user_perm_key",
                table: "user_permission_overrides",
                columns: new[] { "tenant_id", "user_id", "permission_key" },
                unique: true,
                filter: "\"superseded_by\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "role_permissions_tenant_role_active_idx",
                table: "role_permissions",
                columns: new[] { "tenant_id", "role_name", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "role_permissions_tenant_role_perm_key",
                table: "role_permissions",
                columns: new[] { "tenant_id", "role_name", "permission_key" },
                unique: true,
                filter: "\"revoked_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "permissions_feature_action_idx",
                table: "permissions",
                columns: new[] { "feature_key", "action" });

            migrationBuilder.CreateIndex(
                name: "menu_items_parent_key_idx",
                table: "menu_items",
                columns: new[] { "parent_key", "display_order" });

            migrationBuilder.CreateIndex(
                name: "tenant_ent_subs_tenant_active_idx",
                table: "tenant_entitlement_subscriptions",
                columns: new[] { "tenant_id", "effective_until" });

            migrationBuilder.AddForeignKey(
                name: "fk_menu_items_parent",
                table: "menu_items",
                column: "parent_key",
                principalTable: "menu_items",
                principalColumn: "key",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
