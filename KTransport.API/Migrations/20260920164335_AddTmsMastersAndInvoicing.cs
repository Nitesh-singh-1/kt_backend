using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTmsMastersAndInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "consignee_party_id",
                table: "shipments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "consignor_party_id",
                table: "shipments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "invoice_id",
                table: "shipments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "drivers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    license_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    license_valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    aadhar_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("drivers_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_drivers_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    pincode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("locations_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_locations_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parties",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    gst_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    pan_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pincode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    party_type = table.Column<int>(type: "integer", nullable: false),
                    default_payment_term = table.Column<int>(type: "integer", nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("parties_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_parties_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    vehicle_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    vehicle_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    owner_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    capacity_tons = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    engine_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    chassis_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fitness_valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    insurance_valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    permit_valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("vehicles_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_vehicles_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    invoice_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    party_id = table.Column<long>(type: "bigint", nullable: true),
                    party_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    party_gst_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    party_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sub_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    tax_rate = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    tax_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    discount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    other_charges = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    grand_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    paid_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    due_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    payment_status = table.Column<int>(type: "integer", nullable: false),
                    payment_mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("invoices_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_invoices_party",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_invoices_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    shipment_id = table.Column<long>(type: "bigint", nullable: true),
                    shipment_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 1m),
                    rate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    tax_rate = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    tax_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    total_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("invoice_items_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_invoice_items_invoice",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_invoice_items_shipment",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipments_consignee_party_id",
                table: "shipments",
                column: "consignee_party_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_consignor_party_id",
                table: "shipments",
                column: "consignor_party_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_invoice_id",
                table: "shipments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "drivers_tenant_mobile_idx",
                table: "drivers",
                columns: new[] { "tenant_id", "mobile" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_invoice_id",
                table: "invoice_items",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_shipment_id",
                table: "invoice_items",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "invoices_tenant_invoice_no_key",
                table: "invoices",
                columns: new[] { "tenant_id", "invoice_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_party_id",
                table: "invoices",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "locations_tenant_code_key",
                table: "locations",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "parties_tenant_name_idx",
                table: "parties",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "vehicles_tenant_vehicle_no_key",
                table: "vehicles",
                columns: new[] { "tenant_id", "vehicle_no" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_shipments_consignee_party",
                table: "shipments",
                column: "consignee_party_id",
                principalTable: "parties",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_shipments_consignor_party",
                table: "shipments",
                column: "consignor_party_id",
                principalTable: "parties",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_shipments_invoice",
                table: "shipments",
                column: "invoice_id",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_shipments_consignee_party",
                table: "shipments");

            migrationBuilder.DropForeignKey(
                name: "fk_shipments_consignor_party",
                table: "shipments");

            migrationBuilder.DropForeignKey(
                name: "fk_shipments_invoice",
                table: "shipments");

            migrationBuilder.DropTable(
                name: "drivers");

            migrationBuilder.DropTable(
                name: "invoice_items");

            migrationBuilder.DropTable(
                name: "locations");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "parties");

            migrationBuilder.DropIndex(
                name: "IX_shipments_consignee_party_id",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "IX_shipments_consignor_party_id",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "IX_shipments_invoice_id",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "consignee_party_id",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "consignor_party_id",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "invoice_id",
                table: "shipments");
        }
    }
}
