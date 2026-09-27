using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleInsuranceClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicle_insurance_claims",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    vehicle_id = table.Column<long>(type: "bigint", nullable: true),
                    vehicle_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    claim_no = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    insurer_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    policy_no = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    claim_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    incident_date = table.Column<DateOnly>(type: "date", nullable: true),
                    claim_date = table.Column<DateOnly>(type: "date", nullable: false),
                    claim_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    approved_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    status = table.Column<int>(type: "integer", nullable: false),
                    surveyor_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("vehicle_insurance_claims_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_vehicle_insurance_claims_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vehicle_insurance_claims_vehicle",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_insurance_claims_vehicle_id",
                table: "vehicle_insurance_claims",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "vehicle_insurance_claims_tenant_status_idx",
                table: "vehicle_insurance_claims",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_insurance_claims");
        }
    }
}
