using System.Text.RegularExpressions;

namespace UruErpApp.Api.Endpoints;

internal static class ApiHelpers
{
    internal static Guid GetTenantId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst("tenantId")!.Value);

    internal static string Slugify(string input) =>
        Regex.Replace(input.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');
}
