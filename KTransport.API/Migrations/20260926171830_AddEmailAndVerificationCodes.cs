using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailAndVerificationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "users",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "verification_codes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("verification_codes_pkey", x => x.id);
                });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: 1,
                column: "email",
                value: null);

            migrationBuilder.CreateIndex(
                name: "verification_codes_username_purpose_idx",
                table: "verification_codes",
                columns: new[] { "username", "purpose", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "verification_codes");

            migrationBuilder.DropColumn(
                name: "email",
                table: "users");
        }
    }
}
