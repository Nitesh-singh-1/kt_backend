using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTripSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trip_settlements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    trip_id = table.Column<long>(type: "bigint", nullable: false),
                    settlement_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    settlement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_odometer = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    total_kilometers = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    driver_advance_cash_snapshot = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    driver_advance_fuel_snapshot = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    collected_to_pay_freight = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    total_driver_accountability = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    total_expenses_snapshot = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    net_driver_balance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    settled_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    payment_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "CASH"),
                    payment_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    settlement_remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_reversed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reversed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    reversed_by = table.Column<int>(type: "integer", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    settled_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("trip_settlements_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_trip_settlements_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_trip_settlements_trip",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trip_settlements_trip_id",
                table: "trip_settlements",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "trip_settlements_settlement_date_idx",
                table: "trip_settlements",
                columns: new[] { "tenant_id", "settlement_date" });

            migrationBuilder.CreateIndex(
                name: "trip_settlements_tenant_settlement_no_key",
                table: "trip_settlements",
                columns: new[] { "tenant_id", "settlement_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "trip_settlements_tenant_trip_idx",
                table: "trip_settlements",
                columns: new[] { "tenant_id", "trip_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trip_settlements");
        }
    }
}
