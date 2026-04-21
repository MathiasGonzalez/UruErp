namespace UruErpApp.Api.Models;

public class AppUser
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Role within the tenant: "admin" or "user".</summary>
    public string Role { get; set; } = "user";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
