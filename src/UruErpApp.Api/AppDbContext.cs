using Microsoft.EntityFrameworkCore;
using UruErpApp.Api.Models;

namespace UruErpApp.Api;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // IDs are generated in C# with Guid.CreateVersion7(); the DB default
        // (gen_random_uuid()) in the SQL migration is a safety net only.
        modelBuilder.Entity<Tenant>()
            .Property(t => t.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.Slug)
            .IsUnique();

        modelBuilder.Entity<AppUser>()
            .Property(u => u.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<AppUser>()
            .HasOne(u => u.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(u => u.TenantId);

        // Invoices are partitioned by FechaEmision in PostgreSQL.
        // The partition key must be part of the primary key.
        modelBuilder.Entity<Invoice>()
            .HasKey(i => new { i.Id, i.FechaEmision });

        modelBuilder.Entity<Invoice>()
            .Property(i => i.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Tenant)
            .WithMany(t => t.Invoices)
            .HasForeignKey(i => i.TenantId);
    }
}
