using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseTmsFullPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cargo_claims",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    claim_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    shipment_id = table.Column<long>(type: "bigint", nullable: false),
                    shipment_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    party_id = table.Column<long>(type: "bigint", nullable: true),
                    party_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    claim_type = table.Column<int>(type: "integer", nullable: false),
                    claim_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    settled_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    status = table.Column<int>(type: "integer", nullable: false),
                    claim_date = table.Column<DateOnly>(type: "date", nullable: false),
                    settled_date = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    investigation_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    settlement_remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cargo_claims_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_claims_party",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_claims_shipment",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_claims_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "freight_rate_cards",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    party_id = table.Column<long>(type: "bigint", nullable: true),
                    party_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    from_location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    to_location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    commodity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    rate_type = table.Column<int>(type: "integer", nullable: false),
                    base_rate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    min_freight_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    hamali_rate_per_kg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    dd_charge = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    st_charge = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("freight_rate_cards_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_rate_cards_party",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_rate_cards_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pod_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    shipment_id = table.Column<long>(type: "bigint", nullable: false),
                    shipment_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    delivery_date = table.Column<DateOnly>(type: "date", nullable: false),
                    receiver_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    receiver_mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    receiver_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    document_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    signature_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verified_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pod_records_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_pod_records_shipment",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pod_records_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trips",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    trip_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    trip_date = table.Column<DateOnly>(type: "date", nullable: false),
                    vehicle_id = table.Column<long>(type: "bigint", nullable: true),
                    vehicle_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    driver_id = table.Column<long>(type: "bigint", nullable: true),
                    driver_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    driver_mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    origin_location_id = table.Column<long>(type: "bigint", nullable: true),
                    origin_location_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    destination_location_id = table.Column<long>(type: "bigint", nullable: true),
                    destination_location_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    departure_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    arrival_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    start_odometer = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    end_odometer = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    seal_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    total_weight_tons = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false, defaultValue: 0m),
                    total_packages = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_freight_revenue = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    driver_advance_cash = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    driver_advance_fuel = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    total_expenses = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("trips_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_trips_dest",
                        column: x => x.destination_location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_trips_driver",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_trips_origin",
                        column: x => x.origin_location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_trips_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_trips_vehicle",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_maintenances",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    vehicle_id = table.Column<long>(type: "bigint", nullable: false),
                    vehicle_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    maintenance_type = table.Column<int>(type: "integer", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    odometer_reading = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    next_service_due_km = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    next_service_due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    workshop_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    invoice_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("vehicle_maintenances_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_maintenance_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_maintenance_vehicle",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vendors",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    pan_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    gst_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    contact_person = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tds_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 1.0m),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ifsc_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    account_holder_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("vendors_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_vendors_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_expenses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    trip_id = table.Column<long>(type: "bigint", nullable: false),
                    expense_type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    receipt_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    payment_mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    paid_to = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    remarks = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    expense_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("trip_expenses_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_trip_expenses_trip",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_shipments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    trip_id = table.Column<long>(type: "bigint", nullable: false),
                    shipment_id = table.Column<long>(type: "bigint", nullable: false),
                    shipment_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    loaded_weight = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false, defaultValue: 0m),
                    loaded_packages = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    freight_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    loaded_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    unloaded_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    remarks = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("trip_shipments_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_trip_shipments_shipment",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_trip_shipments_trip",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lorry_hire_contracts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    contract_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contract_date = table.Column<DateOnly>(type: "date", nullable: false),
                    trip_id = table.Column<long>(type: "bigint", nullable: true),
                    vendor_id = table.Column<long>(type: "bigint", nullable: true),
                    vendor_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    vehicle_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    driver_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    driver_mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    from_location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    to_location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    total_hire_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    advance_cash_paid = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    diesel_advance_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    tds_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    other_deductions = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    balance_payable = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    paid_balance_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    payment_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValue: "Unpaid"),
                    payment_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("lorry_hire_contracts_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_lorry_hire_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lorry_hire_trip",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lorry_hire_vendor",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "claims_tenant_claim_no_key",
                table: "cargo_claims",
                columns: new[] { "tenant_id", "claim_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cargo_claims_party_id",
                table: "cargo_claims",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_claims_shipment_id",
                table: "cargo_claims",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_freight_rate_cards_party_id",
                table: "freight_rate_cards",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "rate_cards_tenant_route_idx",
                table: "freight_rate_cards",
                columns: new[] { "tenant_id", "from_location", "to_location", "party_id" });

            migrationBuilder.CreateIndex(
                name: "IX_lorry_hire_contracts_trip_id",
                table: "lorry_hire_contracts",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_lorry_hire_contracts_vendor_id",
                table: "lorry_hire_contracts",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "lorry_hire_tenant_contract_no_key",
                table: "lorry_hire_contracts",
                columns: new[] { "tenant_id", "contract_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pod_records_shipment_id",
                table: "pod_records",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "pod_records_tenant_shipment_idx",
                table: "pod_records",
                columns: new[] { "tenant_id", "shipment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_trip_expenses_trip_id",
                table: "trip_expenses",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_trip_shipments_shipment_id",
                table: "trip_shipments",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_trip_shipments_trip_id",
                table: "trip_shipments",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "trip_shipments_tenant_trip_shipment_idx",
                table: "trip_shipments",
                columns: new[] { "tenant_id", "trip_id", "shipment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_trips_destination_location_id",
                table: "trips",
                column: "destination_location_id");

            migrationBuilder.CreateIndex(
                name: "IX_trips_driver_id",
                table: "trips",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_trips_origin_location_id",
                table: "trips",
                column: "origin_location_id");

            migrationBuilder.CreateIndex(
                name: "IX_trips_vehicle_id",
                table: "trips",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "trips_tenant_trip_no_key",
                table: "trips",
                columns: new[] { "tenant_id", "trip_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_maintenances_vehicle_id",
                table: "vehicle_maintenances",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "maintenance_tenant_vehicle_idx",
                table: "vehicle_maintenances",
                columns: new[] { "tenant_id", "vehicle_id" });

            migrationBuilder.CreateIndex(
                name: "vendors_tenant_name_idx",
                table: "vendors",
                columns: new[] { "tenant_id", "name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cargo_claims");

            migrationBuilder.DropTable(
                name: "freight_rate_cards");

            migrationBuilder.DropTable(
                name: "lorry_hire_contracts");

            migrationBuilder.DropTable(
                name: "pod_records");

            migrationBuilder.DropTable(
                name: "trip_expenses");

            migrationBuilder.DropTable(
                name: "trip_shipments");

            migrationBuilder.DropTable(
                name: "vehicle_maintenances");

            migrationBuilder.DropTable(
                name: "vendors");

            migrationBuilder.DropTable(
                name: "trips");
        }
    }
}
