namespace UruErpApp.Api.Models;

public class Tenant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<AppUser> Users { get; set; } = [];
    public List<Invoice> Invoices { get; set; } = [];
}
