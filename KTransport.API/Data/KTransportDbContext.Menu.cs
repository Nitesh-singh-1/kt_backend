using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Data;

/// <summary>
/// DbSets + Fluent API for the menu_items catalog and the menu_parity_log
/// shadow log. Both GLOBAL — no tenant discriminator. Access control is on the
/// admin controller via [RequirePlatformAdmin].
///
/// RBAC clean-slate: the string self-ref parent_key is replaced by an int
/// self-FK parent_id; module_id is the structural visibility join.
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
            entity.HasIndex(e => new { e.ParentId, e.DisplayOrder }, "menu_items_parent_id_idx");
            entity.HasIndex(e => e.IsActive, "menu_items_active_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Key).HasMaxLength(80).HasColumnName("key");
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.Title).HasMaxLength(100).HasColumnName("title");
            entity.Property(e => e.Path).HasMaxLength(200).HasColumnName("path");
            entity.Property(e => e.Icon).HasMaxLength(40).HasColumnName("icon");
            entity.Property(e => e.PermissionKey).HasMaxLength(100).HasColumnName("permission_key");
            entity.Property(e => e.Badge).HasMaxLength(40).HasColumnName("badge");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0).HasColumnName("display_order");
            entity.Property(e => e.VisibilityRule).HasMaxLength(60).HasColumnName("visibility_rule");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");

            // Structural FK to modules.id (NOT NULL).
            entity.Property(e => e.ModuleId).HasColumnName("module_id");
            entity.HasIndex(e => e.ModuleId, "menu_items_module_id_idx");
            entity.HasOne(e => e.Module).WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_menu_items_module");

            // Self-FK parent_id → id. ON DELETE CASCADE so dropping a parent
            // cascades to its children.
            entity.HasOne(e => e.Parent)
                .WithMany()
                .HasForeignKey(e => e.ParentId)
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
        });
    }
}
