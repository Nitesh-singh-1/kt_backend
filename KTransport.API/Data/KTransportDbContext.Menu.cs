using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Data;

/// <summary>
/// TASK-041 Phase 1: DbSets + Fluent API for the menu_items catalog and the
/// menu_parity_log shadow log. Both tables are GLOBAL — no HasQueryFilter,
/// no tenant discriminator. Access control sits on the admin controller
/// with [RequirePlatformAdmin].
///
/// Invoked from the main context's OnModelCreating via
/// <see cref="ConfigureMenuModel"/> (we cannot reuse the existing
/// <c>OnModelCreatingPartial</c> partial method — that slot is already
/// claimed by KTransportDbContext.Entitlements.cs).
/// </summary>
public partial class KTransportDbContext
{
    public virtual DbSet<MenuItem> MenuItems { get; set; } = null!;
    public virtual DbSet<MenuParityLog> MenuParityLogs { get; set; } = null!;

    private static void ConfigureMenuModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("menu_items_pkey");
            entity.ToTable("menu_items");

            entity.HasIndex(e => e.Key, "menu_items_key_key").IsUnique();
            entity.HasIndex(e => new { e.ParentKey, e.DisplayOrder }, "menu_items_parent_key_idx");
            entity.HasIndex(e => e.IsActive, "menu_items_active_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Key).HasMaxLength(80).HasColumnName("key");
            entity.Property(e => e.ParentKey).HasMaxLength(80).HasColumnName("parent_key");
            entity.Property(e => e.Title).HasMaxLength(100).HasColumnName("title");
            entity.Property(e => e.Path).HasMaxLength(200).HasColumnName("path");
            entity.Property(e => e.Icon).HasMaxLength(40).HasColumnName("icon");
            entity.Property(e => e.PermissionKey).HasMaxLength(100).HasColumnName("permission_key");
            entity.Property(e => e.Badge).HasMaxLength(40).HasColumnName("badge");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0).HasColumnName("display_order");
            entity.Property(e => e.VisibilityRule).HasMaxLength(60).HasColumnName("visibility_rule");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");

            // TASK-049 Option B: structural FK to modules.id. The legacy
            // string columns (key / parent_key / permission_key) are kept
            // through Phase A per the migration policy — Phase B drops them.
            entity.Property(e => e.ModuleId).HasColumnName("module_id");
            entity.HasIndex(e => e.ModuleId, "menu_items_module_id_idx");
            entity.HasOne(e => e.Module).WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_menu_items_module");

            // Self-FK on parent_key → key. ON DELETE CASCADE so dropping a
            // module cascades to its children.
            entity.HasOne<MenuItem>()
                .WithMany()
                .HasForeignKey(e => e.ParentKey)
                .HasPrincipalKey(e => e.Key)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_menu_items_parent");

            // NO HasQueryFilter — menu catalog is product-wide (global).
        });

        modelBuilder.Entity<MenuParityLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("menu_parity_log_pkey");
            entity.ToTable("menu_parity_log");

            entity.HasIndex(e => e.LoggedAt, "menu_parity_log_logged_at_idx");
            entity.HasIndex(e => new { e.TenantId, e.LoggedAt }, "menu_parity_log_tenant_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.UserRole).HasMaxLength(50).HasColumnName("user_role");
            entity.Property(e => e.CodeOnlyKeys)
                .HasColumnType("text[]")
                .HasColumnName("code_only_keys");
            entity.Property(e => e.TablesOnlyKeys)
                .HasColumnType("text[]")
                .HasColumnName("tables_only_keys");
            entity.Property(e => e.LoggedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("logged_at");

            // NO HasQueryFilter — read-protected only via [RequirePlatformAdmin].
        });
    }
}
