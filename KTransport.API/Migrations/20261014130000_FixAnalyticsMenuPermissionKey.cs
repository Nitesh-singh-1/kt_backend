using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// Data fix: the "analytics" (Business Analytics) sidebar row was seeded with
    /// permission_key = 'dashboard.view' instead of 'analytics.view'. Menu
    /// visibility is decided by whether the user's effective permission set
    /// contains the row's permission_key, so every tenant that had the Dashboard
    /// module (which is auto-enabled for all tenants) saw Business Analytics even
    /// when they never subscribed to the analytics module. Corrected to
    /// 'analytics.view' so the row is gated by the analytics module like it should
    /// be. Idempotent.
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261014130000_FixAnalyticsMenuPermissionKey")]
    public partial class FixAnalyticsMenuPermissionKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE menu_items
   SET permission_key = 'analytics.view'
 WHERE key = 'analytics'
   AND permission_key = 'dashboard.view';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE menu_items
   SET permission_key = 'dashboard.view'
 WHERE key = 'analytics'
   AND permission_key = 'analytics.view';
");
        }
    }
}
