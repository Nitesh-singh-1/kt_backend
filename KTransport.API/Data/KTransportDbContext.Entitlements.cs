using System;
using KTransport.API.Common;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Data;

/// <summary>
/// RBAC clean-slate (ADR rbac-menu-consolidation-clean-slate): DbSets + Fluent
/// API for the permission catalog, report entitlements, role_permissions and
/// user_permission_overrides — all now keyed on structural FKs (permission_id /
/// role_id), with the legacy string columns dropped. The
/// tenant_entitlement_subscriptions table is retired entirely (tenant_modules +
/// tenant_module_pages are the sole ceiling).
/// </summary>
public partial class KTransportDbContext
{
    public virtual DbSet<Permission> Permissions { get; set; } = null!;
    public virtual DbSet<TenantReportEntitlement> TenantReportEntitlements { get; set; } = null!;
    public virtual DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public virtual DbSet<UserPermissionOverride> UserPermissionOverrides { get; set; } = null!;
    public virtual DbSet<PermissionParityLog> PermissionParityLogs { get; set; } = null!;

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("permissions_pkey");
            entity.ToTable("permissions");

            entity.HasIndex(e => e.Key, "permissions_key_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Key).HasMaxLength(100).HasColumnName("key");
            entity.Property(e => e.Action).HasMaxLength(20).HasColumnName("action");
            entity.Property(e => e.Description).HasMaxLength(200).HasColumnName("description");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0).HasColumnName("display_order");

            // Structural FK to modules.id (NOT NULL).
            entity.Property(e => e.ModuleId).HasColumnName("module_id");
            entity.HasIndex(e => e.ModuleId, "permissions_module_id_idx");
            entity.HasOne(e => e.Module).WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_permissions_module");

            // Optional page scope → menu_items.key.
            entity.Property(e => e.PageKey).HasMaxLength(100).HasColumnName("page_key");
            entity.HasIndex(e => e.PageKey, "permissions_page_key_idx");
        });

        modelBuilder.Entity<TenantReportEntitlement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tenant_report_entitlements_pkey");
            entity.ToTable("tenant_report_entitlements");

            entity.HasIndex(e => new { e.TenantId, e.ReportKey }, "tenant_report_entitlements_tenant_report_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ReportKey).HasMaxLength(60).HasColumnName("report_key");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true).HasColumnName("is_enabled");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_tenant_report_entitlements_tenant");
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("role_permissions_pkey");
            entity.ToTable("role_permissions");

            // Active unique: one grant per (tenant, role, permission).
            entity.HasIndex(e => new { e.TenantId, e.RoleId, e.PermissionId }, "role_permissions_tenant_role_perm_key")
                .IsUnique()
                .HasFilter("\"revoked_at\" IS NULL");
            entity.HasIndex(e => new { e.TenantId, e.RoleId, e.RevokedAt }, "role_permissions_tenant_role_active_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
            entity.Property(e => e.GrantedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("granted_at");
            entity.Property(e => e.GrantedBy).HasColumnName("granted_by");
            entity.Property(e => e.RevokedAt).HasColumnType("timestamp without time zone").HasColumnName("revoked_at");
            entity.Property(e => e.RevokedBy).HasColumnName("revoked_by");
            entity.Property(e => e.RevokeReason).HasMaxLength(500).HasColumnName("revoke_reason");
            entity.Property(e => e.SupersededBy).HasColumnName("superseded_by");

            entity.HasIndex(e => e.PermissionId, "role_permissions_permission_id_idx");
            entity.HasIndex(e => e.RoleId, "role_permissions_role_id_idx");

            entity.HasOne(e => e.Permission).WithMany()
                .HasForeignKey(e => e.PermissionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_role_permissions_permission");
            entity.HasOne(e => e.Role).WithMany()
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_role_permissions_role");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_role_permissions_tenant");
        });

        modelBuilder.Entity<UserPermissionOverride>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_permission_overrides_pkey");
            entity.ToTable("user_permission_overrides");

            entity.HasIndex(e => new { e.TenantId, e.UserId }, "user_perm_overrides_tenant_user_idx");

            // Current unique: one override per (tenant, user, permission).
            entity.HasIndex(e => new { e.TenantId, e.UserId, e.PermissionId }, "user_perm_overrides_tenant_user_perm_key")
                .IsUnique()
                .HasFilter("\"superseded_by\" IS NULL");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
            entity.Property(e => e.IsGranted).HasColumnName("is_granted");
            entity.Property(e => e.GrantedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("granted_at");
            entity.Property(e => e.GrantedBy).HasColumnName("granted_by");
            entity.Property(e => e.SupersededBy).HasColumnName("superseded_by");
            entity.Property(e => e.RevokeReason).HasMaxLength(500).HasColumnName("revoke_reason");

            entity.HasIndex(e => e.PermissionId, "user_perm_overrides_permission_id_idx");

            entity.HasOne(e => e.Permission).WithMany()
                .HasForeignKey(e => e.PermissionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_user_perm_overrides_permission");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_user_perm_overrides_tenant");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_user_perm_overrides_user");
        });

        modelBuilder.Entity<PermissionParityLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("permission_parity_log_pkey");
            entity.ToTable("permission_parity_log");

            entity.HasIndex(e => new { e.TenantId, e.LoggedAt }, "permission_parity_log_tenant_logged_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Role).HasMaxLength(50).HasColumnName("role");
            entity.Property(e => e.JsonOnlyKeys).HasColumnName("json_only_keys");
            entity.Property(e => e.TablesOnlyKeys).HasColumnName("tables_only_keys");
            entity.Property(e => e.LoggedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("logged_at");
        });
    }
}
