using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdminUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "created_at", "full_name", "is_active", "mobile", "password", "role", "username" },
                values: new object[] { 1, new DateTime(2026, 4, 3, 15, 56, 50, 696, DateTimeKind.Unspecified).AddTicks(5250), "Kundan Kumar", true, "9504600060", "admin123", "admin", "admin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: 1);
        }
    }
}
