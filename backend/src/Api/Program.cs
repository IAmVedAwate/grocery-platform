using System.Text;
using System.Threading.RateLimiting;
using Api.Authorization;
using Api.Middleware;
using Application.Catalog;
using Application.Common;
using Application.Identity;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Structured logging (docs/PRD.md §19). Replaces the default console
// provider entirely rather than running alongside it. Framework
// categories are pushed down to Warning — EF Core alone logs every SQL
// command at Information, which floods real signal with noise; "log
// everything at Information" is not the same thing as observability.
// CorrelationIdMiddleware (registered below) pushes a per-request
// CorrelationId into LogContext, so every line below is traceable back
// to the request that produced it.
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning);

    // Humans read the dev console; a real deployment's log aggregator
    // wants structured JSON, not a hand-formatted line — same "one value,
    // one place, environment decides the shape" principle as everything
    // else config-related in this project.
    if (context.HostingEnvironment.IsDevelopment())
        configuration.WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}");
    else
        configuration.WriteTo.Console(new CompactJsonFormatter());
});

// IMPORTANT: every config value below is resolved lazily via DI
// (IConfiguration injected into a Configure<T> delegate, or an
// AddDbContext(sp, options) overload) rather than read eagerly here as
// `builder.Configuration["..."]`. WebApplicationFactory-based integration
// tests apply their configuration overrides as part of the deferred host
// build pipeline — an eager read here captures whatever value existed
// BEFORE that override is applied (this cost real debugging time to find;
// see docs/operations/troubleshooting.md).

// --- Persistence ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddDbContext<GroceryDbContext>((sp, options) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration["DB_CONNECTION_STRING"]
        ?? throw new InvalidOperationException("DB_CONNECTION_STRING is not configured.");
    var retryCount = int.TryParse(configuration["SQL_RETRY_COUNT"], out var rc) ? rc : 3;
    var retryMaxDelaySeconds = int.TryParse(configuration["SQL_RETRY_MAX_DELAY_SECONDS"], out var rd) ? rd : 5;

    // Resilience for this system's one real external dependency
    // (docs/checkpoints/skills-inventory.md). Deliberately EF Core's own
    // connection resiliency, not a hand-rolled Polly policy wrapped
    // around SaveChangesAsync — a generic retry wrapper that isn't
    // execution-strategy-aware can retry on top of change-tracking state
    // left over from a half-applied attempt, which is exactly the class
    // of bug EF Core's IExecutionStrategy exists to prevent. Internally
    // this *is* a retry policy (transient SQL errors — timeouts,
    // connection drops — get retried with backoff); it's just the
    // EF-Core-native mechanism instead of a second one bolted on top.
    // Never retries on DbUpdateConcurrencyException (the RowVersion
    // conflict this app already translates to 409, GroceryDbContext.cs)
    // — that's a real business conflict, not a transient fault.
    options.UseSqlServer(connectionString, sql =>
        sql.EnableRetryOnFailure(retryCount, TimeSpan.FromSeconds(retryMaxDelaySeconds), errorNumbersToAdd: null));
});
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<GroceryDbContext>());

// --- Identity ---
// Default IUserValidator/IRoleValidator enforce GLOBAL uniqueness, which
// breaks multi-tenancy outright (every store's "Admin" role, or two
// different stores' admins sharing an email, would collide). Replaced
// with tenant-scoped equivalents — see docs/operations/troubleshooting.md.
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = false;
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<GroceryDbContext>();

builder.Services.RemoveAll<IUserValidator<ApplicationUser>>();
builder.Services.AddScoped<IUserValidator<ApplicationUser>, TenantScopedUserValidator>();
builder.Services.RemoveAll<IRoleValidator<ApplicationRole>>();
builder.Services.AddScoped<IRoleValidator<ApplicationRole>, TenantScopedRoleValidator>();

// --- Application services (interfaces -> Infrastructure implementations) ---
builder.Services.AddScoped<IIdentityService, IdentityServiceImpl>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenServiceImpl>();
builder.Services.AddScoped<IStoreRepository, StoreRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IAuditWriter, Infrastructure.Audit.AuditWriter>();

// Local filesystem only for now (docs/PRD.md §26) — STORAGE_PROVIDER=azureBlob
// is a documented Phase 4 migration target (a second IStorageService
// implementation selected here), not implemented yet, so there's nothing
// to branch on at registration time.
builder.Services.AddScoped<Application.Common.IStorageService, Infrastructure.Storage.LocalFileStorageService>();

// Singleton: InferenceSession loads/JITs the ONNX model graph once at
// startup, not per request (matches the source practice project).
builder.Services.AddSingleton<Application.Common.IColorExtractionService, Infrastructure.ColorExtraction.OnnxColorExtractionService>();
builder.Services.AddScoped<AuthApplicationService>();
builder.Services.AddScoped<Application.Identity.UserManagementApplicationService>();
builder.Services.AddScoped<ProductApplicationService>();
builder.Services.AddScoped<Application.Catalog.ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<Application.Catalog.CategoryApplicationService>();
builder.Services.AddMemoryCache();

// Phase 2 — Inventory / Purchasing / Sales
builder.Services.AddScoped<Application.Inventory.IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<Application.Inventory.InventoryApplicationService>();
builder.Services.AddScoped<Application.Purchasing.ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<Application.Purchasing.IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<Application.Purchasing.SupplierApplicationService>();
builder.Services.AddScoped<Application.Purchasing.PurchasingApplicationService>();
builder.Services.AddScoped<Application.Sales.ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<Application.Sales.ISalesOrderRepository, SalesOrderRepository>();
builder.Services.AddScoped<Application.Sales.CustomerApplicationService>();
builder.Services.AddScoped<Application.Sales.SalesApplicationService>();

// Phase 3 — Reporting
builder.Services.AddScoped<Application.Reporting.IReportingRepository, ReportingRepository>();
builder.Services.AddScoped<Application.Reporting.ReportingApplicationService>();

// Phase 3 — Notifications (low-stock background sweep)
builder.Services.AddScoped<Application.Notifications.INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<Application.Notifications.NotificationApplicationService>();
builder.Services.AddScoped<Application.Notifications.ILowStockNotificationGenerator, Infrastructure.Notifications.LowStockNotificationGenerator>();
builder.Services.AddHostedService<Api.BackgroundServices.LowStockNotificationWorker>();

// --- AuthN/AuthZ ---
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        var jwtSigningKey = configuration["JWT_SIGNING_KEY"]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY is not configured.");
        var jwtIssuer = configuration["JWT_ISSUER"] ?? "quickstock-api";
        var jwtAudience = configuration["JWT_AUDIENCE"] ?? "quickstock-client";

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

// --- Cross-cutting ---
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

// Rate limiting on auth endpoints (docs/security/security-model.md —
// blunts credential-stuffing/brute-force). Fixed window per client IP;
// QueueLimit 0 means an over-limit request is rejected immediately with
// 429, never queued/delayed — for an abuse-protection policy, "wait and
// retry" is the wrong shape, we want a hard no.
//
// The partitioner delegate below resolves IConfiguration from
// httpContext.RequestServices — i.e. PER REQUEST, not once at startup.
// AddRateLimiter's configureOptions callback runs immediately/eagerly
// (unlike AddOptions<T>().Configure<IConfiguration>()), so capturing
// `configuration` in a closure here would suffer the exact same
// WebApplicationFactory-override-invisible bug documented in
// docs/operations/troubleshooting.md. Reading it inside the per-request
// delegate is how RateLimiterOptions supports the deferred pattern.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
    {
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var permitLimit = int.TryParse(configuration["RATE_LIMIT_AUTH_PERMIT_LIMIT"], out var limit) ? limit : 10;
        var windowSeconds = int.TryParse(configuration["RATE_LIMIT_AUTH_WINDOW_SECONDS"], out var seconds) ? seconds : 60;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0
            });
    });
});

builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        // Comma-separated. Next.js dev takes whatever port is free
        // starting at 3000 — on this machine that's usually 3001, since
        // another project's dev server already holds 3000 (see
        // docs/operations/troubleshooting.md) — so both are allowed by
        // default rather than hardcoding one and hitting this again.
        var origins = (configuration["ALLOWED_WEB_ORIGIN"] ?? "http://localhost:3000,http://localhost:3001")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
    });

// Enums as JSON strings ("Cash", not 0) — matches every DTO that already
// calls .ToString() on an enum for responses, and is what a real client
// (the Next.js app, these tests) sends for request bodies too.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();

// "database" is tagged "ready" only — /health/live must stay dependency-free
// (it answers "is the process up", not "can it reach SQL Server"); without
// the tag filter below both endpoints would run every check, which is what
// happened the first time this was wired up (docs/operations/troubleshooting.md).
builder.Services.AddHealthChecks()
    .AddDbContextCheck<GroceryDbContext>("database", tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Local/dev convenience only — staging/production apply migrations as
    // an explicit CI/CD deploy step (docs/architecture/deployment-architecture.md),
    // never automatically on process start.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<GroceryDbContext>().Database.MigrateAsync();
}

// CorrelationIdMiddleware and the request-logging line both need to wrap
// literally everything else, including the exception handler, so a
// correlation id and a logged summary exist even for a request that
// blows up before reaching a controller.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler(_ => { });
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

app.Run();

// Exposed so WebApplicationFactory<Program> in the Integration test project
// can host this API in-process (see backend/tests/Integration).
public partial class Program;
