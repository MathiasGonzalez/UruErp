using UruFacturaSDK.Enums;

namespace UruErpApp.Api.Endpoints;

public static class CfeEndpoints
{
    public static WebApplication MapCfeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/cfe-types", () =>
            Enum.GetValues<TipoCfe>()
                .Select(t => new { value = (int)t, label = t.ToString() })
                .OrderBy(t => t.value));

        return app;
    }
}
