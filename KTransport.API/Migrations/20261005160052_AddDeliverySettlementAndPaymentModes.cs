using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliverySettlementAndPaymentModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "delivered_to",
                table: "shipments",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "delivery_date",
                table: "shipments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discount_reason",
                table: "shipments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discount_remarks",
                table: "shipments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_partial_payment",
                table: "shipments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_settled",
                table: "shipments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "settled_discount_amount",
                table: "shipments",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "settled_payment_mode",
                table: "shipments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "settled_received_amount",
                table: "shipments",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "settlement_reference_no",
                table: "shipments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payment_modes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("payment_modes_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_modes_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "payment_modes_tenant_code_key",
                table: "payment_modes",
                columns: new[] { "tenant_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_modes");

            migrationBuilder.DropColumn(
                name: "delivered_to",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "delivery_date",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "discount_reason",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "discount_remarks",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "is_partial_payment",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "is_settled",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "settled_discount_amount",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "settled_payment_mode",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "settled_received_amount",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "settlement_reference_no",
                table: "shipments");
        }
    }
}
