using KTransport.API.Data;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

// Enable Npgsql legacy timestamp behavior for compatibility with 'timestamp without time zone' columns
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure JWT Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();

// Add HttpContextAccessor and Multi-Tenancy Context
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();

// Add DbContext
builder.Services.AddDbContext<KTransportDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.Secret ?? "")),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings?.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings?.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Register Services
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IGstBillService, GstBillService>();
builder.Services.AddScoped<IChargeService, ChargeService>();
builder.Services.AddScoped<IGoodsDetailService, GoodsDetailService>();
builder.Services.AddScoped<INumberingSequenceService, NumberingSequenceService>();
builder.Services.AddScoped<IShipmentService, ShipmentService>();
builder.Services.AddScoped<IManifestService, ManifestService>();
builder.Services.AddScoped<IPartyService, PartyService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IFleetService, FleetService>();
builder.Services.AddScoped<ITripService, TripService>();
builder.Services.AddScoped<IVendorService, VendorService>();
builder.Services.AddScoped<IPodService, PodService>();
builder.Services.AddScoped<IRateCardService, RateCardService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<ICargoClaimService, CargoClaimService>();
builder.Services.AddScoped<ITrackingService, TrackingService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IChallanService, ChallanService>();
builder.Services.AddScoped<ITenantConfigurationService, TenantConfigurationService>();
builder.Services.AddScoped<INavigationService, NavigationService>();
builder.Services.AddScoped<IMoneyReceiptService, MoneyReceiptService>();
builder.Services.AddScoped<IFeatureAuthorizationService, FeatureAuthorizationService>();
builder.Services.AddScoped<IUserService, UserService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KTransport API",
        Version = "v1",
        Description = "KTransport Management System API"
    });

    // Define the Bearer auth scheme
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "KTransport API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("AllowReactApp");

app.MapControllers();

// Automatically apply pending database migrations and seed data on startup with retry resilience
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var maxRetries = 10;
    var delay = TimeSpan.FromSeconds(3);

    for (int retry = 1; retry <= maxRetries; retry++)
    {
        try
        {
            logger.LogInformation("Attempting database migration and schema sync (Attempt {Retry}/{MaxRetries})...", retry, maxRetries);
            var dbContext = services.GetRequiredService<KTransportDbContext>();

            if (dbContext.Database.IsRelational())
            {
                // Baseline existing tables in __EFMigrationsHistory if database pre-existed
                dbContext.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                        ""MigrationId"" character varying(150) NOT NULL,
                        ""ProductVersion"" character varying(32) NOT NULL,
                        CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
                    );
                    DO $$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BillType') THEN
                            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                            VALUES ('20260822172624_InitialCreate', '8.0.0')
                            ON CONFLICT DO NOTHING;

                            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                            VALUES ('20260822173252_SeedAdminUser', '8.0.0')
                            ON CONFLICT DO NOTHING;
                        END IF;
                    END $$;
                ");

                // Ensure SaaS Configuration & Subscription tables exist
                dbContext.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS subscription_plans (
                        id uuid PRIMARY KEY,
                        name character varying(100) NOT NULL,
                        tier character varying(50) NOT NULL,
                        description character varying(500) NOT NULL,
                        max_vehicles integer NOT NULL,
                        max_users integer NOT NULL,
                        max_monthly_shipments integer NOT NULL,
                        storage_limit_mb integer NOT NULL,
                        monthly_price numeric(14,2) NOT NULL,
                        is_active boolean DEFAULT true,
                        created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS tenant_settings (
                        id uuid PRIMARY KEY,
                        tenant_id uuid NOT NULL UNIQUE REFERENCES tenants(id) ON DELETE CASCADE,
                        general_json text NOT NULL,
                        billing_and_tax_json text NOT NULL,
                        document_sequences_json text NOT NULL,
                        operational_workflows_json text NOT NULL,
                        feature_flags_json text NOT NULL,
                        integrations_json text NOT NULL,
                        custom_settings_json text NOT NULL,
                        created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
                        updated_at timestamp without time zone
                    );

                    ALTER TABLE tenant_settings ADD COLUMN IF NOT EXISTS menu_entitlements_json text NULL;

                    CREATE TABLE IF NOT EXISTS tenant_subscriptions (
                        id uuid PRIMARY KEY,
                        tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                        subscription_plan_id uuid NOT NULL REFERENCES subscription_plans(id) ON DELETE CASCADE,
                        status character varying(50) NOT NULL DEFAULT 'Active',
                        started_at timestamp without time zone NOT NULL,
                        expires_at timestamp without time zone,
                        is_auto_renew boolean DEFAULT true,
                        created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
                        updated_at timestamp without time zone
                    );

                    -- Ensure Indian Road Transport / GTA Manifests and Multi-Invoice tables exist
                    ALTER TABLE shipments ADD COLUMN IF NOT EXISTS origin_hub_id bigint NULL;
                    ALTER TABLE shipments ADD COLUMN IF NOT EXISTS destination_hub_id bigint NULL;
                    ALTER TABLE shipments ADD COLUMN IF NOT EXISTS current_hub_id bigint NULL;
                    ALTER TABLE shipments ADD COLUMN IF NOT EXISTS delivery_type character varying(50) NULL;
                    ALTER TABLE shipments ADD COLUMN IF NOT EXISTS eway_bill_no character varying(50) NULL;
                    ALTER TABLE shipments ADD COLUMN IF NOT EXISTS eway_bill_valid_upto timestamp without time zone NULL;
                    ALTER TABLE shipments ADD COLUMN IF NOT EXISTS invoice_id bigint NULL;
                    ALTER TABLE shipments ADD COLUMN IF NOT EXISTS consignor_party_id bigint NULL;
                    ALTER TABLE shipments ADD COLUMN IF NOT EXISTS consignee_party_id bigint NULL;
                    ALTER TABLE shipments ADD COLUMN IF NOT EXISTS paid_amount numeric(14,2) DEFAULT 0;
                    ALTER TABLE shipments ADD COLUMN IF NOT EXISTS due_amount numeric(14,2) DEFAULT 0;

                    ALTER TABLE invoices ADD COLUMN IF NOT EXISTS payment_mode character varying(50) NULL;
                    ALTER TABLE invoices ADD COLUMN IF NOT EXISTS party_id bigint NULL;
                    ALTER TABLE invoices ADD COLUMN IF NOT EXISTS party_name character varying(150) NULL;
                    ALTER TABLE invoices ADD COLUMN IF NOT EXISTS party_gst_no character varying(30) NULL;
                    ALTER TABLE invoices ADD COLUMN IF NOT EXISTS party_address character varying(300) NULL;
                    ALTER TABLE invoices ADD COLUMN IF NOT EXISTS paid_amount numeric(14,2) DEFAULT 0;
                    ALTER TABLE invoices ADD COLUMN IF NOT EXISTS due_amount numeric(14,2) DEFAULT 0;

                    ALTER TABLE users ADD COLUMN IF NOT EXISTS mobile character varying(20) NULL;
                    ALTER TABLE users ADD COLUMN IF NOT EXISTS tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001';

                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS origin_location_id bigint NULL;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS origin_location_name character varying(150) NULL;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS destination_location_id bigint NULL;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS destination_location_name character varying(150) NULL;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS driver_id bigint NULL;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS driver_name character varying(150) NULL;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS driver_mobile character varying(20) NULL;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS vehicle_id bigint NULL;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS vehicle_no character varying(50) NULL;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS total_weight_tons numeric(12,3) DEFAULT 0;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS total_packages integer DEFAULT 0;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS total_freight_revenue numeric(14,2) DEFAULT 0;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS driver_advance_cash numeric(14,2) DEFAULT 0;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS driver_advance_fuel numeric(14,2) DEFAULT 0;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS total_expenses numeric(14,2) DEFAULT 0;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS start_odometer numeric(12,2) DEFAULT 0;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS end_odometer numeric(12,2) DEFAULT 0;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS departure_time timestamp without time zone NULL;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS arrival_time timestamp without time zone NULL;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS seal_no character varying(100) NULL;
                    ALTER TABLE trips ADD COLUMN IF NOT EXISTS remarks character varying(500) NULL;

                    CREATE TABLE IF NOT EXISTS consignment_invoice_references (
                        id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                        tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
                        shipment_id bigint NOT NULL,
                        customer_invoice_no character varying(50) NOT NULL,
                        customer_invoice_date date NOT NULL DEFAULT CURRENT_DATE,
                        declared_goods_value numeric(14,2) NOT NULL DEFAULT 0,
                        eway_bill_no character varying(50) NULL,
                        eway_bill_date date NULL,
                        eway_bill_valid_upto timestamp without time zone NULL,
                        document_type character varying(50) NULL DEFAULT 'TaxInvoice',
                        package_count integer NULL,
                        weight_kg numeric(12,3) NULL,
                        commodity_description character varying(250) NULL,
                        is_active boolean NOT NULL DEFAULT true,
                        created_by integer NULL,
                        created_at timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        CONSTRAINT fk_consignment_invoices_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE,
                        CONSTRAINT fk_consignment_invoices_shipment FOREIGN KEY (shipment_id) REFERENCES shipments (id) ON DELETE CASCADE
                    );

                    CREATE INDEX IF NOT EXISTS consignment_invoices_tenant_shipment_inv_idx 
                        ON consignment_invoice_references (tenant_id, shipment_id, customer_invoice_no);

                    CREATE TABLE IF NOT EXISTS manifests (
                        id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                        tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
                        manifest_no character varying(50) NOT NULL,
                        manifest_date date NOT NULL DEFAULT CURRENT_DATE,
                        origin_hub_id bigint NULL,
                        destination_hub_id bigint NULL,
                        trip_id bigint NULL,
                        consolidated_eway_bill_no character varying(50) NULL,
                        consolidated_eway_bill_date timestamp without time zone NULL,
                        seal_no character varying(100) NULL,
                        loading_supervisor_name character varying(150) NULL,
                        remarks character varying(500) NULL,
                        status integer NOT NULL DEFAULT 0,
                        total_consignments integer NOT NULL DEFAULT 0,
                        total_packages integer NOT NULL DEFAULT 0,
                        total_weight_kg numeric(12,3) NOT NULL DEFAULT 0,
                        is_active boolean NOT NULL DEFAULT true,
                        created_by integer NULL,
                        updated_by integer NULL,
                        created_at timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        updated_at timestamp without time zone NULL,
                        CONSTRAINT fk_manifests_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE,
                        CONSTRAINT fk_manifests_origin_hub FOREIGN KEY (origin_hub_id) REFERENCES locations (id) ON DELETE SET NULL,
                        CONSTRAINT fk_manifests_dest_hub FOREIGN KEY (destination_hub_id) REFERENCES locations (id) ON DELETE SET NULL,
                        CONSTRAINT fk_manifests_trip FOREIGN KEY (trip_id) REFERENCES trips (id) ON DELETE SET NULL
                    );

                    CREATE UNIQUE INDEX IF NOT EXISTS manifests_tenant_manifest_no_key 
                        ON manifests (tenant_id, manifest_no);

                    CREATE TABLE IF NOT EXISTS manifest_items (
                        id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                        tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
                        manifest_id bigint NOT NULL,
                        shipment_id bigint NOT NULL,
                        target_destination_hub_id bigint NULL,
                        loaded_packages integer NOT NULL DEFAULT 0,
                        loaded_weight_kg numeric(12,3) NOT NULL DEFAULT 0,
                        unloading_status integer NOT NULL DEFAULT 0,
                        received_packages integer NULL,
                        shortage_packages integer NULL,
                        damaged_packages integer NULL,
                        unloaded_at_hub_id bigint NULL,
                        unloaded_date timestamp without time zone NULL,
                        discrepancy_remarks character varying(500) NULL,
                        is_active boolean NOT NULL DEFAULT true,
                        created_at timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        CONSTRAINT fk_manifest_items_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE CASCADE,
                        CONSTRAINT fk_manifest_items_manifest FOREIGN KEY (manifest_id) REFERENCES manifests (id) ON DELETE CASCADE,
                        CONSTRAINT fk_manifest_items_shipment FOREIGN KEY (shipment_id) REFERENCES shipments (id) ON DELETE CASCADE,
                        CONSTRAINT fk_manifest_items_target_hub FOREIGN KEY (target_destination_hub_id) REFERENCES locations (id) ON DELETE SET NULL,
                        CONSTRAINT fk_manifest_items_unloaded_hub FOREIGN KEY (unloaded_at_hub_id) REFERENCES locations (id) ON DELETE SET NULL
                    );

                    CREATE INDEX IF NOT EXISTS manifest_items_tenant_manifest_shipment_idx 
                        ON manifest_items (tenant_id, manifest_id, shipment_id);

                    -- Backfill single invoice shipments into consignment_invoice_references
                    INSERT INTO consignment_invoice_references (
                        tenant_id, 
                        shipment_id, 
                        customer_invoice_no, 
                        customer_invoice_date, 
                        declared_goods_value, 
                        document_type, 
                        is_active, 
                        created_at
                    )
                    SELECT 
                        s.tenant_id,
                        s.id,
                        s.invoice_no,
                        COALESCE(s.invoice_date, s.shipment_date, CURRENT_DATE),
                        COALESCE(s.goods_value, 0),
                        'TaxInvoice',
                        true,
                        CURRENT_TIMESTAMP
                    FROM shipments s
                    WHERE s.invoice_no IS NOT NULL 
                      AND TRIM(s.invoice_no) <> ''
                      AND NOT EXISTS (
                          SELECT 1 FROM consignment_invoice_references cir 
                          WHERE cir.shipment_id = s.id AND cir.customer_invoice_no = s.invoice_no
                      );
                ");

                dbContext.Database.Migrate();
                logger.LogInformation("Database migrations applied successfully.");
            }

            // Seed default tenant if not already present
            if (!dbContext.Tenants.Any(t => t.Id == TenantContext.DefaultTenantId))
            {
                dbContext.Tenants.Add(new Tenant
                {
                    Id = TenantContext.DefaultTenantId,
                    Name = "Default Organization",
                    Code = "DEFAULT",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                dbContext.SaveChanges();
            }

            // Seed default subscription plans if not present
            if (!dbContext.SubscriptionPlans.Any())
            {
                var enterprisePlanId = Guid.Parse("33333333-3333-3333-3333-333333333333");
                dbContext.SubscriptionPlans.AddRange(
                    new SubscriptionPlan
                    {
                        Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                        Name = "Starter Tier",
                        Tier = "Starter",
                        Description = "Single branch transport company up to 10 vehicles",
                        MaxVehicles = 10,
                        MaxUsers = 3,
                        MaxMonthlyShipments = 150,
                        MonthlyPrice = 999m,
                        IsActive = true
                    },
                    new SubscriptionPlan
                    {
                        Id = Guid.Parse("22222222-2222-3333-4444-555555555555"),
                        Name = "Professional Tier",
                        Tier = "Professional",
                        Description = "Regional transport fleet up to 50 vehicles with GPS telematics",
                        MaxVehicles = 50,
                        MaxUsers = 15,
                        MaxMonthlyShipments = 1000,
                        MonthlyPrice = 3999m,
                        IsActive = true
                    },
                    new SubscriptionPlan
                    {
                        Id = enterprisePlanId,
                        Name = "Enterprise Dedicated Fleet Tier",
                        Tier = "Enterprise",
                        Description = "Full scale national logistics operations with unlimited scalability",
                        MaxVehicles = 500,
                        MaxUsers = 100,
                        MaxMonthlyShipments = 10000,
                        MonthlyPrice = 9999m,
                        IsActive = true
                    }
                );
                dbContext.SaveChanges();

                if (!dbContext.TenantSubscriptions.Any(s => s.TenantId == TenantContext.DefaultTenantId))
                {
                    dbContext.TenantSubscriptions.Add(new TenantSubscription
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantContext.DefaultTenantId,
                        SubscriptionPlanId = enterprisePlanId,
                        Status = "Active",
                        StartedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddYears(5)
                    });
                    dbContext.SaveChanges();
                }
            }

            // Seed default admin user if not already present
            if (!dbContext.Users.Any(u => u.Username == "admin"))
            {
                dbContext.Users.Add(new User
                {
                    TenantId = TenantContext.DefaultTenantId,
                    Username = "admin",
                    Password = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    FullName = "Kundan Kumar",
                    Role = "admin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Mobile = "9504600060"
                });
                dbContext.SaveChanges();
            }

            logger.LogInformation("Database startup initialization completed successfully.");
            break; // Migration and seeding succeeded, exit retry loop
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database migration/seeding attempt {Retry} failed. Waiting {Delay}s before retry...", retry, delay.TotalSeconds);
            if (retry == maxRetries)
            {
                logger.LogError(ex, "FATAL: Database migration and seeding failed after {MaxRetries} attempts.", maxRetries);
            }
            else
            {
                Thread.Sleep(delay);
            }
        }
    }
}

app.Run();

