using System.Text;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

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
    options.UseSqlServer(connectionString);
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
builder.Services.AddScoped<AuthApplicationService>();
builder.Services.AddScoped<ProductApplicationService>();

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

builder.Services.AddControllers();
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

app.UseExceptionHandler(_ => { });
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

app.Run();

// Exposed so WebApplicationFactory<Program> in the Integration test project
// can host this API in-process (see backend/tests/Integration).
public partial class Program;
