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
                // 1. Baseline existing tables in __EFMigrationsHistory ONLY if database pre-existed before EF Core migrations
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

                // 2. Apply all EF Core migrations in sequence (creates tenants, shipments, invoices, manifests, subscription_plans, etc.)
                dbContext.Database.Migrate();
                logger.LogInformation("Database migrations applied successfully via EF Core.");

                // 3. Apply post-migration safety patches for any dynamic columns
                dbContext.Database.ExecuteSqlRaw(@"
                    DO $$
                    BEGIN
                        -- Safely patch tenant_settings if table exists
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'tenant_settings') THEN
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'tenant_settings' AND column_name = 'menu_entitlements_json') THEN
                                ALTER TABLE tenant_settings ADD COLUMN menu_entitlements_json text NOT NULL DEFAULT '{}';
                            END IF;
                        END IF;

                        -- Safely patch invoices if table exists
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'invoices') THEN
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'invoices' AND column_name = 'payment_mode') THEN
                                ALTER TABLE invoices ADD COLUMN payment_mode character varying(50) NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'invoices' AND column_name = 'party_id') THEN
                                ALTER TABLE invoices ADD COLUMN party_id bigint NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'invoices' AND column_name = 'party_name') THEN
                                ALTER TABLE invoices ADD COLUMN party_name character varying(150) NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'invoices' AND column_name = 'party_gst_no') THEN
                                ALTER TABLE invoices ADD COLUMN party_gst_no character varying(30) NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'invoices' AND column_name = 'party_address') THEN
                                ALTER TABLE invoices ADD COLUMN party_address character varying(300) NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'invoices' AND column_name = 'paid_amount') THEN
                                ALTER TABLE invoices ADD COLUMN paid_amount numeric(14,2) DEFAULT 0;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'invoices' AND column_name = 'due_amount') THEN
                                ALTER TABLE invoices ADD COLUMN due_amount numeric(14,2) DEFAULT 0;
                            END IF;
                        END IF;

                        -- Safely patch shipments if table exists
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'shipments') THEN
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'origin_hub_id') THEN
                                ALTER TABLE shipments ADD COLUMN origin_hub_id bigint NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'destination_hub_id') THEN
                                ALTER TABLE shipments ADD COLUMN destination_hub_id bigint NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'current_hub_id') THEN
                                ALTER TABLE shipments ADD COLUMN current_hub_id bigint NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'delivery_type') THEN
                                ALTER TABLE shipments ADD COLUMN delivery_type character varying(50) NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'eway_bill_no') THEN
                                ALTER TABLE shipments ADD COLUMN eway_bill_no character varying(50) NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'eway_bill_valid_upto') THEN
                                ALTER TABLE shipments ADD COLUMN eway_bill_valid_upto timestamp without time zone NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'invoice_id') THEN
                                ALTER TABLE shipments ADD COLUMN invoice_id bigint NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'consignor_party_id') THEN
                                ALTER TABLE shipments ADD COLUMN consignor_party_id bigint NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'consignee_party_id') THEN
                                ALTER TABLE shipments ADD COLUMN consignee_party_id bigint NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'paid_amount') THEN
                                ALTER TABLE shipments ADD COLUMN paid_amount numeric(14,2) DEFAULT 0;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'due_amount') THEN
                                ALTER TABLE shipments ADD COLUMN due_amount numeric(14,2) DEFAULT 0;
                            END IF;
                        END IF;

                        -- Safely patch trips if table exists
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'trips') THEN
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'trips' AND column_name = 'origin_location_name') THEN
                                ALTER TABLE trips ADD COLUMN origin_location_name character varying(150) NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'trips' AND column_name = 'destination_location_name') THEN
                                ALTER TABLE trips ADD COLUMN destination_location_name character varying(150) NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'trips' AND column_name = 'driver_name') THEN
                                ALTER TABLE trips ADD COLUMN driver_name character varying(150) NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'trips' AND column_name = 'driver_mobile') THEN
                                ALTER TABLE trips ADD COLUMN driver_mobile character varying(20) NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'trips' AND column_name = 'vehicle_no') THEN
                                ALTER TABLE trips ADD COLUMN vehicle_no character varying(50) NULL;
                            END IF;
                        END IF;
                    END $$;
                ");
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

