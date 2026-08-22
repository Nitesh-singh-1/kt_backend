using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("BillType_pkey", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    mobile = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "challan",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChallanNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ChallanDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LorryNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DriverName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VoiceDriverName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FromLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ToLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("challan_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_challan_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_challan_users_ModifiedBy",
                        column: x => x.ModifiedBy,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "gst_bills",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    gr_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    invoice_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    from_location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    to_location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gr_date = table.Column<DateOnly>(type: "date", nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    goods_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    gst_paid_by = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    consigner_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    consigner_gst_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    consigner_mobile = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    consignee_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    consignee_gst_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    consignee_mobile = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    consignee_address = table.Column<string>(type: "text", nullable: true),
                    truck_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    delivery_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    paid = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    tbb = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    to_pay = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    booking_clerk = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    consigneeraddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("gst_bills_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "without_gst_bills",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    gr_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    invoice_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    from_location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    to_location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gr_date = table.Column<DateOnly>(type: "date", nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    goods_value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    consigner_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    consigner_mobile = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    consignee_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    consignee_mobile = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    consignee_address = table.Column<string>(type: "text", nullable: true),
                    truck_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    delivery_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    paid = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    tbb = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    to_pay = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    booking_clerk = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("without_gst_bills_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_wgst_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_wgst_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "challandetail",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChallanId = table.Column<long>(type: "bigint", nullable: false),
                    BillNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: true),
                    Destination = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FreightAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BillTypeId = table.Column<int>(type: "integer", nullable: true),
                    ConsigneeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("challanDetail_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallanDetail_BillType",
                        column: x => x.BillTypeId,
                        principalTable: "BillType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChallanDetail_Challan",
                        column: x => x.ChallanId,
                        principalTable: "challan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "charges",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    bill_id = table.Column<int>(type: "integer", nullable: false),
                    freight = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0"),
                    service_charge = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0"),
                    dd_charge = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0"),
                    hamali = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0"),
                    other_charge = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0"),
                    st_charge = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0"),
                    grand_total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("charges_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_charges_bill",
                        column: x => x.bill_id,
                        principalTable: "gst_bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goods_details",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    bill_id = table.Column<int>(type: "integer", nullable: false),
                    article = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    weight = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    rate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("goods_details_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_goods_bill",
                        column: x => x.bill_id,
                        principalTable: "gst_bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_challan_CreatedBy",
                table: "challan",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_challan_ModifiedBy",
                table: "challan",
                column: "ModifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_challandetail_BillTypeId",
                table: "challandetail",
                column: "BillTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_challandetail_ChallanId",
                table: "challandetail",
                column: "ChallanId");

            migrationBuilder.CreateIndex(
                name: "charges_bill_id_key",
                table: "charges",
                column: "bill_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_details_bill_id",
                table: "goods_details",
                column: "bill_id");

            migrationBuilder.CreateIndex(
                name: "gst_bills_gr_no_key",
                table: "gst_bills",
                column: "gr_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gst_bills_created_by",
                table: "gst_bills",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_gst_bills_updated_by",
                table: "gst_bills",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "users_username_key",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_without_gst_bills_created_by",
                table: "without_gst_bills",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_without_gst_bills_updated_by",
                table: "without_gst_bills",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "without_gst_bills_gr_no_key",
                table: "without_gst_bills",
                column: "gr_no",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "challandetail");

            migrationBuilder.DropTable(
                name: "charges");

            migrationBuilder.DropTable(
                name: "goods_details");

            migrationBuilder.DropTable(
                name: "without_gst_bills");

            migrationBuilder.DropTable(
                name: "BillType");

            migrationBuilder.DropTable(
                name: "challan");

            migrationBuilder.DropTable(
                name: "gst_bills");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
