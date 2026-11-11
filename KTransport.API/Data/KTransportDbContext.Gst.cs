using System;
using KTransport.API.Models;
using KTransport.API.Models.Gst;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Data;

public partial class KTransportDbContext
{
    public virtual DbSet<GstRegistration> GstRegistrations { get; set; } = null!;
    public virtual DbSet<GstRegistrationGstProfile> GstRegistrationGstProfiles { get; set; } = null!;
    public virtual DbSet<PartyGstProfile> PartyGstProfiles { get; set; } = null!;
    public virtual DbSet<GstRule> GstRules { get; set; } = null!;
    public virtual DbSet<TaxCalculation> TaxCalculations { get; set; } = null!;
    public virtual DbSet<EWayBillCache> EWayBillCache { get; set; } = null!;

    // NOTE: OnModelCreatingPartial is implemented once in
    // KTransportDbContext.Entitlements.cs, which calls ConfigureGstEntities.
    // A partial method may have only one implementing declaration.
    private void ConfigureGstEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GstRegistration>(entity =>
        {
            entity.ToTable("gst_registrations");
            entity.HasKey(e => e.Id).HasName("gst_registrations_pkey");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Name).HasMaxLength(150).HasColumnName("name");
            entity.Property(e => e.State).HasMaxLength(100).HasColumnName("state");
            entity.Property(e => e.StateCode).HasMaxLength(2).HasColumnName("state_code");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.Pincode).HasMaxLength(10).HasColumnName("pincode");
            entity.Property(e => e.IsPrincipal).HasDefaultValue(false).HasColumnName("is_principal");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasIndex(e => new { e.TenantId, e.IsActive }).HasDatabaseName("ix_gst_registrations_tenant_active");

            // Exactly one principal per tenant — Postgres partial unique index.
            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("ux_gst_registrations_tenant_principal")
                .HasFilter("is_principal = true")
                .IsUnique();

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<GstRegistrationGstProfile>(entity =>
        {
            entity.ToTable("gst_registration_profiles");
            entity.HasKey(e => e.Id).HasName("gst_registration_profiles_pkey");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.GstRegistrationId).HasColumnName("gst_registration_id");
            entity.Property(e => e.Gstin).HasMaxLength(20).HasColumnName("gstin");
            entity.Property(e => e.GstRegistered).HasDefaultValue(false).HasColumnName("gst_registered");
            entity.Property(e => e.IsGta).HasDefaultValue(false).HasColumnName("is_gta");
            entity.Property(e => e.IssuesConsignmentNote).HasDefaultValue(false).HasColumnName("issues_consignment_note");
            entity.Property(e => e.GtaTaxScheme).HasConversion<int>().HasColumnName("gta_tax_scheme");
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.EffectiveTo).HasColumnName("effective_to");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasOne(e => e.GstRegistration)
                .WithMany(r => r.Profiles)
                .HasForeignKey(e => e.GstRegistrationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_gst_registration_profiles_registration");

            entity.HasIndex(e => new { e.TenantId, e.GstRegistrationId, e.EffectiveFrom })
                .HasDatabaseName("ix_gst_registration_profiles_tenant_reg_effective");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);
        });

        // Merge-pattern: add the registration FK to the existing Location config.
        modelBuilder.Entity<Location>(entity =>
        {
            entity.Property(e => e.GstRegistrationId).HasColumnName("gst_registration_id");

            entity.HasOne(e => e.GstRegistration)
                .WithMany()
                .HasForeignKey(e => e.GstRegistrationId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_locations_gst_registration");

            entity.HasIndex(e => e.GstRegistrationId).HasDatabaseName("ix_locations_gst_registration");
        });

        // Merge-pattern: HomeLocation FK on the existing User config.
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.HomeLocationId).HasColumnName("home_location_id");

            entity.HasOne(e => e.HomeLocation)
                .WithMany()
                .HasForeignKey(e => e.HomeLocationId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_users_home_location");
        });

        modelBuilder.Entity<PartyGstProfile>(entity =>
        {
            entity.ToTable("party_gst_profiles");
            entity.HasKey(e => e.Id).HasName("party_gst_profiles_pkey");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.PartyId).HasColumnName("party_id");
            entity.Property(e => e.Gstin).HasMaxLength(20).HasColumnName("gstin");
            entity.Property(e => e.GstRegistered).HasDefaultValue(false).HasColumnName("gst_registered");
            entity.Property(e => e.State).HasMaxLength(100).HasColumnName("state");
            entity.Property(e => e.StateCode).HasMaxLength(2).HasColumnName("state_code");
            entity.Property(e => e.RecipientCategory).HasConversion<int>().HasColumnName("recipient_category");
            entity.Property(e => e.PlaceOfSupplyDefault).HasMaxLength(100).HasColumnName("place_of_supply_default");
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.EffectiveTo).HasColumnName("effective_to");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasIndex(e => new { e.TenantId, e.PartyId, e.EffectiveFrom }).HasDatabaseName("ix_party_gst_profiles_tenant_party_effective");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<GstRule>(entity =>
        {
            entity.ToTable("gst_rules");
            entity.HasKey(e => e.Id).HasName("gst_rules_pkey");
            entity.Property(e => e.Id).HasColumnName("id");

            entity.Property(e => e.ServiceType).HasConversion<int>().HasColumnName("service_type");
            entity.Property(e => e.ProviderIsGta).HasColumnName("provider_is_gta");
            entity.Property(e => e.IssuesConsignmentNote).HasColumnName("issues_consignment_note");
            entity.Property(e => e.ProviderGstRegistered).HasColumnName("provider_gst_registered");
            entity.Property(e => e.GtaTaxScheme).HasConversion<int>().HasColumnName("gta_tax_scheme");
            entity.Property(e => e.RecipientCategory).HasConversion<int>().HasColumnName("recipient_category");
            entity.Property(e => e.PosRelation).HasConversion<int>().HasColumnName("pos_relation");

            entity.Property(e => e.TaxTreatment).HasConversion<int>().HasColumnName("tax_treatment");
            entity.Property(e => e.GstRate).HasPrecision(6, 4).HasColumnName("gst_rate");
            entity.Property(e => e.CgstRate).HasPrecision(6, 4).HasColumnName("cgst_rate");
            entity.Property(e => e.SgstRate).HasPrecision(6, 4).HasColumnName("sgst_rate");
            entity.Property(e => e.IgstRate).HasPrecision(6, 4).HasColumnName("igst_rate");
            entity.Property(e => e.UtgstRate).HasPrecision(6, 4).HasColumnName("utgst_rate");
            entity.Property(e => e.ItcEligibleForProvider).HasDefaultValue(false).HasColumnName("itc_eligible_for_provider");
            entity.Property(e => e.RcmPayableBy).HasMaxLength(30).HasColumnName("rcm_payable_by");
            entity.Property(e => e.NotificationReference).HasMaxLength(300).HasColumnName("notification_reference");
            entity.Property(e => e.ExceptionsNote).HasColumnName("exceptions_note");

            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.EffectiveTo).HasColumnName("effective_to");
            entity.Property(e => e.Active).HasDefaultValue(true).HasColumnName("active");

            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasIndex(e => new
            {
                e.ServiceType, e.ProviderIsGta, e.IssuesConsignmentNote,
                e.ProviderGstRegistered, e.GtaTaxScheme, e.RecipientCategory,
                e.PosRelation, e.EffectiveFrom
            }).HasDatabaseName("ix_gst_rules_tuple_effective").IsUnique();

            entity.HasIndex(e => new { e.Active, e.EffectiveFrom, e.EffectiveTo }).HasDatabaseName("ix_gst_rules_active_window");
            // Global (not tenant-scoped) — platform-admin maintained; no query filter.
        });

        modelBuilder.Entity<TaxCalculation>(entity =>
        {
            entity.ToTable("tax_calculations");
            entity.HasKey(e => e.Id).HasName("tax_calculations_pkey");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.BiltyId).HasColumnName("bilty_id");
            entity.Property(e => e.ShipmentId).HasColumnName("shipment_id");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.InputJson).HasColumnType("jsonb").HasColumnName("input_json");
            entity.Property(e => e.RuleVersionId).HasColumnName("rule_version_id");

            entity.Property(e => e.TaxTreatment).HasConversion<int>().HasColumnName("tax_treatment");
            entity.Property(e => e.GstRate).HasPrecision(6, 4).HasColumnName("gst_rate");
            entity.Property(e => e.CgstRate).HasPrecision(6, 4).HasColumnName("cgst_rate");
            entity.Property(e => e.CgstAmount).HasPrecision(14, 2).HasColumnName("cgst_amount");
            entity.Property(e => e.SgstRate).HasPrecision(6, 4).HasColumnName("sgst_rate");
            entity.Property(e => e.SgstAmount).HasPrecision(14, 2).HasColumnName("sgst_amount");
            entity.Property(e => e.IgstRate).HasPrecision(6, 4).HasColumnName("igst_rate");
            entity.Property(e => e.IgstAmount).HasPrecision(14, 2).HasColumnName("igst_amount");
            entity.Property(e => e.UtgstRate).HasPrecision(6, 4).HasColumnName("utgst_rate");
            entity.Property(e => e.UtgstAmount).HasPrecision(14, 2).HasColumnName("utgst_amount");
            entity.Property(e => e.TaxableAmount).HasPrecision(14, 2).HasColumnName("taxable_amount");
            entity.Property(e => e.TotalTax).HasPrecision(14, 2).HasColumnName("total_tax");
            entity.Property(e => e.GrandTotal).HasPrecision(14, 2).HasColumnName("grand_total");

            entity.Property(e => e.IsRcm).HasDefaultValue(false).HasColumnName("is_rcm");
            entity.Property(e => e.IsItcEligible).HasDefaultValue(false).HasColumnName("is_itc_eligible");

            entity.Property(e => e.PlaceOfSupply).HasMaxLength(100).HasColumnName("place_of_supply");
            entity.Property(e => e.PosRelation).HasConversion<int?>().HasColumnName("pos_relation");

            entity.Property(e => e.CalculatedBy).HasColumnName("calculated_by");
            entity.Property(e => e.CalculatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("calculated_at");

            entity.Property(e => e.NotificationReference).HasMaxLength(300).HasColumnName("notification_reference");
            entity.Property(e => e.SupersededBy).HasColumnName("superseded_by");

            entity.HasOne<GstRule>()
                .WithMany()
                .HasForeignKey(e => e.RuleVersionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_tax_calculations_rule");

            entity.HasIndex(e => e.BiltyId).HasDatabaseName("ix_tax_calculations_bilty");
            entity.HasIndex(e => e.ShipmentId).HasDatabaseName("ix_tax_calculations_shipment");
            entity.HasIndex(e => e.CalculatedAt).HasDatabaseName("ix_tax_calculations_calculated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);
        });

        // Extend the existing Shipment config with the GST audit link.
        // Multiple modelBuilder.Entity<Shipment>() calls MERGE in EF Core —
        // this adds the column + FK without touching the main OnModelCreating
        // definition in KTransportDbContext.cs.
        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.Property(e => e.GstCalculationId).HasColumnName("gst_calculation_id");

            entity.HasOne<TaxCalculation>()
                .WithMany()
                .HasForeignKey(e => e.GstCalculationId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_shipments_gst_calculation");

            entity.HasIndex(e => e.GstCalculationId).HasDatabaseName("ix_shipments_gst_calculation");

            // 2026-06-15 NIC rule: Ship-To GSTIN mandatory on Bill-To / Ship-To EWBs.
            entity.Property(e => e.ShipToGstin).HasMaxLength(20).HasColumnName("ship_to_gstin");
            entity.Property(e => e.ShipToStateCode).HasMaxLength(2).HasColumnName("ship_to_state_code");
            entity.Property(e => e.EwbTransactionType).HasColumnName("ewb_transaction_type");

            // TASK-033: BookingLocation drives GST provider identity.
            entity.Property(e => e.BookingLocationId).HasColumnName("booking_location_id");
            entity.HasOne(e => e.BookingLocation)
                .WithMany()
                .HasForeignKey(e => e.BookingLocationId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_shipments_booking_location");
            entity.HasIndex(e => e.BookingLocationId).HasDatabaseName("ix_shipments_booking_location");
        });

        modelBuilder.Entity<EWayBillCache>(entity =>
        {
            entity.ToTable("eway_bill_cache");
            entity.HasKey(e => e.Id).HasName("eway_bill_cache_pkey");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.EwbNumber).HasMaxLength(20).HasColumnName("ewb_number");
            entity.Property(e => e.EwbDate).HasColumnType("timestamp without time zone").HasColumnName("ewb_date");
            entity.Property(e => e.ValidUpto).HasColumnType("timestamp without time zone").HasColumnName("valid_upto");
            entity.Property(e => e.Status).HasMaxLength(10).HasColumnName("status");
            entity.Property(e => e.SyncStatus).HasConversion<int>().HasColumnName("sync_status");

            entity.Property(e => e.SupplyType).HasMaxLength(2).HasColumnName("supply_type");
            entity.Property(e => e.SubSupplyType).HasMaxLength(5).HasColumnName("sub_supply_type");
            entity.Property(e => e.DocType).HasMaxLength(5).HasColumnName("doc_type");
            entity.Property(e => e.DocNo).HasMaxLength(50).HasColumnName("doc_no");
            entity.Property(e => e.DocDate).HasColumnName("doc_date");

            entity.Property(e => e.FromGstin).HasMaxLength(20).HasColumnName("from_gstin");
            entity.Property(e => e.FromStateCode).HasMaxLength(2).HasColumnName("from_state_code");
            entity.Property(e => e.ToGstin).HasMaxLength(20).HasColumnName("to_gstin");
            entity.Property(e => e.ToStateCode).HasMaxLength(2).HasColumnName("to_state_code");
            entity.Property(e => e.ShipToGstin).HasMaxLength(20).HasColumnName("ship_to_gstin");
            entity.Property(e => e.ShipToStateCode).HasMaxLength(2).HasColumnName("ship_to_state_code");

            entity.Property(e => e.TransactionType).HasColumnName("transaction_type");
            entity.Property(e => e.TotalInvoiceValue).HasPrecision(14, 2).HasColumnName("total_invoice_value");
            entity.Property(e => e.TransDistance).HasPrecision(10, 2).HasColumnName("trans_distance");

            entity.Property(e => e.VehicleNo).HasMaxLength(20).HasColumnName("vehicle_no");
            entity.Property(e => e.TransporterId).HasMaxLength(20).HasColumnName("transporter_id");
            entity.Property(e => e.TransporterName).HasMaxLength(150).HasColumnName("transporter_name");

            entity.Property(e => e.PayloadJson).HasColumnType("jsonb").HasColumnName("payload_json");
            entity.Property(e => e.LastError).HasColumnName("last_error");

            entity.Property(e => e.ShipmentId).HasColumnName("shipment_id");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasIndex(e => new { e.TenantId, e.EwbNumber }).IsUnique().HasDatabaseName("ux_eway_bill_cache_tenant_ewb");
            entity.HasIndex(e => e.ShipmentId).HasDatabaseName("ix_eway_bill_cache_shipment");
            entity.HasIndex(e => e.ValidUpto).HasDatabaseName("ix_eway_bill_cache_valid_upto");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);
        });
    }
}
