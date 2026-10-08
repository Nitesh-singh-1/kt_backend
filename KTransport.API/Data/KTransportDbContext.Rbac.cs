using KTransport.API.Common;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Data;

/// <summary>
/// TASK-046 Phase 1: DbSets + Fluent API for the four new RBAC tables
/// (<c>modules</c>, <c>tenant_modules</c>, <c>roles</c>, <c>user_roles</c>)
/// plus the <c>role_permissions.role_id</c> FK that dual-maintains with the
/// legacy <c>role_name</c> string. Soft-delete discipline per ADR §Y applies
/// — no column drops, no row deletes.
///
/// Hooked into the main context via <see cref="ConfigureRbacModel"/>, invoked
/// from <c>OnModelCreating</c> at the bottom of KTransportDbContext.cs.
/// </summary>
public partial class KTransportDbContext
{
    public virtual DbSet<Module> Modules { get; set; } = null!;
    public virtual DbSet<TenantModule> TenantModules { get; set; } = null!;
    public virtual DbSet<Role> Roles { get; set; } = null!;
    public virtual DbSet<UserRole> UserRoles { get; set; } = null!;

    private void ConfigureRbacModel(ModelBuilder modelBuilder)
    {
        // -------- modules (global catalog, no HasQueryFilter) ----------------
        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("modules_pkey");
            entity.ToTable("modules");

            entity.HasIndex(e => e.Code, "modules_code_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasMaxLength(60).HasColumnName("code");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
            entity.Property(e => e.Description).HasMaxLength(500).HasColumnName("description");
            entity.Property(e => e.IsPremium).HasDefaultValue(false).HasColumnName("is_premium");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0).HasColumnName("display_order");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
        });

        // -------- tenant_modules (tenant-scoped, append-only) ---------------
        modelBuilder.Entity<TenantModule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tenant_modules_pkey");
            entity.ToTable("tenant_modules");

            entity.HasIndex(e => new { e.TenantId, e.ModuleId }, "tenant_modules_tenant_module_active_uq")
                .IsUnique()
                .HasFilter("\"enabled_until\" IS NULL");

            entity.HasIndex(e => new { e.TenantId, e.IsEnabled }, "tenant_modules_tenant_enabled_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ModuleId).HasColumnName("module_id");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true).HasColumnName("is_enabled");
            entity.Property(e => e.EnabledFrom)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("enabled_from");
            entity.Property(e => e.EnabledUntil)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("enabled_until");
            entity.Property(e => e.EnabledBy).HasColumnName("enabled_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_tenant_modules_tenant");

            entity.HasOne(d => d.Module).WithMany()
                .HasForeignKey(d => d.ModuleId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_tenant_modules_module");
        });

        // -------- roles (tenant-scoped catalog) -----------------------------
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");
            entity.ToTable("roles");

            entity.HasIndex(e => new { e.TenantId, e.Code }, "roles_tenant_code_uq").IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IsActive }, "roles_tenant_active_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Code).HasMaxLength(50).HasColumnName("code");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
            entity.Property(e => e.Description).HasMaxLength(500).HasColumnName("description");
            entity.Property(e => e.IsSystem).HasDefaultValue(false).HasColumnName("is_system");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_roles_tenant");
        });

        // -------- user_roles (M2M, append-only) -----------------------------
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_roles_pkey");
            entity.ToTable("user_roles");

            entity.HasIndex(e => new { e.TenantId, e.UserId, e.RoleId }, "user_roles_tenant_user_role_active_uq")
                .IsUnique()
                .HasFilter("\"revoked_at\" IS NULL");
            entity.HasIndex(e => new { e.TenantId, e.UserId }, "user_roles_tenant_user_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("assigned_at");
            entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
            entity.Property(e => e.RevokedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("revoked_at");
            entity.Property(e => e.RevokedBy).HasColumnName("revoked_by");
            entity.Property(e => e.RevokeReason).HasMaxLength(500).HasColumnName("revoke_reason");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_user_roles_user");

            entity.HasOne(d => d.Role).WithMany()
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_user_roles_role");
        });

        // -------- role_permissions.role_id — new nullable FK ----------------
        // Column added by the schema migration. role_name stays FOREVER per
        // ADR §Y (soft-delete) and is dual-written on every insert.
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.Property(e => e.RoleId).HasColumnName("role_id");

            entity.HasOne(d => d.Role).WithMany()
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_role_permissions_role");

            entity.HasIndex(e => e.RoleId, "role_permissions_role_id_idx");
        });
    }
}
