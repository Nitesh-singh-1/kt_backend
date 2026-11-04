using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AccountingLedgerPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: this migration is deliberately accounting-only. EF also wanted to add
            // module_id/permission_id columns + FKs to the RBAC/menu tables, but those are a
            // pre-existing model-vs-snapshot drift in this repo (the columns were applied via
            // earlier raw-SQL migrations and already exist in the database). Emitting them here
            // would fail against a real DB and collide with the RBAC branch, so they are omitted;
            // regenerating the snapshot absorbs the drift so later migrations won't re-emit them.
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    account_type = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    nature = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    is_group = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    source_party_id = table.Column<long>(type: "bigint", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("accounts_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_accounts_parent",
                        column: x => x.parent_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_accounts_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_periods",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    fy_label = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "Open"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("financial_periods_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_financial_periods_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opening_balances",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    period_id = table.Column<long>(type: "bigint", nullable: false),
                    dr_cr = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("opening_balances_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_opening_balances_account",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_opening_balances_period",
                        column: x => x.period_id,
                        principalTable: "financial_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vouchers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    voucher_type = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    voucher_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    period_id = table.Column<long>(type: "bigint", nullable: false),
                    narration = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "Posted"),
                    source_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    source_id = table.Column<long>(type: "bigint", nullable: true),
                    reversal_of_id = table.Column<long>(type: "bigint", nullable: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("vouchers_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_vouchers_period",
                        column: x => x.period_id,
                        principalTable: "financial_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vouchers_reversal",
                        column: x => x.reversal_of_id,
                        principalTable: "vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vouchers_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "voucher_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    voucher_id = table.Column<long>(type: "bigint", nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    dr_cr = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    line_narration = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("voucher_lines_pkey", x => x.id);
                    table.CheckConstraint("ck_voucher_lines_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "fk_voucher_lines_account",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_voucher_lines_voucher",
                        column: x => x.voucher_id,
                        principalTable: "vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "accounts_tenant_code_uq",
                table: "accounts",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "accounts_tenant_parent_idx",
                table: "accounts",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_parent_id",
                table: "accounts",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "financial_periods_tenant_fy_uq",
                table: "financial_periods",
                columns: new[] { "tenant_id", "fy_label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_opening_balances_account_id",
                table: "opening_balances",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_opening_balances_period_id",
                table: "opening_balances",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "opening_balances_tenant_acct_period_uq",
                table: "opening_balances",
                columns: new[] { "tenant_id", "account_id", "period_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_lines_account_id",
                table: "voucher_lines",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_lines_voucher_id",
                table: "voucher_lines",
                column: "voucher_id");

            migrationBuilder.CreateIndex(
                name: "voucher_lines_tenant_account_idx",
                table: "voucher_lines",
                columns: new[] { "tenant_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "voucher_lines_tenant_voucher_idx",
                table: "voucher_lines",
                columns: new[] { "tenant_id", "voucher_id" });

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_period_id",
                table: "vouchers",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_reversal_of_id",
                table: "vouchers",
                column: "reversal_of_id");

            migrationBuilder.CreateIndex(
                name: "vouchers_tenant_date_idx",
                table: "vouchers",
                columns: new[] { "tenant_id", "date" });

            migrationBuilder.CreateIndex(
                name: "vouchers_tenant_source_idx",
                table: "vouchers",
                columns: new[] { "tenant_id", "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "vouchers_tenant_type_no_uq",
                table: "vouchers",
                columns: new[] { "tenant_id", "voucher_type", "voucher_no" },
                unique: true);

            // Balancing invariant (DB half; the service validates too): at transaction commit,
            // every voucher's debit and credit lines must net to zero. A DEFERRED constraint
            // trigger lets all lines of a voucher be inserted before the check runs.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION check_voucher_balanced() RETURNS trigger AS $$
                DECLARE
                    v_id bigint;
                    diff numeric;
                BEGIN
                    v_id := COALESCE(NEW.voucher_id, OLD.voucher_id);
                    SELECT COALESCE(SUM(CASE WHEN dr_cr = 'DR' THEN amount ELSE -amount END), 0)
                        INTO diff FROM voucher_lines WHERE voucher_id = v_id;
                    IF diff <> 0 THEN
                        RAISE EXCEPTION 'Voucher % is not balanced: debits minus credits = %', v_id, diff;
                    END IF;
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;

                CREATE CONSTRAINT TRIGGER trg_voucher_lines_balanced
                    AFTER INSERT OR UPDATE OR DELETE ON voucher_lines
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION check_voucher_balanced();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_voucher_lines_balanced ON voucher_lines;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS check_voucher_balanced();");

            migrationBuilder.DropTable(
                name: "opening_balances");

            migrationBuilder.DropTable(
                name: "voucher_lines");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "vouchers");

            migrationBuilder.DropTable(
                name: "financial_periods");
        }
    }
}
