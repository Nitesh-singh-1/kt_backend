using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quotations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("11111111-1111-1111-1111-111111111111")),
                    quote_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    quote_date = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    party_id = table.Column<long>(type: "bigint", nullable: true),
                    party_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    party_mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    party_gst_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    from_location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    to_location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    vehicle_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    goods_description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    weight_kg = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    rate_per_unit = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    rate_basis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    estimated_freight = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    status = table.Column<int>(type: "integer", nullable: false),
                    converted_ref = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    terms = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("quotations_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_quotations_party",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_quotations_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quotations_party_id",
                table: "quotations",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "quotations_tenant_quoteno_idx",
                table: "quotations",
                columns: new[] { "tenant_id", "quote_no" });

            migrationBuilder.CreateIndex(
                name: "quotations_tenant_status_idx",
                table: "quotations",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quotations");
        }
    }
}
