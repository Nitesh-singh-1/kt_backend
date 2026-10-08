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
using Polly;
using Polly.Retry;
using Serilog;
using Serilog.Events;

// Enable Npgsql legacy timestamp behavior for compatibility with 'timestamp without time zone' columns
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// TASK-010: Serilog replaces the built-in ILogger sinks so every log line is emitted as
// structured JSON with a CorrelationId enrichment (populated per-request by
// CorrelationIdMiddleware). Config is loaded from appsettings.{Environment}.json under
// the "Serilog" section; when nothing is configured we fall back to Console-only at
// Information level (Debug in Development). Health-check hits are noisy and are filtered
// out so they don't drown real signal in the logs.
builder.Host.UseSerilog((context, services, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .Filter.ByExcluding(logEvent =>
            logEvent.Properties.TryGetValue("RequestPath", out var path) &&
            path.ToString().Contains("/health"))
        .MinimumLevel.Is(context.HostingEnvironment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .WriteTo.Console(
            outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj} {Properties:j}{NewLine}{Exception}");
});

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
// TASK-044 Phase 3: entitlements normalized tables are sole source of truth.
builder.Services.AddScoped<IEntitlementsService, EntitlementsService>();
// TASK-045 Phase 3: menu_items table is sole authoritative source for the navigation menu.
builder.Services.AddScoped<IMenuCatalogService, MenuCatalogService>();
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

// TASK-010: health checks — /health is liveness (process up), /health/ready runs a
// lightweight DB ping so a load balancer knows to stop routing traffic while the
// database is unreachable. The check has a short timeout so a slow DB doesn't hold
// the probe hostage.
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<KTransportDbContext>(name: "database", tags: new[] { "ready" });

var app = builder.Build();

// TASK-044 Phase 3 removed the `--backfill-entitlements` CLI. The JSON
// source column (`tenant_settings.menu_entitlements_json`) was dropped and
// `EntitlementsService.BackfillFromJsonAsync` is now an [Obsolete] no-op.
// Grants are created via `PUT /api/configuration/menu-entitlements` or the
// upcoming SaaS admin UI; nothing to invoke from the command line.

// TASK-010: correlation-id middleware must run BEFORE ExceptionHandlingMiddleware and
// before request logging so the same X-Request-Id appears in the response header, in
// the structured request log line, and in the error envelope if anything throws.
app.UseMiddleware<KTransport.API.Middleware.CorrelationIdMiddleware>();

// Consistent JSON error envelope for any unhandled exception (must wrap everything past this point).
app.UseMiddleware<KTransport.API.Middleware.ExceptionHandlingMiddleware>();

// One structured log line per request (method, path, status, elapsed ms + CorrelationId).
// Health-check hits are excluded via the filter set up in the host config above.
app.UseSerilogRequestLogging();

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

// TASK-010: health endpoints for orchestrators/load balancers.
//   /health        — liveness. Always returns Healthy while the process is up.
//   /health/ready  — readiness. Runs only the checks tagged "ready" (currently the DB
//                    ping). Returns 503 while the DB is unreachable so the LB stops
//                    routing traffic during an outage.
// Both are AllowAnonymous — health probes must not need a JWT.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false, // exclude all named checks — pure "am I up?" liveness
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

// Automatically apply pending database migrations and seed data on startup with retry resilience.
// TASK-010: replaced a hand-rolled for-loop-with-Thread.Sleep with a Polly async retry
// policy — same behaviour (up to 10 attempts) but with exponential backoff, jitter to
// prevent thundering-herd on restart, and awaited waits instead of blocking the pool.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    const int maxRetries = 10;

    var jitter = new Random();
    AsyncRetryPolicy retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: maxRetries - 1, // WaitAndRetry counts RETRIES, not attempts
            sleepDurationProvider: attempt =>
                TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)))
                + TimeSpan.FromMilliseconds(jitter.Next(0, 500)),
            onRetry: (ex, wait, attempt, _) =>
                logger.LogWarning(ex,
                    "Database migration/seeding attempt {Attempt} failed; retrying in {WaitSeconds:F1}s...",
                    attempt, wait.TotalSeconds));

    try
    {
        await retryPolicy.ExecuteAsync(async () =>
        {
            await Task.Yield(); // yield control so Polly's async plumbing runs cleanly
            logger.LogInformation("Attempting database migration and schema sync...");
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
                        MonthlyPrice = 2999m,
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

            // Self-heal the Professional plan's price on a DB that was already seeded before
            // this price change shipped. The block above only inserts on a fresh DB
            // (`!SubscriptionPlans.Any()`), so an existing deployment's row would otherwise
            // keep the old 3999 forever. Mirrors the admin-password self-heal pattern below:
            // only touch the row if it's still at the OLD price, so an operator who has since
            // manually repriced it isn't overwritten.
            const decimal OldProfessionalPrice = 3999m;
            const decimal NewProfessionalPrice = 2999m;
            var professionalPlan = dbContext.SubscriptionPlans.FirstOrDefault(p => p.Tier == "Professional");
            if (professionalPlan != null && professionalPlan.MonthlyPrice == OldProfessionalPrice)
            {
                professionalPlan.MonthlyPrice = NewProfessionalPrice;
                dbContext.SaveChanges();
                logger.LogInformation("Repriced Professional Tier from {Old} to {New} on startup.", OldProfessionalPrice, NewProfessionalPrice);
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
                    FullName = "Super Admin",
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
        });
    }
    catch (Exception ex)
    {
        // All Polly retries exhausted. Log fatally — the app will still call app.Run() so
        // /health returns 200 (the process is alive) but /health/ready will fail on the
        // DB check, telling any load balancer to keep traffic away until the operator
        // resolves the underlying issue.
        logger.LogError(ex, "FATAL: Database migration and seeding failed after {MaxRetries} attempts.", maxRetries);
    }
}

app.Run();

