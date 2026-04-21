using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UruErpApp.Api.Auth;
using UruErpApp.Api.Models;

namespace UruErpApp.Api.Endpoints;

public static class AuthEndpoints
{
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (RegisterRequest req, AppDbContext db, JwtService jwt) =>
        {
            if (await db.Users.AnyAsync(u => u.Email == req.Email))
                return Results.Conflict(new { detail = "El email ya está registrado." });

            var tenant = new Tenant { Name = req.CompanyName, Slug = ApiHelpers.Slugify(req.CompanyName) };

            if (await db.Tenants.AnyAsync(t => t.Slug == tenant.Slug))
                tenant.Slug = $"{tenant.Slug}-{Guid.NewGuid().ToString("N")[..6]}";

            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            var hasher = new PasswordHasher<AppUser>();
            var user = new AppUser
            {
                TenantId = tenant.Id,
                Email    = req.Email.ToLowerInvariant().Trim(),
                Name     = req.Name,
                Role     = "admin",
            };
            user.PasswordHash = hasher.HashPassword(user, req.Password);

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Ok(new { token = jwt.Generate(user), name = user.Name, tenantName = tenant.Name });
        });

        app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db, JwtService jwt) =>
        {
            var user = await db.Users
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant().Trim());

            if (user is null) return Results.Unauthorized();

            var hasher = new PasswordHasher<AppUser>();
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
            if (result == PasswordVerificationResult.Failed) return Results.Unauthorized();

            return Results.Ok(new { token = jwt.Generate(user), name = user.Name, tenantName = user.Tenant.Name });
        });

        return app;
    }
}
