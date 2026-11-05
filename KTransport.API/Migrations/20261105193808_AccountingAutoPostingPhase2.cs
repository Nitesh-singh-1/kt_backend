using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AccountingAutoPostingPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "vouchers_tenant_source_idx",
                table: "vouchers");

            migrationBuilder.CreateTable(
                name: "accounting_settings",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auto_posting_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    default_bank_account_id = table.Column<long>(type: "bigint", nullable: true),
                    enabled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    enabled_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("accounting_settings_pkey", x => x.tenant_id);
                    table.ForeignKey(
                        name: "fk_accounting_settings_default_bank",
                        column: x => x.default_bank_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_accounting_settings_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "auto_posting_queue",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    source_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "Pending"),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    enqueued_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    processed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("auto_posting_queue_pkey", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "vouchers_tenant_source_live_uq",
                table: "vouchers",
                columns: new[] { "tenant_id", "source_type", "source_id" },
                unique: true,
                filter: "source_id IS NOT NULL AND reversal_of_id IS NULL AND status <> 'Reversed'");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_settings_default_bank_account_id",
                table: "accounting_settings",
                column: "default_bank_account_id");

            migrationBuilder.CreateIndex(
                name: "auto_posting_queue_tenant_status_idx",
                table: "auto_posting_queue",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounting_settings");

            migrationBuilder.DropTable(
                name: "auto_posting_queue");

            migrationBuilder.DropIndex(
                name: "vouchers_tenant_source_live_uq",
                table: "vouchers");

            migrationBuilder.CreateIndex(
                name: "vouchers_tenant_source_idx",
                table: "vouchers",
                columns: new[] { "tenant_id", "source_type", "source_id" });
        }
    }
}
