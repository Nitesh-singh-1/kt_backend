using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Data;

public partial class KTransportDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    static KTransportDbContext()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public KTransportDbContext()
    {
    }

    public KTransportDbContext(DbContextOptions<KTransportDbContext> options, ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public virtual DbSet<Tenant> Tenants { get; set; }
    public virtual DbSet<BillType> BillTypes { get; set; }
    public virtual DbSet<Charge> Charges { get; set; }
    public virtual DbSet<Challan> Challans { get; set; }
    public virtual DbSet<ChallanDetail> ChallanDetails { get; set; }

    public virtual DbSet<GoodsDetail> GoodsDetails { get; set; }
    public virtual DbSet<GstBill> GstBills { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<WithoutGstBill> WithoutGstBills { get; set; }

    public virtual DbSet<Shipment> Shipments { get; set; }
    public virtual DbSet<ShipmentItem> ShipmentItems { get; set; }
    public virtual DbSet<ShipmentChargeItem> ShipmentChargeItems { get; set; }
    public virtual DbSet<ChargeType> ChargeTypes { get; set; }
    public virtual DbSet<ShipmentStatusHistory> ShipmentStatusHistories { get; set; }
    public virtual DbSet<NumberingSequence> NumberingSequences { get; set; }
    public virtual DbSet<Party> Parties { get; set; }
    public virtual DbSet<Invoice> Invoices { get; set; }
    public virtual DbSet<InvoiceItem> InvoiceItems { get; set; }
    public virtual DbSet<Vehicle> Vehicles { get; set; }
    public virtual DbSet<Driver> Drivers { get; set; }
    public virtual DbSet<Location> Locations { get; set; }
    public virtual DbSet<Trip> Trips { get; set; }
    public virtual DbSet<TripShipment> TripShipments { get; set; }
    public virtual DbSet<TripExpense> TripExpenses { get; set; }
    public virtual DbSet<Vendor> Vendors { get; set; }
    public virtual DbSet<LorryHireContract> LorryHireContracts { get; set; }
    public virtual DbSet<PodRecord> PodRecords { get; set; }
    public virtual DbSet<FreightRateCard> FreightRateCards { get; set; }
    public virtual DbSet<VehicleMaintenance> VehicleMaintenances { get; set; }
    public virtual DbSet<CargoClaim> CargoClaims { get; set; }

    public override int SaveChanges()
    {
        ApplyTenantId();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantId();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTenantId()
    {
        if (_tenantContext == null || !_tenantContext.HasTenant) return;

        var entries = ChangeTracker.Entries<ITenantScopedEntity>()
            .Where(e => e.State == EntityState.Added && e.Entity.TenantId == Guid.Empty);

        foreach (var entry in entries)
        {
            entry.Entity.TenantId = _tenantContext.CurrentTenantId;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tenants_pkey");
            entity.ToTable("tenants");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasMaxLength(150).HasColumnName("name");
            entity.Property(e => e.Code).HasMaxLength(50).HasColumnName("code");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasData(new Tenant
            {
                Id = TenantContext.DefaultTenantId,
                Name = "Default Organization",
                Code = "DEFAULT",
                IsActive = true,
                CreatedAt = DateTime.Parse("2026-04-03 15:56:50.696525")
            });
        });

        modelBuilder.Entity<Charge>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("charges_pkey");

            entity.ToTable("charges");

            entity.HasIndex(e => e.BillId, "charges_bill_id_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId)
                .HasDefaultValue(TenantContext.DefaultTenantId)
                .HasColumnName("tenant_id");
            entity.Property(e => e.BillId).HasColumnName("bill_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.DdCharge)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("dd_charge");
            entity.Property(e => e.Freight)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("freight");
            entity.Property(e => e.GrandTotal)
                .HasPrecision(12, 2)
                .HasColumnName("grand_total");
            entity.Property(e => e.Hamali)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("hamali");
            entity.Property(e => e.OtherCharge)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("other_charge");
            entity.Property(e => e.ServiceCharge)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("service_charge");
            entity.Property(e => e.StCharge)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("st_charge");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Bill).WithOne(p => p.Charge)
                .HasForeignKey<Charge>(d => d.BillId)
                .HasConstraintName("fk_charges_bill");
        });

        modelBuilder.Entity<GoodsDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("goods_details_pkey");

            entity.ToTable("goods_details");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId)
                .HasDefaultValue(TenantContext.DefaultTenantId)
                .HasColumnName("tenant_id");
            entity.Property(e => e.Article)
                .HasMaxLength(100)
                .HasColumnName("article");
            entity.Property(e => e.BillId).HasColumnName("bill_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Rate)
                .HasPrecision(10, 2)
                .HasColumnName("rate");
            entity.Property(e => e.Weight)
                .HasPrecision(10, 2)
                .HasColumnName("weight");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Bill).WithMany(p => p.GoodsDetails)
                .HasForeignKey(d => d.BillId)
                .HasConstraintName("fk_goods_bill");
        });

        modelBuilder.Entity<GstBill>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("gst_bills_pkey");

            entity.ToTable("gst_bills");

            entity.HasIndex(e => e.GrNo, "gst_bills_gr_no_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId)
                .HasDefaultValue(TenantContext.DefaultTenantId)
                .HasColumnName("tenant_id");
            entity.Property(e => e.BookingClerk)
                .HasMaxLength(100)
                .HasColumnName("booking_clerk");
            entity.Property(e => e.ConsigneeAddress).HasColumnName("consignee_address");
            entity.Property(e => e.ConsigneeGstNo)
                .HasMaxLength(20)
                .HasColumnName("consignee_gst_no");
            entity.Property(e => e.ConsigneeMobile)
                .HasMaxLength(15)
                .HasColumnName("consignee_mobile");
            entity.Property(e => e.ConsigneeName)
                .HasMaxLength(100)
                .HasColumnName("consignee_name");
            entity.Property(e => e.ConsignerGstNo)
                .HasMaxLength(20)
                .HasColumnName("consigner_gst_no");
            entity.Property(e => e.ConsignerMobile)
                .HasMaxLength(15)
                .HasColumnName("consigner_mobile");
            entity.Property(e => e.ConsignerName)
                .HasMaxLength(100)
                .HasColumnName("consigner_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeliveryStatus)
                .HasMaxLength(20)
                .HasColumnName("delivery_status");
            entity.Property(e => e.FromLocation)
                .HasMaxLength(100)
                .HasColumnName("from_location");
            entity.Property(e => e.GoodsValue)
                .HasPrecision(12, 2)
                .HasColumnName("goods_value");
            entity.Property(e => e.GrDate).HasColumnName("gr_date");
            entity.Property(e => e.GrNo)
                .HasMaxLength(20)
                .HasColumnName("gr_no");
            entity.Property(e => e.GstPaidBy)
                .HasMaxLength(20)
                .HasColumnName("gst_paid_by");
            entity.Property(e => e.InvoiceDate).HasColumnName("invoice_date");
            entity.Property(e => e.InvoiceNo)
                .HasMaxLength(50)
                .HasColumnName("invoice_no");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Paid)
                .HasPrecision(12, 2)
                .HasColumnName("paid");
            entity.Property(e => e.Remarks).HasColumnName("remarks");
            entity.Property(e => e.Tbb)
                .HasPrecision(12, 2)
                .HasColumnName("tbb");
            entity.Property(e => e.ToLocation)
                .HasMaxLength(100)
                .HasColumnName("to_location");
            entity.Property(e => e.ToPay)
                .HasPrecision(12, 2)
                .HasColumnName("to_pay");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(12, 2)
                .HasColumnName("total_amount");
            entity.Property(e => e.TruckNo)
                .HasMaxLength(20)
                .HasColumnName("truck_no");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(d => d.Consigneeraddress).HasMaxLength(200).HasColumnName("consigneeraddress");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany(p => p.GstBills)
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_gst_bills_tenant");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.GstBillCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.GstBillUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_updated_by");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId)
                .HasDefaultValue(TenantContext.DefaultTenantId)
                .HasColumnName("tenant_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("full_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Mobile)
                .HasMaxLength(10)
                .HasColumnName("mobile");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("password");
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .HasColumnName("role");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Users)
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_users_tenant");

            entity.HasData(new User
            {
                Id = 1,
                TenantId = TenantContext.DefaultTenantId,
                Username = "admin",
                Password = "$2a$11$0aBw4j5tM1Ew2k5hF8O/TehI5jY9K2HkZ0K6o0tM6dYp/1R9Gq8m6", // admin123
                FullName = "Kundan Kumar",
                Role = "admin",
                IsActive = true,
                CreatedAt = DateTime.Parse("2026-04-03 15:56:50.696525"),
                Mobile = "9504600060"
            });
        });

        modelBuilder.Entity<WithoutGstBill>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("without_gst_bills_pkey");

            entity.ToTable("without_gst_bills");

            entity.HasIndex(e => e.GrNo, "without_gst_bills_gr_no_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId)
                .HasDefaultValue(TenantContext.DefaultTenantId)
                .HasColumnName("tenant_id");
            entity.Property(e => e.BookingClerk)
                .HasMaxLength(100)
                .HasColumnName("booking_clerk");
            entity.Property(e => e.ConsigneeAddress).HasColumnName("consignee_address");
            entity.Property(e => e.ConsigneeMobile)
                .HasMaxLength(15)
                .HasColumnName("consignee_mobile");
            entity.Property(e => e.ConsigneeName)
                .HasMaxLength(100)
                .HasColumnName("consignee_name");
            entity.Property(e => e.ConsignerMobile)
                .HasMaxLength(15)
                .HasColumnName("consigner_mobile");
            entity.Property(e => e.ConsignerName)
                .HasMaxLength(100)
                .HasColumnName("consigner_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeliveryStatus)
                .HasMaxLength(20)
                .HasColumnName("delivery_status");
            entity.Property(e => e.FromLocation)
                .HasMaxLength(100)
                .HasColumnName("from_location");
            entity.Property(e => e.GoodsValue)
                .HasPrecision(12, 2)
                .HasColumnName("goods_value");
            entity.Property(e => e.GrDate).HasColumnName("gr_date");
            entity.Property(e => e.GrNo)
                .HasMaxLength(20)
                .HasColumnName("gr_no");
            entity.Property(e => e.InvoiceDate).HasColumnName("invoice_date");
            entity.Property(e => e.InvoiceNo)
                .HasMaxLength(50)
                .HasColumnName("invoice_no");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Paid)
                .HasPrecision(12, 2)
                .HasColumnName("paid");
            entity.Property(e => e.Remarks).HasColumnName("remarks");
            entity.Property(e => e.Tbb)
                .HasPrecision(12, 2)
                .HasColumnName("tbb");
            entity.Property(e => e.ToLocation)
                .HasMaxLength(100)
                .HasColumnName("to_location");
            entity.Property(e => e.ToPay)
                .HasPrecision(12, 2)
                .HasColumnName("to_pay");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(12, 2)
                .HasColumnName("total_amount");
            entity.Property(e => e.TruckNo)
                .HasMaxLength(20)
                .HasColumnName("truck_no");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany(p => p.WithoutGstBills)
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_wgst_tenant");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.WithoutGstBillCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_wgst_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.WithoutGstBillUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_wgst_updated_by");
        });

        modelBuilder.Entity<BillType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("BillType_pkey");

            entity.ToTable("BillType");

            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("Name");
        });

        modelBuilder.Entity<Challan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("challan_pkey");

            entity.ToTable("challan");

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.TenantId)
                .HasDefaultValue(TenantContext.DefaultTenantId)
                .HasColumnName("TenantId");
            entity.Property(e => e.ChallanNo)
                .HasMaxLength(50)
                .HasColumnName("ChallanNo");
            entity.Property(e => e.ChallanDate).HasColumnName("ChallanDate");
            entity.Property(e => e.LorryNo)
                .HasMaxLength(50)
                .HasColumnName("LorryNo");
            entity.Property(e => e.DriverName)
                .HasMaxLength(100)
                .HasColumnName("DriverName");
            entity.Property(e => e.VoiceDriverName)
                .HasMaxLength(100)
                .HasColumnName("VoiceDriverName");
            entity.Property(e => e.FromLocation)
                .HasMaxLength(100)
                .HasColumnName("FromLocation");
            entity.Property(e => e.ToLocation)
                .HasMaxLength(100)
                .HasColumnName("ToLocation");
            entity.Property(e => e.Remarks).HasColumnName("Remarks");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("IsDeleted");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("CreatedDate");
            entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
            entity.Property(e => e.ModifiedDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("ModifiedDate");
            entity.Property(e => e.ModifiedBy).HasColumnName("ModifiedBy");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Challans)
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_challan_tenant");

            entity.HasOne(d => d.CreatedByNavigation)
                .WithMany(p => p.ChallanCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ModifiedByNavigation)
                .WithMany(p => p.ChallanModifiedByNavigations)
                .HasForeignKey(d => d.ModifiedBy)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ChallanDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("challanDetail_pkey");

            entity.ToTable("challandetail");

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.TenantId)
                .HasDefaultValue(TenantContext.DefaultTenantId)
                .HasColumnName("TenantId");
            entity.Property(e => e.ChallanId).HasColumnName("ChallanId");
            entity.Property(e => e.BillNo)
                .HasMaxLength(50)
                .HasColumnName("BillNo");
            entity.Property(e => e.Quantity).HasColumnName("Quantity");
            entity.Property(e => e.Destination)
                .HasMaxLength(100)
                .HasColumnName("Destination");
            entity.Property(e => e.FreightAmount)
                .HasPrecision(18, 2)
                .HasColumnName("FreightAmount");
            entity.Property(e => e.BillTypeId).HasColumnName("BillTypeId");
            entity.Property(e => e.ConsigneeName)
                .HasMaxLength(200)
                .HasColumnName("ConsigneeName");
            entity.Property(e => e.Remarks)
                .HasMaxLength(500)
                .HasColumnName("Remarks");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("IsDeleted");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("CreatedDate");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Challan)
                .WithMany(p => p.ChallanDetails)
                .HasForeignKey(d => d.ChallanId)
                .HasConstraintName("FK_ChallanDetail_Challan");

            entity.HasOne(d => d.BillType)
                .WithMany(p => p.ChallanDetails)
                .HasForeignKey(d => d.BillTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChallanDetail_BillType");
        });

        modelBuilder.Entity<ChargeType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("charge_types_pkey");
            entity.ToTable("charge_types");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Code).HasMaxLength(50).HasColumnName("code");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
            entity.Property(e => e.DefaultAmount).HasPrecision(12, 2).HasDefaultValue(0).HasColumnName("default_amount");
            entity.Property(e => e.IsTaxable).HasDefaultValue(false).HasColumnName("is_taxable");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_charge_types_tenant");

            // Seed standard default charge types
            entity.HasData(
                new ChargeType { Id = 1, TenantId = TenantConstants.DefaultTenantId, Code = "FREIGHT", Name = "Base Freight", DefaultAmount = 0, IsTaxable = true, IsActive = true },
                new ChargeType { Id = 2, TenantId = TenantConstants.DefaultTenantId, Code = "HAMALI", Name = "Loading / Hamali", DefaultAmount = 0, IsTaxable = false, IsActive = true },
                new ChargeType { Id = 3, TenantId = TenantConstants.DefaultTenantId, Code = "DOOR_DELIVERY", Name = "Door Delivery (DD)", DefaultAmount = 0, IsTaxable = true, IsActive = true },
                new ChargeType { Id = 4, TenantId = TenantConstants.DefaultTenantId, Code = "STATION_CHARGE", Name = "Stationary / ST Charge", DefaultAmount = 0, IsTaxable = false, IsActive = true },
                new ChargeType { Id = 5, TenantId = TenantConstants.DefaultTenantId, Code = "TOLL_SURCHARGE", Name = "Toll & Surcharge", DefaultAmount = 0, IsTaxable = false, IsActive = true }
            );
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipments_pkey");
            entity.ToTable("shipments");

            entity.HasIndex(e => new { e.TenantId, e.ShipmentNo }, "shipments_tenant_shipment_no_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ShipmentNo).HasMaxLength(50).HasColumnName("shipment_no");
            entity.Property(e => e.InvoiceNo).HasMaxLength(50).HasColumnName("invoice_no");
            entity.Property(e => e.ShipmentDate).HasColumnName("shipment_date");
            entity.Property(e => e.InvoiceDate).HasColumnName("invoice_date");
            entity.Property(e => e.FromLocation).HasMaxLength(150).HasColumnName("from_location");
            entity.Property(e => e.ToLocation).HasMaxLength(150).HasColumnName("to_location");
            entity.Property(e => e.TruckNo).HasMaxLength(30).HasColumnName("truck_no");
            entity.Property(e => e.TaxTreatment).HasConversion<int>().HasColumnName("tax_treatment");
            entity.Property(e => e.GstPaidBy).HasMaxLength(50).HasColumnName("gst_paid_by");

            entity.Property(e => e.ConsignorName).HasMaxLength(150).HasColumnName("consignor_name");
            entity.Property(e => e.ConsignorGstNo).HasMaxLength(30).HasColumnName("consignor_gst_no");
            entity.Property(e => e.ConsignorMobile).HasMaxLength(20).HasColumnName("consignor_mobile");
            entity.Property(e => e.ConsignorAddress).HasMaxLength(250).HasColumnName("consignor_address");

            entity.Property(e => e.ConsigneeName).HasMaxLength(150).HasColumnName("consignee_name");
            entity.Property(e => e.ConsigneeGstNo).HasMaxLength(30).HasColumnName("consignee_gst_no");
            entity.Property(e => e.ConsigneeMobile).HasMaxLength(20).HasColumnName("consignee_mobile");
            entity.Property(e => e.ConsigneeAddress).HasMaxLength(250).HasColumnName("consignee_address");

            entity.Property(e => e.GoodsValue).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("goods_value");
            entity.Property(e => e.PaymentTerm).HasConversion<int>().HasColumnName("payment_term");
            entity.Property(e => e.TotalFreight).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("total_freight");
            entity.Property(e => e.TotalOtherCharges).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("total_other_charges");
            entity.Property(e => e.TotalTaxAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("total_tax_amount");
            entity.Property(e => e.GrandTotal).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("grand_total");
            entity.Property(e => e.PaidAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("paid_amount");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.ConsignorPartyId).HasColumnName("consignor_party_id");
            entity.Property(e => e.ConsigneePartyId).HasColumnName("consignee_party_id");
            entity.Property(e => e.DueAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("due_amount");

            entity.Property(e => e.Status).HasConversion<int>().HasColumnName("status");
            entity.Property(e => e.Remarks).HasMaxLength(500).HasColumnName("remarks");
            entity.Property(e => e.BookingClerk).HasMaxLength(100).HasColumnName("booking_clerk");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_shipments_tenant");

            entity.HasOne(d => d.Invoice).WithMany(p => p.Shipments)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_shipments_invoice");

            entity.HasOne(d => d.ConsignorParty).WithMany()
                .HasForeignKey(d => d.ConsignorPartyId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_shipments_consignor_party");

            entity.HasOne(d => d.ConsigneeParty).WithMany()
                .HasForeignKey(d => d.ConsigneePartyId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_shipments_consignee_party");

            entity.HasOne(d => d.CreatedByNavigation).WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_shipments_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany()
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_shipments_updated_by");
        });

        modelBuilder.Entity<ShipmentItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipment_items_pkey");
            entity.ToTable("shipment_items");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ShipmentId).HasColumnName("shipment_id");
            entity.Property(e => e.Article).HasMaxLength(150).HasColumnName("article");
            entity.Property(e => e.Description).HasMaxLength(300).HasColumnName("description");
            entity.Property(e => e.Weight).HasPrecision(12, 3).HasDefaultValue(0).HasColumnName("weight");
            entity.Property(e => e.Rate).HasPrecision(12, 2).HasDefaultValue(0).HasColumnName("rate");
            entity.Property(e => e.Quantity).HasDefaultValue(1).HasColumnName("quantity");
            entity.Property(e => e.TotalAmount).HasPrecision(12, 2).HasDefaultValue(0).HasColumnName("total_amount");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Shipment).WithMany(p => p.Items)
                .HasForeignKey(d => d.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_shipment_items_shipment");
        });

        modelBuilder.Entity<ShipmentChargeItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipment_charge_items_pkey");
            entity.ToTable("shipment_charge_items");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ShipmentId).HasColumnName("shipment_id");
            entity.Property(e => e.ChargeTypeId).HasColumnName("charge_type_id");
            entity.Property(e => e.ChargeName).HasMaxLength(100).HasColumnName("charge_name");
            entity.Property(e => e.Amount).HasPrecision(12, 2).HasDefaultValue(0).HasColumnName("amount");
            entity.Property(e => e.IsTaxable).HasDefaultValue(false).HasColumnName("is_taxable");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Shipment).WithMany(p => p.ChargeItems)
                .HasForeignKey(d => d.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_shipment_charge_items_shipment");

            entity.HasOne(d => d.ChargeType).WithMany(p => p.ShipmentChargeItems)
                .HasForeignKey(d => d.ChargeTypeId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_shipment_charge_items_type");
        });

        modelBuilder.Entity<ShipmentStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipment_status_history_pkey");
            entity.ToTable("shipment_status_history");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ShipmentId).HasColumnName("shipment_id");
            entity.Property(e => e.FromStatus).HasConversion<int>().HasColumnName("from_status");
            entity.Property(e => e.ToStatus).HasConversion<int>().HasColumnName("to_status");
            entity.Property(e => e.Location).HasMaxLength(150).HasColumnName("location");
            entity.Property(e => e.Remarks).HasMaxLength(500).HasColumnName("remarks");
            entity.Property(e => e.ChangedByUserId).HasColumnName("changed_by_user_id");
            entity.Property(e => e.ChangedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("changed_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Shipment).WithMany(p => p.StatusHistory)
                .HasForeignKey(d => d.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_shipment_history_shipment");

            entity.HasOne(d => d.ChangedByUser).WithMany()
                .HasForeignKey(d => d.ChangedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_shipment_history_user");
        });

        modelBuilder.Entity<NumberingSequence>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("numbering_sequences_pkey");
            entity.ToTable("numbering_sequences");

            entity.HasIndex(e => new { e.TenantId, e.EntityType, e.Year }, "numbering_sequences_tenant_entity_year_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.EntityType).HasMaxLength(50).HasColumnName("entity_type");
            entity.Property(e => e.Prefix).HasMaxLength(20).HasColumnName("prefix");
            entity.Property(e => e.CurrentValue).HasDefaultValue(0).HasColumnName("current_value");
            entity.Property(e => e.Padding).HasDefaultValue(4).HasColumnName("padding");
            entity.Property(e => e.Year).HasColumnName("year");
            entity.Property(e => e.FormatPattern).HasMaxLength(100).HasDefaultValue("{PREFIX}-{YEAR}-{SEQ}").HasColumnName("format_pattern");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Party>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("parties_pkey");
            entity.ToTable("parties");

            entity.HasIndex(e => new { e.TenantId, e.Name }, "parties_tenant_name_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Name).HasMaxLength(150).HasColumnName("name");
            entity.Property(e => e.Code).HasMaxLength(50).HasColumnName("code");
            entity.Property(e => e.GstNo).HasMaxLength(30).HasColumnName("gst_no");
            entity.Property(e => e.PanNo).HasMaxLength(20).HasColumnName("pan_no");
            entity.Property(e => e.Mobile).HasMaxLength(20).HasColumnName("mobile");
            entity.Property(e => e.Phone).HasMaxLength(30).HasColumnName("phone");
            entity.Property(e => e.Email).HasMaxLength(100).HasColumnName("email");
            entity.Property(e => e.Address).HasMaxLength(300).HasColumnName("address");
            entity.Property(e => e.City).HasMaxLength(100).HasColumnName("city");
            entity.Property(e => e.State).HasMaxLength(100).HasColumnName("state");
            entity.Property(e => e.Pincode).HasMaxLength(20).HasColumnName("pincode");
            entity.Property(e => e.PartyType).HasConversion<int>().HasColumnName("party_type");
            entity.Property(e => e.DefaultPaymentTerm).HasConversion<int>().HasColumnName("default_payment_term");
            entity.Property(e => e.CreditLimit).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("credit_limit");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_parties_tenant");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoices_pkey");
            entity.ToTable("invoices");

            entity.HasIndex(e => new { e.TenantId, e.InvoiceNo }, "invoices_tenant_invoice_no_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.InvoiceNo).HasMaxLength(50).HasColumnName("invoice_no");
            entity.Property(e => e.InvoiceDate).HasColumnName("invoice_date");
            entity.Property(e => e.DueDate).HasColumnName("due_date");
            entity.Property(e => e.PartyId).HasColumnName("party_id");
            entity.Property(e => e.PartyName).HasMaxLength(150).HasColumnName("party_name");
            entity.Property(e => e.PartyGstNo).HasMaxLength(30).HasColumnName("party_gst_no");
            entity.Property(e => e.PartyAddress).HasMaxLength(300).HasColumnName("party_address");
            entity.Property(e => e.SubTotal).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("sub_total");
            entity.Property(e => e.TaxRate).HasPrecision(6, 2).HasDefaultValue(0).HasColumnName("tax_rate");
            entity.Property(e => e.TaxAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("tax_amount");
            entity.Property(e => e.Discount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("discount");
            entity.Property(e => e.OtherCharges).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("other_charges");
            entity.Property(e => e.GrandTotal).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("grand_total");
            entity.Property(e => e.PaidAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("paid_amount");
            entity.Property(e => e.DueAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("due_amount");
            entity.Property(e => e.PaymentStatus).HasConversion<int>().HasColumnName("payment_status");
            entity.Property(e => e.PaymentMode).HasMaxLength(50).HasColumnName("payment_mode");
            entity.Property(e => e.Remarks).HasMaxLength(500).HasColumnName("remarks");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_invoices_tenant");

            entity.HasOne(d => d.Party).WithMany()
                .HasForeignKey(d => d.PartyId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_invoices_party");
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoice_items_pkey");
            entity.ToTable("invoice_items");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.ShipmentId).HasColumnName("shipment_id");
            entity.Property(e => e.ShipmentNo).HasMaxLength(50).HasColumnName("shipment_no");
            entity.Property(e => e.Description).HasMaxLength(300).HasColumnName("description");
            entity.Property(e => e.Quantity).HasPrecision(12, 2).HasDefaultValue(1).HasColumnName("quantity");
            entity.Property(e => e.Rate).HasPrecision(12, 2).HasDefaultValue(0).HasColumnName("rate");
            entity.Property(e => e.Amount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("amount");
            entity.Property(e => e.TaxRate).HasPrecision(6, 2).HasDefaultValue(0).HasColumnName("tax_rate");
            entity.Property(e => e.TaxAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("tax_amount");
            entity.Property(e => e.TotalAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("total_amount");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Invoice).WithMany(p => p.Items)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_invoice_items_invoice");

            entity.HasOne(d => d.Shipment).WithMany()
                .HasForeignKey(d => d.ShipmentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_invoice_items_shipment");
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vehicles_pkey");
            entity.ToTable("vehicles");

            entity.HasIndex(e => new { e.TenantId, e.VehicleNo }, "vehicles_tenant_vehicle_no_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.VehicleNo).HasMaxLength(50).HasColumnName("vehicle_no");
            entity.Property(e => e.VehicleType).HasMaxLength(100).HasColumnName("vehicle_type");
            entity.Property(e => e.OwnerType).HasMaxLength(50).HasColumnName("owner_type");
            entity.Property(e => e.CapacityTons).HasPrecision(10, 2).HasDefaultValue(0).HasColumnName("capacity_tons");
            entity.Property(e => e.EngineNo).HasMaxLength(100).HasColumnName("engine_no");
            entity.Property(e => e.ChassisNo).HasMaxLength(100).HasColumnName("chassis_no");
            entity.Property(e => e.FitnessValidUntil).HasColumnName("fitness_valid_until");
            entity.Property(e => e.InsuranceValidUntil).HasColumnName("insurance_valid_until");
            entity.Property(e => e.PermitValidUntil).HasColumnName("permit_valid_until");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_vehicles_tenant");
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("drivers_pkey");
            entity.ToTable("drivers");

            entity.HasIndex(e => new { e.TenantId, e.Mobile }, "drivers_tenant_mobile_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Name).HasMaxLength(150).HasColumnName("name");
            entity.Property(e => e.Mobile).HasMaxLength(20).HasColumnName("mobile");
            entity.Property(e => e.LicenseNo).HasMaxLength(50).HasColumnName("license_no");
            entity.Property(e => e.LicenseValidUntil).HasColumnName("license_valid_until");
            entity.Property(e => e.AadharNo).HasMaxLength(30).HasColumnName("aadhar_no");
            entity.Property(e => e.Address).HasMaxLength(300).HasColumnName("address");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_drivers_tenant");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("locations_pkey");
            entity.ToTable("locations");

            entity.HasIndex(e => new { e.TenantId, e.Code }, "locations_tenant_code_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Code).HasMaxLength(50).HasColumnName("code");
            entity.Property(e => e.Name).HasMaxLength(150).HasColumnName("name");
            entity.Property(e => e.City).HasMaxLength(100).HasColumnName("city");
            entity.Property(e => e.State).HasMaxLength(100).HasColumnName("state");
            entity.Property(e => e.Address).HasMaxLength(300).HasColumnName("address");
            entity.Property(e => e.Pincode).HasMaxLength(20).HasColumnName("pincode");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_locations_tenant");
        });

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("trips_pkey");
            entity.ToTable("trips");

            entity.HasIndex(e => new { e.TenantId, e.TripNo }, "trips_tenant_trip_no_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.TripNo).HasMaxLength(50).HasColumnName("trip_no");
            entity.Property(e => e.TripDate).HasColumnName("trip_date");
            entity.Property(e => e.VehicleId).HasColumnName("vehicle_id");
            entity.Property(e => e.VehicleNo).HasMaxLength(50).HasColumnName("vehicle_no");
            entity.Property(e => e.DriverId).HasColumnName("driver_id");
            entity.Property(e => e.DriverName).HasMaxLength(150).HasColumnName("driver_name");
            entity.Property(e => e.DriverMobile).HasMaxLength(20).HasColumnName("driver_mobile");
            entity.Property(e => e.OriginLocationId).HasColumnName("origin_location_id");
            entity.Property(e => e.OriginLocationName).HasMaxLength(150).HasColumnName("origin_location_name");
            entity.Property(e => e.DestinationLocationId).HasColumnName("destination_location_id");
            entity.Property(e => e.DestinationLocationName).HasMaxLength(150).HasColumnName("destination_location_name");
            entity.Property(e => e.Status).HasConversion<int>().HasColumnName("status");
            entity.Property(e => e.DepartureTime).HasColumnType("timestamp without time zone").HasColumnName("departure_time");
            entity.Property(e => e.ArrivalTime).HasColumnType("timestamp without time zone").HasColumnName("arrival_time");
            entity.Property(e => e.StartOdometer).HasPrecision(12, 2).HasDefaultValue(0).HasColumnName("start_odometer");
            entity.Property(e => e.EndOdometer).HasPrecision(12, 2).HasDefaultValue(0).HasColumnName("end_odometer");
            entity.Property(e => e.SealNo).HasMaxLength(100).HasColumnName("seal_no");
            entity.Property(e => e.Remarks).HasMaxLength(500).HasColumnName("remarks");
            entity.Property(e => e.TotalWeightTons).HasPrecision(12, 3).HasDefaultValue(0).HasColumnName("total_weight_tons");
            entity.Property(e => e.TotalPackages).HasDefaultValue(0).HasColumnName("total_packages");
            entity.Property(e => e.TotalFreightRevenue).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("total_freight_revenue");
            entity.Property(e => e.DriverAdvanceCash).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("driver_advance_cash");
            entity.Property(e => e.DriverAdvanceFuel).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("driver_advance_fuel");
            entity.Property(e => e.TotalExpenses).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("total_expenses");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_trips_tenant");

            entity.HasOne(d => d.Vehicle).WithMany()
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_trips_vehicle");

            entity.HasOne(d => d.Driver).WithMany()
                .HasForeignKey(d => d.DriverId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_trips_driver");

            entity.HasOne(d => d.OriginLocation).WithMany()
                .HasForeignKey(d => d.OriginLocationId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_trips_origin");

            entity.HasOne(d => d.DestinationLocation).WithMany()
                .HasForeignKey(d => d.DestinationLocationId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_trips_dest");
        });

        modelBuilder.Entity<TripShipment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("trip_shipments_pkey");
            entity.ToTable("trip_shipments");

            entity.HasIndex(e => new { e.TenantId, e.TripId, e.ShipmentId }, "trip_shipments_tenant_trip_shipment_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.TripId).HasColumnName("trip_id");
            entity.Property(e => e.ShipmentId).HasColumnName("shipment_id");
            entity.Property(e => e.ShipmentNo).HasMaxLength(50).HasColumnName("shipment_no");
            entity.Property(e => e.LoadedWeight).HasPrecision(12, 3).HasDefaultValue(0).HasColumnName("loaded_weight");
            entity.Property(e => e.LoadedPackages).HasDefaultValue(0).HasColumnName("loaded_packages");
            entity.Property(e => e.FreightAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("freight_amount");
            entity.Property(e => e.LoadedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("loaded_at");
            entity.Property(e => e.UnloadedAt).HasColumnType("timestamp without time zone").HasColumnName("unloaded_at");
            entity.Property(e => e.Remarks).HasMaxLength(300).HasColumnName("remarks");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Trip).WithMany(p => p.Shipments)
                .HasForeignKey(d => d.TripId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_trip_shipments_trip");

            entity.HasOne(d => d.Shipment).WithMany()
                .HasForeignKey(d => d.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_trip_shipments_shipment");
        });

        modelBuilder.Entity<TripExpense>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("trip_expenses_pkey");
            entity.ToTable("trip_expenses");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.TripId).HasColumnName("trip_id");
            entity.Property(e => e.ExpenseType).HasConversion<int>().HasColumnName("expense_type");
            entity.Property(e => e.Amount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("amount");
            entity.Property(e => e.ReceiptNo).HasMaxLength(100).HasColumnName("receipt_no");
            entity.Property(e => e.PaymentMode).HasMaxLength(50).HasColumnName("payment_mode");
            entity.Property(e => e.PaidTo).HasMaxLength(150).HasColumnName("paid_to");
            entity.Property(e => e.Remarks).HasMaxLength(300).HasColumnName("remarks");
            entity.Property(e => e.ExpenseDate).HasColumnName("expense_date");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Trip).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.TripId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_trip_expenses_trip");
        });

        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vendors_pkey");
            entity.ToTable("vendors");

            entity.HasIndex(e => new { e.TenantId, e.Name }, "vendors_tenant_name_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Name).HasMaxLength(150).HasColumnName("name");
            entity.Property(e => e.Code).HasMaxLength(50).HasColumnName("code");
            entity.Property(e => e.PanNo).HasMaxLength(20).HasColumnName("pan_no");
            entity.Property(e => e.GstNo).HasMaxLength(30).HasColumnName("gst_no");
            entity.Property(e => e.ContactPerson).HasMaxLength(100).HasColumnName("contact_person");
            entity.Property(e => e.Mobile).HasMaxLength(20).HasColumnName("mobile");
            entity.Property(e => e.Phone).HasMaxLength(30).HasColumnName("phone");
            entity.Property(e => e.Email).HasMaxLength(100).HasColumnName("email");
            entity.Property(e => e.Address).HasMaxLength(300).HasColumnName("address");
            entity.Property(e => e.City).HasMaxLength(100).HasColumnName("city");
            entity.Property(e => e.State).HasMaxLength(100).HasColumnName("state");
            entity.Property(e => e.TdsPercentage).HasPrecision(5, 2).HasDefaultValue(1.0m).HasColumnName("tds_percentage");
            entity.Property(e => e.BankName).HasMaxLength(100).HasColumnName("bank_name");
            entity.Property(e => e.AccountNumber).HasMaxLength(50).HasColumnName("account_number");
            entity.Property(e => e.IfscCode).HasMaxLength(20).HasColumnName("ifsc_code");
            entity.Property(e => e.AccountHolderName).HasMaxLength(150).HasColumnName("account_holder_name");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_vendors_tenant");
        });

        modelBuilder.Entity<LorryHireContract>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("lorry_hire_contracts_pkey");
            entity.ToTable("lorry_hire_contracts");

            entity.HasIndex(e => new { e.TenantId, e.ContractNo }, "lorry_hire_tenant_contract_no_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ContractNo).HasMaxLength(50).HasColumnName("contract_no");
            entity.Property(e => e.ContractDate).HasColumnName("contract_date");
            entity.Property(e => e.TripId).HasColumnName("trip_id");
            entity.Property(e => e.VendorId).HasColumnName("vendor_id");
            entity.Property(e => e.VendorName).HasMaxLength(150).HasColumnName("vendor_name");
            entity.Property(e => e.VehicleNo).HasMaxLength(50).HasColumnName("vehicle_no");
            entity.Property(e => e.DriverName).HasMaxLength(150).HasColumnName("driver_name");
            entity.Property(e => e.DriverMobile).HasMaxLength(20).HasColumnName("driver_mobile");
            entity.Property(e => e.FromLocation).HasMaxLength(150).HasColumnName("from_location");
            entity.Property(e => e.ToLocation).HasMaxLength(150).HasColumnName("to_location");
            entity.Property(e => e.TotalHireAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("total_hire_amount");
            entity.Property(e => e.AdvanceCashPaid).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("advance_cash_paid");
            entity.Property(e => e.DieselAdvanceAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("diesel_advance_amount");
            entity.Property(e => e.TdsAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("tds_amount");
            entity.Property(e => e.OtherDeductions).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("other_deductions");
            entity.Property(e => e.BalancePayable).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("balance_payable");
            entity.Property(e => e.PaidBalanceAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("paid_balance_amount");
            entity.Property(e => e.PaymentStatus).HasMaxLength(50).HasDefaultValue("Unpaid").HasColumnName("payment_status");
            entity.Property(e => e.PaymentReference).HasMaxLength(100).HasColumnName("payment_reference");
            entity.Property(e => e.Remarks).HasMaxLength(500).HasColumnName("remarks");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_lorry_hire_tenant");

            entity.HasOne(d => d.Trip).WithMany()
                .HasForeignKey(d => d.TripId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_lorry_hire_trip");

            entity.HasOne(d => d.Vendor).WithMany()
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_lorry_hire_vendor");
        });

        modelBuilder.Entity<PodRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pod_records_pkey");
            entity.ToTable("pod_records");

            entity.HasIndex(e => new { e.TenantId, e.ShipmentId }, "pod_records_tenant_shipment_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ShipmentId).HasColumnName("shipment_id");
            entity.Property(e => e.ShipmentNo).HasMaxLength(50).HasColumnName("shipment_no");
            entity.Property(e => e.DeliveryDate).HasColumnName("delivery_date");
            entity.Property(e => e.ReceiverName).HasMaxLength(150).HasColumnName("receiver_name");
            entity.Property(e => e.ReceiverMobile).HasMaxLength(20).HasColumnName("receiver_mobile");
            entity.Property(e => e.ReceiverAadharOrId).HasMaxLength(50).HasColumnName("receiver_id");
            entity.Property(e => e.DocumentUrl).HasMaxLength(500).HasColumnName("document_url");
            entity.Property(e => e.SignatureUrl).HasMaxLength(500).HasColumnName("signature_url");
            entity.Property(e => e.Status).HasConversion<int>().HasColumnName("status");
            entity.Property(e => e.RejectionReason).HasMaxLength(300).HasColumnName("rejection_reason");
            entity.Property(e => e.Remarks).HasMaxLength(500).HasColumnName("remarks");
            entity.Property(e => e.VerifiedByUserId).HasColumnName("verified_by_user_id");
            entity.Property(e => e.VerifiedAt).HasColumnType("timestamp without time zone").HasColumnName("verified_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_pod_records_tenant");

            entity.HasOne(d => d.Shipment).WithMany()
                .HasForeignKey(d => d.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_pod_records_shipment");
        });

        modelBuilder.Entity<FreightRateCard>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("freight_rate_cards_pkey");
            entity.ToTable("freight_rate_cards");

            entity.HasIndex(e => new { e.TenantId, e.FromLocation, e.ToLocation, e.PartyId }, "rate_cards_tenant_route_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.PartyId).HasColumnName("party_id");
            entity.Property(e => e.PartyName).HasMaxLength(150).HasColumnName("party_name");
            entity.Property(e => e.FromLocation).HasMaxLength(150).HasColumnName("from_location");
            entity.Property(e => e.ToLocation).HasMaxLength(150).HasColumnName("to_location");
            entity.Property(e => e.CommodityType).HasMaxLength(100).HasColumnName("commodity_type");
            entity.Property(e => e.RateType).HasConversion<int>().HasColumnName("rate_type");
            entity.Property(e => e.BaseRate).HasPrecision(12, 2).HasDefaultValue(0).HasColumnName("base_rate");
            entity.Property(e => e.MinFreightAmount).HasPrecision(12, 2).HasDefaultValue(0).HasColumnName("min_freight_amount");
            entity.Property(e => e.HamaliRatePerKg).HasPrecision(10, 2).HasDefaultValue(0).HasColumnName("hamali_rate_per_kg");
            entity.Property(e => e.DoorDeliveryCharge).HasPrecision(12, 2).HasDefaultValue(0).HasColumnName("dd_charge");
            entity.Property(e => e.StationaryCharge).HasPrecision(10, 2).HasDefaultValue(0).HasColumnName("st_charge");
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.EffectiveTo).HasColumnName("effective_to");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_rate_cards_tenant");

            entity.HasOne(d => d.Party).WithMany()
                .HasForeignKey(d => d.PartyId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_rate_cards_party");
        });

        modelBuilder.Entity<VehicleMaintenance>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vehicle_maintenances_pkey");
            entity.ToTable("vehicle_maintenances");

            entity.HasIndex(e => new { e.TenantId, e.VehicleId }, "maintenance_tenant_vehicle_idx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.VehicleId).HasColumnName("vehicle_id");
            entity.Property(e => e.VehicleNo).HasMaxLength(50).HasColumnName("vehicle_no");
            entity.Property(e => e.MaintenanceType).HasConversion<int>().HasColumnName("maintenance_type");
            entity.Property(e => e.Cost).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("cost");
            entity.Property(e => e.ServiceDate).HasColumnName("service_date");
            entity.Property(e => e.OdometerReading).HasPrecision(12, 2).HasDefaultValue(0).HasColumnName("odometer_reading");
            entity.Property(e => e.NextServiceDueKm).HasPrecision(12, 2).HasColumnName("next_service_due_km");
            entity.Property(e => e.NextServiceDueDate).HasColumnName("next_service_due_date");
            entity.Property(e => e.WorkshopName).HasMaxLength(150).HasColumnName("workshop_name");
            entity.Property(e => e.InvoiceNo).HasMaxLength(50).HasColumnName("invoice_no");
            entity.Property(e => e.Description).HasMaxLength(300).HasColumnName("description");
            entity.Property(e => e.Remarks).HasMaxLength(500).HasColumnName("remarks");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_maintenance_tenant");

            entity.HasOne(d => d.Vehicle).WithMany()
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_maintenance_vehicle");
        });

        modelBuilder.Entity<CargoClaim>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cargo_claims_pkey");
            entity.ToTable("cargo_claims");

            entity.HasIndex(e => new { e.TenantId, e.ClaimNo }, "claims_tenant_claim_no_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasDefaultValue(TenantConstants.DefaultTenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ClaimNo).HasMaxLength(50).HasColumnName("claim_no");
            entity.Property(e => e.ShipmentId).HasColumnName("shipment_id");
            entity.Property(e => e.ShipmentNo).HasMaxLength(50).HasColumnName("shipment_no");
            entity.Property(e => e.PartyId).HasColumnName("party_id");
            entity.Property(e => e.PartyName).HasMaxLength(150).HasColumnName("party_name");
            entity.Property(e => e.ClaimType).HasConversion<int>().HasColumnName("claim_type");
            entity.Property(e => e.ClaimAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("claim_amount");
            entity.Property(e => e.SettledAmount).HasPrecision(14, 2).HasDefaultValue(0).HasColumnName("settled_amount");
            entity.Property(e => e.Status).HasConversion<int>().HasColumnName("status");
            entity.Property(e => e.ClaimDate).HasColumnName("claim_date");
            entity.Property(e => e.SettledDate).HasColumnName("settled_date");
            entity.Property(e => e.Description).HasMaxLength(500).HasColumnName("description");
            entity.Property(e => e.InvestigationNotes).HasMaxLength(1000).HasColumnName("investigation_notes");
            entity.Property(e => e.SettlementRemarks).HasMaxLength(500).HasColumnName("settlement_remarks");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasQueryFilter(e => _tenantContext == null || !_tenantContext.HasTenant || e.TenantId == _tenantContext.CurrentTenantId);

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("fk_claims_tenant");

            entity.HasOne(d => d.Shipment).WithMany()
                .HasForeignKey(d => d.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_claims_shipment");

            entity.HasOne(d => d.Party).WithMany()
                .HasForeignKey(d => d.PartyId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_claims_party");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
