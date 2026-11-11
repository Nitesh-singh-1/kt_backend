using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGstEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "home_location_id",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "booking_location_id",
                table: "shipments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ewb_transaction_type",
                table: "shipments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "gst_calculation_id",
                table: "shipments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ship_to_gstin",
                table: "shipments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ship_to_state_code",
                table: "shipments",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "gst_registration_id",
                table: "locations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "eway_bill_cache",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ewb_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ewb_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    valid_upto = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    sync_status = table.Column<int>(type: "integer", nullable: false),
                    supply_type = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    sub_supply_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    doc_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    doc_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    doc_date = table.Column<DateOnly>(type: "date", nullable: true),
                    from_gstin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    from_state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    to_gstin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ship_to_gstin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ship_to_state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    transaction_type = table.Column<int>(type: "integer", nullable: true),
                    total_invoice_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    trans_distance = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    vehicle_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    transporter_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    transporter_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    shipment_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("eway_bill_cache_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gst_registrations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_principal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("gst_registrations_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gst_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type = table.Column<int>(type: "integer", nullable: false),
                    provider_is_gta = table.Column<bool>(type: "boolean", nullable: false),
                    issues_consignment_note = table.Column<bool>(type: "boolean", nullable: false),
                    provider_gst_registered = table.Column<bool>(type: "boolean", nullable: false),
                    gta_tax_scheme = table.Column<int>(type: "integer", nullable: false),
                    recipient_category = table.Column<int>(type: "integer", nullable: false),
                    pos_relation = table.Column<int>(type: "integer", nullable: false),
                    tax_treatment = table.Column<int>(type: "integer", nullable: false),
                    gst_rate = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    cgst_rate = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    sgst_rate = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    igst_rate = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    utgst_rate = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    itc_eligible_for_provider = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    rcm_payable_by = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    notification_reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    exceptions_note = table.Column<string>(type: "text", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("gst_rules_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "party_gst_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<int>(type: "integer", nullable: false),
                    gstin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    gst_registered = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    recipient_category = table.Column<int>(type: "integer", nullable: false),
                    place_of_supply_default = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("party_gst_profiles_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gst_registration_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gst_registration_id = table.Column<long>(type: "bigint", nullable: false),
                    gstin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    gst_registered = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_gta = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    issues_consignment_note = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    gta_tax_scheme = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("gst_registration_profiles_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_gst_registration_profiles_registration",
                        column: x => x.gst_registration_id,
                        principalTable: "gst_registrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tax_calculations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bilty_id = table.Column<int>(type: "integer", nullable: true),
                    shipment_id = table.Column<long>(type: "bigint", nullable: true),
                    invoice_id = table.Column<int>(type: "integer", nullable: true),
                    input_json = table.Column<string>(type: "jsonb", nullable: false),
                    rule_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tax_treatment = table.Column<int>(type: "integer", nullable: false),
                    gst_rate = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    cgst_rate = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    cgst_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    sgst_rate = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    sgst_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    igst_rate = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    igst_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    utgst_rate = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    utgst_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    taxable_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    total_tax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    grand_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    is_rcm = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_itc_eligible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    place_of_supply = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pos_relation = table.Column<int>(type: "integer", nullable: true),
                    calculated_by = table.Column<int>(type: "integer", nullable: true),
                    calculated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    notification_reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    superseded_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_calculations_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_tax_calculations_rule",
                        column: x => x.rule_version_id,
                        principalTable: "gst_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: 1,
                column: "home_location_id",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_users_home_location_id",
                table: "users",
                column: "home_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipments_booking_location",
                table: "shipments",
                column: "booking_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipments_gst_calculation",
                table: "shipments",
                column: "gst_calculation_id");

            migrationBuilder.CreateIndex(
                name: "ix_locations_gst_registration",
                table: "locations",
                column: "gst_registration_id");

            migrationBuilder.CreateIndex(
                name: "ix_eway_bill_cache_shipment",
                table: "eway_bill_cache",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_eway_bill_cache_valid_upto",
                table: "eway_bill_cache",
                column: "valid_upto");

            migrationBuilder.CreateIndex(
                name: "ux_eway_bill_cache_tenant_ewb",
                table: "eway_bill_cache",
                columns: new[] { "tenant_id", "ewb_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gst_registration_profiles_gst_registration_id",
                table: "gst_registration_profiles",
                column: "gst_registration_id");

            migrationBuilder.CreateIndex(
                name: "ix_gst_registration_profiles_tenant_reg_effective",
                table: "gst_registration_profiles",
                columns: new[] { "tenant_id", "gst_registration_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "ix_gst_registrations_tenant_active",
                table: "gst_registrations",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ux_gst_registrations_tenant_principal",
                table: "gst_registrations",
                column: "tenant_id",
                unique: true,
                filter: "is_principal = true");

            migrationBuilder.CreateIndex(
                name: "ix_gst_rules_active_window",
                table: "gst_rules",
                columns: new[] { "active", "effective_from", "effective_to" });

            migrationBuilder.CreateIndex(
                name: "ix_gst_rules_tuple_effective",
                table: "gst_rules",
                columns: new[] { "service_type", "provider_is_gta", "issues_consignment_note", "provider_gst_registered", "gta_tax_scheme", "recipient_category", "pos_relation", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_party_gst_profiles_tenant_party_effective",
                table: "party_gst_profiles",
                columns: new[] { "tenant_id", "party_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "ix_tax_calculations_bilty",
                table: "tax_calculations",
                column: "bilty_id");

            migrationBuilder.CreateIndex(
                name: "ix_tax_calculations_calculated_at",
                table: "tax_calculations",
                column: "calculated_at");

            migrationBuilder.CreateIndex(
                name: "IX_tax_calculations_rule_version_id",
                table: "tax_calculations",
                column: "rule_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_tax_calculations_shipment",
                table: "tax_calculations",
                column: "shipment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_locations_gst_registration",
                table: "locations",
                column: "gst_registration_id",
                principalTable: "gst_registrations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_shipments_booking_location",
                table: "shipments",
                column: "booking_location_id",
                principalTable: "locations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_shipments_gst_calculation",
                table: "shipments",
                column: "gst_calculation_id",
                principalTable: "tax_calculations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_users_home_location",
                table: "users",
                column: "home_location_id",
                principalTable: "locations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_locations_gst_registration",
                table: "locations");

            migrationBuilder.DropForeignKey(
                name: "fk_shipments_booking_location",
                table: "shipments");

            migrationBuilder.DropForeignKey(
                name: "fk_shipments_gst_calculation",
                table: "shipments");

            migrationBuilder.DropForeignKey(
                name: "fk_users_home_location",
                table: "users");

            migrationBuilder.DropTable(
                name: "eway_bill_cache");

            migrationBuilder.DropTable(
                name: "gst_registration_profiles");

            migrationBuilder.DropTable(
                name: "party_gst_profiles");

            migrationBuilder.DropTable(
                name: "tax_calculations");

            migrationBuilder.DropTable(
                name: "gst_registrations");

            migrationBuilder.DropTable(
                name: "gst_rules");

            migrationBuilder.DropIndex(
                name: "IX_users_home_location_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_shipments_booking_location",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "ix_shipments_gst_calculation",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "ix_locations_gst_registration",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "home_location_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "booking_location_id",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "ewb_transaction_type",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "gst_calculation_id",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "ship_to_gstin",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "ship_to_state_code",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "gst_registration_id",
                table: "locations");
        }
    }
}
