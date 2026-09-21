using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenancyAndGenericShipmentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "without_gst_bills",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "gst_bills",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "goods_details",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "charges",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "challandetail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "challan",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tenants_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "charge_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    default_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    is_taxable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("charge_types_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_charge_types_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "numbering_sequences",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_value = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    padding = table.Column<int>(type: "integer", nullable: false, defaultValue: 4),
                    year = table.Column<int>(type: "integer", nullable: false),
                    format_pattern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "{PREFIX}-{YEAR}-{SEQ}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("numbering_sequences_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_numbering_sequences_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    shipment_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invoice_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    shipment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    from_location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    to_location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    truck_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tax_treatment = table.Column<int>(type: "integer", nullable: false),
                    gst_paid_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    consignor_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    consignor_gst_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    consignor_mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    consignor_address = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    consignee_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    consignee_gst_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    consignee_mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    consignee_address = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    goods_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    payment_term = table.Column<int>(type: "integer", nullable: false),
                    total_freight = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    total_other_charges = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    total_tax_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    grand_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    paid_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    due_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    status = table.Column<int>(type: "integer", nullable: false),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    booking_clerk = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("shipments_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_shipments_created_by",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_shipments_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shipments_updated_by",
                        column: x => x.UpdatedBy,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "shipment_charge_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    shipment_id = table.Column<long>(type: "bigint", nullable: false),
                    charge_type_id = table.Column<int>(type: "integer", nullable: true),
                    charge_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    is_taxable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("shipment_charge_items_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipment_charge_items_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shipment_charge_items_shipment",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shipment_charge_items_type",
                        column: x => x.charge_type_id,
                        principalTable: "charge_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "shipment_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    shipment_id = table.Column<long>(type: "bigint", nullable: false),
                    article = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    weight = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false, defaultValue: 0m),
                    rate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("shipment_items_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipment_items_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shipment_items_shipment",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipment_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    shipment_id = table.Column<long>(type: "bigint", nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: false),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("shipment_status_history_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipment_status_history_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shipment_history_shipment",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shipment_history_user",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "tenants",
                columns: new[] { "id", "code", "created_at", "is_active", "name", "updated_at" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), "DEFAULT", new DateTime(2026, 4, 3, 15, 56, 50, 696, DateTimeKind.Unspecified).AddTicks(5250), true, "Default Organization", null });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "password", "tenant_id" },
                values: new object[] { "$2a$11$0aBw4j5tM1Ew2k5hF8O/TehI5jY9K2HkZ0K6o0tM6dYp/1R9Gq8m6", new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.InsertData(
                table: "charge_types",
                columns: new[] { "id", "code", "is_active", "is_taxable", "name", "tenant_id" },
                values: new object[] { 1, "FREIGHT", true, true, "Base Freight", new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.InsertData(
                table: "charge_types",
                columns: new[] { "id", "code", "is_active", "name", "tenant_id" },
                values: new object[] { 2, "HAMALI", true, "Loading / Hamali", new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.InsertData(
                table: "charge_types",
                columns: new[] { "id", "code", "is_active", "is_taxable", "name", "tenant_id" },
                values: new object[] { 3, "DOOR_DELIVERY", true, true, "Door Delivery (DD)", new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.InsertData(
                table: "charge_types",
                columns: new[] { "id", "code", "is_active", "name", "tenant_id" },
                values: new object[,]
                {
                    { 4, "STATION_CHARGE", true, "Stationary / ST Charge", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 5, "TOLL_SURCHARGE", true, "Toll & Surcharge", new Guid("11111111-1111-1111-1111-111111111111") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_without_gst_bills_tenant_id",
                table: "without_gst_bills",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id",
                table: "users",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_gst_bills_tenant_id",
                table: "gst_bills",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_challan_TenantId",
                table: "challan",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_charge_types_tenant_id",
                table: "charge_types",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "numbering_sequences_tenant_entity_year_key",
                table: "numbering_sequences",
                columns: new[] { "tenant_id", "entity_type", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_charge_items_charge_type_id",
                table: "shipment_charge_items",
                column: "charge_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_charge_items_shipment_id",
                table: "shipment_charge_items",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_charge_items_tenant_id",
                table: "shipment_charge_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_items_shipment_id",
                table: "shipment_items",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_items_tenant_id",
                table: "shipment_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_status_history_changed_by_user_id",
                table: "shipment_status_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_status_history_shipment_id",
                table: "shipment_status_history",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_status_history_tenant_id",
                table: "shipment_status_history",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_CreatedBy",
                table: "shipments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_UpdatedBy",
                table: "shipments",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "shipments_tenant_shipment_no_key",
                table: "shipments",
                columns: new[] { "tenant_id", "shipment_no" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_challan_tenant",
                table: "challan",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_gst_bills_tenant",
                table: "gst_bills",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_users_tenant",
                table: "users",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_wgst_tenant",
                table: "without_gst_bills",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_challan_tenant",
                table: "challan");

            migrationBuilder.DropForeignKey(
                name: "fk_gst_bills_tenant",
                table: "gst_bills");

            migrationBuilder.DropForeignKey(
                name: "fk_users_tenant",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "fk_wgst_tenant",
                table: "without_gst_bills");

            migrationBuilder.DropTable(
                name: "numbering_sequences");

            migrationBuilder.DropTable(
                name: "shipment_charge_items");

            migrationBuilder.DropTable(
                name: "shipment_items");

            migrationBuilder.DropTable(
                name: "shipment_status_history");

            migrationBuilder.DropTable(
                name: "charge_types");

            migrationBuilder.DropTable(
                name: "shipments");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_without_gst_bills_tenant_id",
                table: "without_gst_bills");

            migrationBuilder.DropIndex(
                name: "IX_users_tenant_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_gst_bills_tenant_id",
                table: "gst_bills");

            migrationBuilder.DropIndex(
                name: "IX_challan_TenantId",
                table: "challan");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "without_gst_bills");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "gst_bills");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "goods_details");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "challandetail");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "challan");

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: 1,
                column: "password",
                value: "admin123");
        }
    }
}
