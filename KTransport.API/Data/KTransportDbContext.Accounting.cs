using KTransport.API.Common;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Data;

/// <summary>
/// TASK-016 Phase 1: DbSets + Fluent API for the Accounting &amp; Finance ledger
/// (<c>accounts</c>, <c>financial_periods</c>, <c>vouchers</c>, <c>voucher_lines</c>,
/// <c>opening_balances</c>) per ADR
/// <c>.agent/specs/decisions/accounting-ledger-architecture.md</c>.
///
/// Enums are persisted as readable strings (HasConversion&lt;string&gt;()) so the DB rows
/// speak Tally's vocabulary and export cleanly. The per-voucher balancing invariant
/// (ΣDR = ΣCR) and the amount &gt; 0 check are enforced by a trigger created in the
/// migration — the model config here cannot express a cross-row CHECK.
///
/// Hooked into the main context via <see cref="ConfigureAccountingModel"/>, invoked from
/// <c>OnModelCreating</c> at the bottom of KTransportDbContext.cs.
/// </summary>
public partial class KTransportDbContext
{
    public virtual DbSet<Account> Accounts { get; set; } = null!;
    public virtual DbSet<FinancialPeriod> FinancialPeriods { get; set; } = null!;
    public virtual DbSet<Voucher> Vouchers { get; set; } = null!;
    public virtual DbSet<VoucherLine> VoucherLines { get; set; } = null!;
    public virtual DbSet<OpeningBalance> OpeningBalances { get; set; } = null!;

    // Phase 2 auto-posting.
    public virtual DbSet<AutoPostingQueueRow> AutoPostingQueue { get; set; } = null!;
    public virtual DbSet<AccountingSettings> AccountingSettings { get; set; } = null!;

    private void ConfigureAccountingModel(ModelBuilder modelBuilder)
    {
        // ---------------- accounts (chart of accounts, self-referencing tree) ----------------
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("accounts_pkey");
            entity.ToTable("accounts");

            entity.HasIndex(e => new { e.TenantId, e.Code }, "accounts_tenant_code_uq").IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.ParentId }, "accounts_tenant_parent_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.Code).HasMaxLength(30).HasColumnName("code");
            entity.Property(e => e.Name).HasMaxLength(150).HasColumnName("name");
            entity.Property(e => e.AccountType).HasConversion<string>().HasMaxLength(12).HasColumnName("account_type");
            entity.Property(e => e.Nature).HasConversion<string>().HasMaxLength(2).HasColumnName("nature");
            entity.Property(e => e.IsGroup).HasDefaultValue(false).HasColumnName("is_group");
            entity.Property(e => e.IsSystem).HasDefaultValue(false).HasColumnName("is_system");
            entity.Property(e => e.SourcePartyId).HasColumnName("source_party_id");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_accounts_tenant");

            entity.HasOne(d => d.Parent).WithMany(p => p.Children)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_accounts_parent");
        });

        // ---------------- financial_periods (FY Apr–Mar) ----------------
        modelBuilder.Entity<FinancialPeriod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("financial_periods_pkey");
            entity.ToTable("financial_periods");

            entity.HasIndex(e => new { e.TenantId, e.FyLabel }, "financial_periods_tenant_fy_uq").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.FyLabel).HasMaxLength(9).HasColumnName("fy_label");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(8).HasDefaultValue(PeriodStatus.Open).HasColumnName("status");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_financial_periods_tenant");
        });

        // ---------------- vouchers (header; no amount column) ----------------
        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vouchers_pkey");
            entity.ToTable("vouchers");

            entity.HasIndex(e => new { e.TenantId, e.VoucherType, e.VoucherNo }, "vouchers_tenant_type_no_uq").IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Date }, "vouchers_tenant_date_idx");
            // Phase 2 (ADR Q4): PARTIAL UNIQUE — at most one LIVE (non-reversed, source-bearing)
            // voucher per (tenant, source_type, source_id). Excludes reversal vouchers
            // (reversal_of_id IS NOT NULL) and lets a fresh voucher post after the prior one is
            // Reversed (so reverse+repost works). Replaces the Phase 1 non-unique source index.
            entity.HasIndex(e => new { e.TenantId, e.SourceType, e.SourceId }, "vouchers_tenant_source_live_uq")
                .IsUnique()
                .HasFilter("source_id IS NOT NULL AND reversal_of_id IS NULL AND status <> 'Reversed'");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.VoucherType).HasConversion<string>().HasMaxLength(12).HasColumnName("voucher_type");
            entity.Property(e => e.VoucherNo).HasMaxLength(40).HasColumnName("voucher_no");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.PeriodId).HasColumnName("period_id");
            entity.Property(e => e.Narration).HasMaxLength(1000).HasColumnName("narration");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(8).HasDefaultValue(VoucherStatus.Posted).HasColumnName("status");
            entity.Property(e => e.SourceType).HasMaxLength(40).HasColumnName("source_type");
            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.Property(e => e.ReversalOfId).HasColumnName("reversal_of_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_vouchers_tenant");

            entity.HasOne(d => d.Period).WithMany()
                .HasForeignKey(d => d.PeriodId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_vouchers_period");

            entity.HasOne(d => d.ReversalOf).WithMany()
                .HasForeignKey(d => d.ReversalOfId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_vouchers_reversal");
        });

        // ---------------- voucher_lines (append-only child) ----------------
        modelBuilder.Entity<VoucherLine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("voucher_lines_pkey");
            entity.ToTable("voucher_lines", t =>
                t.HasCheckConstraint("ck_voucher_lines_amount_positive", "amount > 0"));

            entity.HasIndex(e => new { e.TenantId, e.VoucherId }, "voucher_lines_tenant_voucher_idx");
            entity.HasIndex(e => new { e.TenantId, e.AccountId }, "voucher_lines_tenant_account_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.VoucherId).HasColumnName("voucher_id");
            entity.Property(e => e.AccountId).HasColumnName("account_id");
            entity.Property(e => e.DrCr).HasConversion<string>().HasMaxLength(2).HasColumnName("dr_cr");
            entity.Property(e => e.Amount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("amount");
            entity.Property(e => e.LineNarration).HasMaxLength(500).HasColumnName("line_narration");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Voucher).WithMany(v => v.Lines)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_voucher_lines_voucher");

            entity.HasOne(d => d.Account).WithMany()
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_voucher_lines_account");
        });

        // ---------------- opening_balances ----------------
        modelBuilder.Entity<OpeningBalance>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("opening_balances_pkey");
            entity.ToTable("opening_balances");

            entity.HasIndex(e => new { e.TenantId, e.AccountId, e.PeriodId }, "opening_balances_tenant_acct_period_uq").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.AccountId).HasColumnName("account_id");
            entity.Property(e => e.PeriodId).HasColumnName("period_id");
            entity.Property(e => e.DrCr).HasConversion<string>().HasMaxLength(2).HasColumnName("dr_cr");
            entity.Property(e => e.Amount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("amount");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Account).WithMany()
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_opening_balances_account");

            entity.HasOne(d => d.Period).WithMany()
                .HasForeignKey(d => d.PeriodId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_opening_balances_period");
        });

        // ---------------- auto_posting_queue (Phase 2 transactional outbox / work queue) ----------------
        modelBuilder.Entity<AutoPostingQueueRow>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("auto_posting_queue_pkey");
            entity.ToTable("auto_posting_queue");

            entity.HasIndex(e => new { e.TenantId, e.Status }, "auto_posting_queue_tenant_status_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.SourceType).HasMaxLength(40).HasColumnName("source_type");
            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.Property(e => e.Operation).HasConversion<string>().HasMaxLength(10).HasColumnName("operation");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(10).HasDefaultValue(AutoPostStatus.Pending).HasColumnName("status");
            entity.Property(e => e.Attempts).HasDefaultValue(0).HasColumnName("attempts");
            entity.Property(e => e.LastError).HasMaxLength(1000).HasColumnName("last_error");
            entity.Property(e => e.EnqueuedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("enqueued_at");
            entity.Property(e => e.ProcessedAt).HasColumnType("timestamp without time zone").HasColumnName("processed_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);
        });

        // ---------------- accounting_settings (Phase 2 per-tenant 1:1 extension; DATABASE.md rule 2) ----------------
        modelBuilder.Entity<AccountingSettings>(entity =>
        {
            entity.HasKey(e => e.TenantId).HasName("accounting_settings_pkey");
            entity.ToTable("accounting_settings");

            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.AutoPostingEnabled).HasDefaultValue(false).HasColumnName("auto_posting_enabled");
            entity.Property(e => e.DefaultBankAccountId).HasColumnName("default_bank_account_id");
            entity.Property(e => e.EnabledAt).HasColumnType("timestamp without time zone").HasColumnName("enabled_at");
            entity.Property(e => e.EnabledBy).HasColumnName("enabled_by");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne<Tenant>().WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_accounting_settings_tenant");

            entity.HasOne<Account>().WithMany()
                .HasForeignKey(e => e.DefaultBankAccountId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_accounting_settings_default_bank");
        });
    }
}
