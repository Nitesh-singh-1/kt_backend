using KTransport.API.Data;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

// Enable Npgsql legacy timestamp behavior for compatibility with 'timestamp without time zone' columns
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure JWT Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();

// Fail fast if critical secrets are missing rather than silently starting with an insecure/empty signing key.
// Values must come from appsettings.{Environment}.json, environment variables (ConnectionStrings__DefaultConnection,
// JwtSettings__Secret), or `dotnet user-secrets` for local development.
if (string.IsNullOrWhiteSpace(jwtSettings?.Secret))
{
    throw new InvalidOperationException(
        "JwtSettings:Secret is not configured. Set it via appsettings.{Environment}.json, the JwtSettings__Secret " +
        "environment variable, or 'dotnet user-secrets set JwtSettings:Secret \"...\"' for local development.");
}

if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Set it via appsettings.{Environment}.json, the " +
        "ConnectionStrings__DefaultConnection environment variable, or 'dotnet user-secrets' for local development.");
}

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
builder.Services.AddScoped<IQuotationService, QuotationService>();
builder.Services.AddScoped<IVendorRateContractService, VendorRateContractService>();
builder.Services.AddScoped<ITyreService, TyreService>();
builder.Services.AddScoped<ISparePartService, SparePartService>();
builder.Services.AddScoped<IVehicleLoanService, VehicleLoanService>();
builder.Services.AddScoped<IEmptyTripLogService, EmptyTripLogService>();
builder.Services.AddScoped<IDriverLedgerService, DriverLedgerService>();
builder.Services.AddScoped<IVehicleInsuranceClaimService, VehicleInsuranceClaimService>();
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
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IVerificationCodeService, VerificationCodeService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();

// Email / notification infrastructure
builder.Services.Configure<KTransport.API.Services.Email.EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<KTransport.API.Services.Email.IEmailSender, KTransport.API.Services.Email.SmtpEmailSender>();

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

// Allowed browser origins come from configuration (Cors:AllowedOrigins), never a wildcard-all policy.
// Note: the Electron desktop build disables webSecurity and is not subject to browser CORS at all —
// this policy only matters for the web frontend accessed directly through a browser.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            // No origins configured: reject all cross-origin browser requests rather than allowing everything.
            policy.WithOrigins(Array.Empty<string>());
        }
    });
});

// Basic abuse protection on auth/onboarding endpoints (brute force, credential stuffing, bot signups).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("AuthPolicy", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

var app = builder.Build();

// Consistent JSON error envelope for any unhandled exception (must be first to wrap everything).
app.UseMiddleware<KTransport.API.Middleware.ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "KTransport API v1");
    c.RoutePrefix = "swagger";
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors("AllowReactApp");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

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
                // 0. Ensure the database itself exists. Migrate() creates tables (not the database),
                //    and the baseline SQL below needs a connectable database — so on a brand-new
                //    deployment we create the empty database first, then baseline + migrate.
                var dbCreator = dbContext.Database.GetService<IRelationalDatabaseCreator>();
                if (!dbCreator.Exists())
                {
                    dbCreator.Create();
                    logger.LogInformation("Created new empty database for first-time initialization.");
                }

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

                // NOTE: the previous ad-hoc "ALTER TABLE ... IF NOT EXISTS" startup patch block was
                // removed here. Every column it added is already created by the EF migrations above, so
                // it was redundant — and its `DEFAULT '{}'` literal was parsed as a String.Format
                // placeholder by ExecuteSqlRaw, throwing FormatException on every boot and preventing
                // a fresh database from being seeded. Schema now comes solely from EF migrations.
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

            // Seed the default admin user, or repair its login on a fresh DB.
            // The InitialCreate migration seeds an admin via HasData with a stale placeholder password
            // hash that does NOT actually verify against "admin123". On a fresh database that row is
            // created by Migrate() before this block runs, so we detect the untouched stale hash and
            // reset it to a real BCrypt hash of "admin123". An admin whose password was already changed
            // (any other hash) is left untouched.
            const string StaleSeedAdminHash = "$2a$11$0aBw4j5tM1Ew2k5hF8O/TehI5jY9K2HkZ0K6o0tM6dYp/1R9Gq8m6";
            var seededAdmin = dbContext.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Username == "admin");
            if (seededAdmin == null)
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
            else if (seededAdmin.Password == StaleSeedAdminHash)
            {
                seededAdmin.Password = BCrypt.Net.BCrypt.HashPassword("admin123");
                dbContext.SaveChanges();
                logger.LogInformation("Repaired default admin login on first-time initialization.");
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

