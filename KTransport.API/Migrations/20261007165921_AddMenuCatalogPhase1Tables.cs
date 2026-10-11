using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuCatalogPhase1Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "menu_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    parent_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    icon = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    permission_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    badge = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    visibility_rule = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("menu_items_pkey", x => x.id);
                    table.UniqueConstraint("AK_menu_items_key", x => x.key);
                    table.ForeignKey(
                        name: "fk_menu_items_parent",
                        column: x => x.parent_key,
                        principalTable: "menu_items",
                        principalColumn: "key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "menu_parity_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    user_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    code_only_keys = table.Column<List<string>>(type: "text[]", nullable: false),
                    tables_only_keys = table.Column<List<string>>(type: "text[]", nullable: false),
                    logged_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("menu_parity_log_pkey", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "menu_items_active_idx",
                table: "menu_items",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "menu_items_key_key",
                table: "menu_items",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "menu_items_parent_key_idx",
                table: "menu_items",
                columns: new[] { "parent_key", "display_order" });

            migrationBuilder.CreateIndex(
                name: "menu_parity_log_logged_at_idx",
                table: "menu_parity_log",
                column: "logged_at");

            migrationBuilder.CreateIndex(
                name: "menu_parity_log_tenant_idx",
                table: "menu_parity_log",
                columns: new[] { "tenant_id", "logged_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "menu_items");

            migrationBuilder.DropTable(
                name: "menu_parity_log");
        }
    }
}
