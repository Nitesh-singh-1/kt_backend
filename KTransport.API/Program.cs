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
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",  // Vite default
                "https://yourdomain.com"   // Production
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("AllowReactApp");

app.MapControllers();

// Automatically apply pending database migrations and seed data on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
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
            ");

            dbContext.Database.Migrate();
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
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while applying database migrations or seeding.");
    }
}

app.Run();
