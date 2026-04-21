using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UruErpApp.Api;
using UruErpApp.Api.Auth;
using UruErpApp.Api.Endpoints;
using UruErpApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ────────────────────────────────────────────────────────────────
// Railway provides DATABASE_URL; Aspire provides the named connection string.
string? rawConnectionString;
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var separatorIndex = uri.UserInfo.IndexOf(':');
    var encodedUsername = separatorIndex >= 0 ? uri.UserInfo[..separatorIndex] : uri.UserInfo;
    var encodedPassword = separatorIndex >= 0 ? uri.UserInfo[(separatorIndex + 1)..] : string.Empty;

    var connStrBuilder = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(encodedUsername),
        Password = Uri.UnescapeDataString(encodedPassword),
        SslMode = Npgsql.SslMode.Require,
        TrustServerCertificate = true
    };

    rawConnectionString = connStrBuilder.ConnectionString;
    builder.Services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(rawConnectionString));
}
else
{
    rawConnectionString = builder.Configuration.GetConnectionString("uruerp");
    builder.AddNpgsqlDbContext<AppDbContext>("uruerp");
}

// ── Auth ────────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret must be configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer   = false,
            ValidateAudience = false,
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtService>();

// ── Cloudflare R2 (optional) ────────────────────────────────────────────────
var r2Configured = !string.IsNullOrWhiteSpace(builder.Configuration["CloudflareR2:AccountId"])
                && !string.IsNullOrWhiteSpace(builder.Configuration["CloudflareR2:AccessKeyId"])
                && !string.IsNullOrWhiteSpace(builder.Configuration["CloudflareR2:SecretAccessKey"])
                && !string.IsNullOrWhiteSpace(builder.Configuration["CloudflareR2:BucketName"]);
if (r2Configured)
    builder.Services.AddSingleton<CloudflareR2Service>();

// ── Invoice Mailer (Cloudflare Worker) ──────────────────────────────────────
builder.Services.AddHttpClient("InvoiceMailer");
builder.Services.AddScoped<InvoiceMailerService>();

// ── CORS ────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration["AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p => p
        .WithOrigins(
            allowedOrigins is { Length: > 0 }
                ? allowedOrigins
                : ["http://localhost:5173"])
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// ── SQL migrations ──────────────────────────────────────────────────────────
if (!string.IsNullOrWhiteSpace(rawConnectionString))
{
    var migrationLogger = app.Services.GetRequiredService<ILogger<Program>>();
    await MigrationRunner.RunAsync(rawConnectionString, migrationLogger);
}
else
{
    app.Logger.LogWarning(
        "No database connection string found (DATABASE_URL env var or 'ConnectionStrings:uruerp' config key). Skipping migrations.");
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ────────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapDashboardEndpoints();
app.MapCfeEndpoints();
app.MapConfigEndpoints();
app.MapInvoicesEndpoints();

app.Run();
