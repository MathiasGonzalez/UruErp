using UruFacturaSDK.Configuration;

namespace UruErpApp.Api.Endpoints;

public static class ConfigEndpoints
{
    public static WebApplication MapConfigEndpoints(this WebApplication app)
    {
        app.MapGet("/api/config/status", (IConfiguration config, HttpContext ctx) =>
        {
            var section  = config.GetSection("UruFactura");
            var ufConfig = section.Get<UruFacturaConfig>() ?? new UruFacturaConfig();

            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(ufConfig.RutEmisor))         issues.Add("RutEmisor no configurado.");
            if (string.IsNullOrWhiteSpace(ufConfig.RazonSocialEmisor)) issues.Add("RazonSocialEmisor no configurado.");
            if (string.IsNullOrWhiteSpace(ufConfig.DomicilioFiscal))   issues.Add("DomicilioFiscal no configurado.");
            if (string.IsNullOrWhiteSpace(ufConfig.RutaCertificado))
                issues.Add("RutaCertificado no configurado.");
            else if (!File.Exists(ufConfig.RutaCertificado))
                issues.Add($"Certificado no encontrado: {ufConfig.RutaCertificado}");
            if (string.IsNullOrWhiteSpace(ufConfig.PasswordCertificado)) issues.Add("PasswordCertificado no configurado.");

            return new
            {
                ok                = issues.Count == 0,
                ambiente          = ufConfig.Ambiente.ToString(),
                rutEmisor         = ufConfig.RutEmisor,
                razonSocial       = ufConfig.RazonSocialEmisor,
                certificado       = ufConfig.RutaCertificado,
                certificadoExiste = !string.IsNullOrWhiteSpace(ufConfig.RutaCertificado) && File.Exists(ufConfig.RutaCertificado),
                issues,
            };
        }).RequireAuthorization();

        return app;
    }
}
