using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentPaymentsLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shipment_payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    shipment_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    payment_mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reference_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false),
                    invoice_id = table.Column<long>(type: "bigint", nullable: true),
                    is_reversed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reversed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    reversed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("shipment_payments_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_shipment_payments_shipment",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shipment_payments_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_payments_shipment_id",
                table: "shipment_payments",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "shipment_payments_tenant_shipment_idx",
                table: "shipment_payments",
                columns: new[] { "tenant_id", "shipment_id" });

            // Backfill: every bilty with money already received (pre-ledger) gets one synthetic
            // OpeningBackfill row (source = 4) so its payment is reversible. Idempotent.
            migrationBuilder.Sql(@"
                INSERT INTO shipment_payments
                    (tenant_id, shipment_id, amount, payment_mode, reference_no, payment_date, source, is_reversed, created_at)
                SELECT s.tenant_id, s.id, s.paid_amount, s.settled_payment_mode, s.settlement_reference_no,
                       COALESCE(s.delivery_date, CURRENT_DATE), 4, false, CURRENT_TIMESTAMP
                FROM shipments s
                WHERE s.paid_amount > 0
                  AND NOT EXISTS (SELECT 1 FROM shipment_payments p WHERE p.shipment_id = s.id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipment_payments");
        }
    }
}
