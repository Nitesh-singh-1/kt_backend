using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentPathColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document_path",
                table: "tenant_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_path",
                table: "pod_records",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_path",
                table: "consignment_invoice_references",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_path",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "document_path",
                table: "pod_records");

            migrationBuilder.DropColumn(
                name: "document_path",
                table: "consignment_invoice_references");
        }
    }
}
