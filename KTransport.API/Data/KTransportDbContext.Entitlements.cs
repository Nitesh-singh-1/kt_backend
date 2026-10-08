using System;
using KTransport.API.Common;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Data;

/// <summary>
/// TASK-040 Phase 1: DbSets and Fluent API for the five new entitlement
/// shadow tables + the permission_parity_log table. Hooked into the main
/// context via the <c>OnModelCreatingPartial</c> partial method at the
/// bottom of KTransportDbContext.cs.
/// </summary>
public partial class KTransportDbContext
{
    public virtual DbSet<Permission> Permissions { get; set; } = null!;
    public virtual DbSet<TenantEntitlementSubscription> TenantEntitlementSubscriptions { get; set; } = null!;
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
            entity.HasIndex(e => new { e.FeatureKey, e.Action }, "permissions_feature_action_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Key).HasMaxLength(100).HasColumnName("key");
            entity.Property(e => e.FeatureKey).HasMaxLength(80).HasColumnName("feature_key");
            entity.Property(e => e.Action).HasMaxLength(20).HasColumnName("action");
            entity.Property(e => e.Description).HasMaxLength(200).HasColumnName("description");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0).HasColumnName("display_order");
        });

        modelBuilder.Entity<TenantEntitlementSubscription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tenant_entitlement_subscriptions_pkey");
            entity.ToTable("tenant_entitlement_subscriptions");

            entity.HasIndex(e => new { e.TenantId, e.EffectiveUntil }, "tenant_ent_subs_tenant_active_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.PlanTier).HasMaxLength(30).HasColumnName("plan_tier");
            entity.Property(e => e.EnabledFeatureKeys)
                .HasColumnType("text[]")
                .HasColumnName("enabled_feature_keys");
            entity.Property(e => e.EffectiveFrom)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("effective_from");
            entity.Property(e => e.EffectiveUntil)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("effective_until");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_tenant_ent_subs_tenant");
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

            entity.HasIndex(e => new { e.TenantId, e.RoleName, e.RevokedAt }, "role_permissions_tenant_role_active_idx");

            // Partial unique: only one ACTIVE grant per (tenant, role, perm_key).
            entity.HasIndex(e => new { e.TenantId, e.RoleName, e.PermissionKey }, "role_permissions_tenant_role_perm_key")
                .IsUnique()
                .HasFilter("\"revoked_at\" IS NULL");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.RoleName).HasMaxLength(50).HasColumnName("role_name");
            entity.Property(e => e.PermissionKey).HasMaxLength(100).HasColumnName("permission_key");
            entity.Property(e => e.GrantedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("granted_at");
            entity.Property(e => e.GrantedBy).HasColumnName("granted_by");
            entity.Property(e => e.RevokedAt).HasColumnType("timestamp without time zone").HasColumnName("revoked_at");
            entity.Property(e => e.RevokedBy).HasColumnName("revoked_by");
            entity.Property(e => e.RevokeReason).HasMaxLength(500).HasColumnName("revoke_reason");
            entity.Property(e => e.SupersededBy).HasColumnName("superseded_by");

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

            // Partial unique: only one CURRENT override per (tenant, user, perm_key).
            entity.HasIndex(e => new { e.TenantId, e.UserId, e.PermissionKey }, "user_perm_overrides_tenant_user_perm_key")
                .IsUnique()
                .HasFilter("\"superseded_by\" IS NULL");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.PermissionKey).HasMaxLength(100).HasColumnName("permission_key");
            entity.Property(e => e.IsGranted).HasColumnName("is_granted");
            entity.Property(e => e.GrantedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("granted_at");
            entity.Property(e => e.GrantedBy).HasColumnName("granted_by");
            entity.Property(e => e.SupersededBy).HasColumnName("superseded_by");
            entity.Property(e => e.RevokeReason).HasMaxLength(500).HasColumnName("revoke_reason");

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
