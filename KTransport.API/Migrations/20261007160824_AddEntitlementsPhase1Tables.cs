using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEntitlementsPhase1Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permission_parity_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    json_only_keys = table.Column<string>(type: "text", nullable: true),
                    tables_only_keys = table.Column<string>(type: "text", nullable: true),
                    logged_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("permission_parity_log_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    feature_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("permissions_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    role_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    permission_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    granted_by = table.Column<int>(type: "integer", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    revoked_by = table.Column<int>(type: "integer", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    superseded_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("role_permissions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_permissions_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_entitlement_subscriptions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    plan_tier = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    enabled_feature_keys = table.Column<List<string>>(type: "text[]", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    effective_until = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
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

            migrationBuilder.CreateTable(
                name: "tenant_report_entitlements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    report_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_by = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tenant_report_entitlements_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_report_entitlements_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_permission_overrides",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    permission_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_granted = table.Column<bool>(type: "boolean", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    granted_by = table.Column<int>(type: "integer", nullable: true),
                    superseded_by = table.Column<long>(type: "bigint", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_permission_overrides_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_perm_overrides_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_perm_overrides_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "permission_parity_log_tenant_logged_idx",
                table: "permission_parity_log",
                columns: new[] { "tenant_id", "logged_at" });

            migrationBuilder.CreateIndex(
                name: "permissions_feature_action_idx",
                table: "permissions",
                columns: new[] { "feature_key", "action" });

            migrationBuilder.CreateIndex(
                name: "permissions_key_key",
                table: "permissions",
                column: "key",
                unique: true);

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
                name: "tenant_ent_subs_tenant_active_idx",
                table: "tenant_entitlement_subscriptions",
                columns: new[] { "tenant_id", "effective_until" });

            migrationBuilder.CreateIndex(
                name: "tenant_report_entitlements_tenant_report_key",
                table: "tenant_report_entitlements",
                columns: new[] { "tenant_id", "report_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_user_id",
                table: "user_permission_overrides",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "user_perm_overrides_tenant_user_idx",
                table: "user_permission_overrides",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "user_perm_overrides_tenant_user_perm_key",
                table: "user_permission_overrides",
                columns: new[] { "tenant_id", "user_id", "permission_key" },
                unique: true,
                filter: "\"superseded_by\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permission_parity_log");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "tenant_entitlement_subscriptions");

            migrationBuilder.DropTable(
                name: "tenant_report_entitlements");

            migrationBuilder.DropTable(
                name: "user_permission_overrides");
        }
    }
}
