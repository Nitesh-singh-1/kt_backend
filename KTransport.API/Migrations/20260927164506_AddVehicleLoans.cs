using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleLoans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicle_loans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    vehicle_id = table.Column<long>(type: "bigint", nullable: true),
                    vehicle_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    lender = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    loan_account_no = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    principal_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    emi_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    tenure_months = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    emis_paid = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    interest_rate = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    loan_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    next_due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("vehicle_loans_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_vehicle_loans_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vehicle_loans_vehicle",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_loans_vehicle_id",
                table: "vehicle_loans",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "vehicle_loans_tenant_vehicle_idx",
                table: "vehicle_loans",
                columns: new[] { "tenant_id", "vehicle_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_loans");
        }
    }
}
