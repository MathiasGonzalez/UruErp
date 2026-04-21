using Microsoft.EntityFrameworkCore;

namespace UruErpApp.Api.Endpoints;

public static class DashboardEndpoints
{
    public static WebApplication MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/dashboard", async (AppDbContext db, HttpContext ctx) =>
        {
            var tenantId = ApiHelpers.GetTenantId(ctx);
            var invoices = db.Invoices.Where(i => i.TenantId == tenantId);
            var weekAgo  = DateTime.UtcNow.AddDays(-7);
            var monthAgo = DateTime.UtcNow.AddDays(-30);

            return new
            {
                totalInvoices = await invoices.CountAsync(),
                totalRevenue  = await invoices.SumAsync(i => (decimal?)i.MontoTotal) ?? 0m,
                weekInvoices  = await invoices.Where(i => i.FechaEmision >= weekAgo).CountAsync(),
                monthRevenue  = await invoices.Where(i => i.FechaEmision >= monthAgo).SumAsync(i => (decimal?)i.MontoTotal) ?? 0m,
                acceptedByDgi = await invoices.CountAsync(i => i.AceptadoPorDgi),
                byType        = await invoices
                    .GroupBy(i => i.TipoCfe)
                    .Select(g => new { tipoCfe = g.Key, count = g.Count(), total = g.Sum(i => i.MontoTotal) })
                    .ToListAsync(),
            };
        }).RequireAuthorization();

        return app;
    }
}
