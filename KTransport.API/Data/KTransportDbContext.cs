using System;
using System.Collections.Generic;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Data;

public partial class KTransportDbContext : DbContext
{
    public KTransportDbContext()
    {
    }

    public KTransportDbContext(DbContextOptions<KTransportDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BillType> BillTypes { get; set; }
    public virtual DbSet<Charge> Charges { get; set; }
    public virtual DbSet<Challan> Challans { get; set; }
    public virtual DbSet<ChallanDetail> ChallanDetails { get; set; }

    public virtual DbSet<GoodsDetail> GoodsDetails { get; set; }
    public virtual DbSet<GstBill> GstBills { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<WithoutGstBill> WithoutGstBills { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Charge>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("charges_pkey");

            entity.ToTable("charges");

            entity.HasIndex(e => e.BillId, "charges_bill_id_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
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

            entity.HasOne(d => d.Bill).WithOne(p => p.Charge)
                .HasForeignKey<Charge>(d => d.BillId)
                .HasConstraintName("fk_charges_bill");
        });

        modelBuilder.Entity<GoodsDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("goods_details_pkey");

            entity.ToTable("goods_details");

            entity.Property(e => e.Id).HasColumnName("id");
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

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.GstBillCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.GstBillUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_updated_by");
            entity.Property(d => d.Consigneeraddress).HasMaxLength(200).HasColumnName("consigneeraddress");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
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
        });

        modelBuilder.Entity<WithoutGstBill>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("without_gst_bills_pkey");

            entity.ToTable("without_gst_bills");

            entity.HasIndex(e => e.GrNo, "without_gst_bills_gr_no_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
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

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
