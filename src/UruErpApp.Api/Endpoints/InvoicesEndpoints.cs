using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using UruErpApp.Api.Models;
using UruErpApp.Api.Services;
using UruFacturaSDK;
using UruFacturaSDK.Configuration;
using UruFacturaSDK.Enums;
using UruFacturaSDK.Models;

namespace UruErpApp.Api.Endpoints;

public static class InvoicesEndpoints
{
    public static WebApplication MapInvoicesEndpoints(this WebApplication app)
    {
        // ── List invoices ──────────────────────────────────────────────────────
        app.MapGet("/api/invoices", async (AppDbContext db, HttpContext ctx) =>
        {
            var tenantId = ApiHelpers.GetTenantId(ctx);
            return await db.Invoices
                .Where(i => i.TenantId == tenantId)
                .OrderByDescending(i => i.FechaEmision)
                .ToListAsync();
        }).RequireAuthorization();

        // ── Create invoice ─────────────────────────────────────────────────────
        app.MapPost("/api/invoices", async (CreateInvoiceRequest req, AppDbContext db, IConfiguration config,
            HttpContext ctx, IServiceProvider services) =>
        {
            var tenantId = ApiHelpers.GetTenantId(ctx);
            var ufConfig = config.GetSection("UruFactura").Get<UruFacturaConfig>()!;

            using var client = new UruFacturaClient(ufConfig);

            var tipo = (TipoCfe)req.TipoCfe;

            var cfe = tipo switch
            {
                TipoCfe.ETicket               => client.CrearETicket(),
                TipoCfe.NotaCreditoETicket    => client.CrearNotaCreditoETicket(),
                TipoCfe.NotaDebitoETicket     => client.CrearNotaDebitoETicket(),
                TipoCfe.EFactura              => client.CrearEFactura(),
                TipoCfe.NotaCreditoEFactura   => client.CrearNotaCreditoEFactura(),
                TipoCfe.NotaDebitoEFactura    => client.CrearNotaDebitoEFactura(),
                TipoCfe.EFacturaExportacion   => client.CrearEFacturaExportacion(),
                TipoCfe.ERemito               => client.CrearERemito(),
                _ => throw new ArgumentException($"Tipo de CFE no soportado: {tipo}"),
            };

            cfe.Numero = req.Numero;

            if (!string.IsNullOrWhiteSpace(req.RutReceptor))
                cfe.Receptor = new Receptor { Documento = req.RutReceptor, RazonSocial = req.NombreReceptor };

            for (int i = 0; i < req.Detalle.Count; i++)
            {
                var l = req.Detalle[i];
                cfe.Detalle.Add(new LineaDetalle
                {
                    NroLinea       = i + 1,
                    NombreItem     = l.NombreItem,
                    Cantidad       = l.Cantidad,
                    PrecioUnitario = l.PrecioUnitario,
                    IndFactIva     = (TipoIva)l.IndFactIva,
                });
            }

            foreach (var r in req.Referencias ?? [])
            {
                if (!Enum.IsDefined(typeof(TipoCfe), r.TipoCfe))
                    return Results.BadRequest(new { detail = $"TipoCfe de referencia inválido: {r.TipoCfe}" });

                cfe.Referencias.Add(new RefCfe
                {
                    TipoCfe  = (TipoCfe)r.TipoCfe,
                    Serie    = r.Serie ?? string.Empty,
                    NroCfe   = r.NroCfe,
                    FechaCfe = r.FechaCfe,
                    Razon    = r.Razon,
                });
            }

            cfe.CalcularTotales();
            client.GenerarYFirmar(cfe);

            var pdfBytes = client.GenerarPdfA4(cfe);

            var invoice = new Invoice
            {
                TenantId        = tenantId,
                TipoCfe         = (int)cfe.Tipo,
                Numero          = cfe.Numero,
                FechaEmision    = cfe.FechaEmision,
                RutReceptor     = cfe.Receptor?.Documento,
                NombreReceptor  = cfe.Receptor?.RazonSocial,
                MontoTotal      = cfe.MontoTotal,
                MontoNetoExento = cfe.MontoNetoExento,
                MontoNetoMinimo = cfe.MontoNetoMinimo,
                MontoNetoBasico = cfe.MontoNetoBasico,
                IvaMinimo       = cfe.IvaMinimo,
                IvaBasico       = cfe.IvaBasico,
                XmlFirmado      = cfe.XmlFirmado,
                DetalleJson     = JsonSerializer.Serialize(cfe.Detalle),
            };

            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            // ── Upload to Cloudflare R2 (best-effort) ─────────────────────────
            var r2 = services.GetService<CloudflareR2Service>();
            if (r2 is not null)
            {
                var prefix = $"tenant-{tenantId}/{cfe.FechaEmision:yyyy/MM}";
                try
                {
                    var pdfKey = $"{prefix}/{invoice.Id}-{(int)cfe.Tipo}-{cfe.Numero}.pdf";
                    await r2.UploadAsync(pdfKey, pdfBytes, "application/pdf");
                    invoice.R2PdfKey = pdfKey;
                }
                catch (Exception ex)
                {
                    app.Logger.LogWarning(ex, "R2 PDF upload failed for invoice {InvoiceId}.", invoice.Id);
                }

                if (!string.IsNullOrWhiteSpace(cfe.XmlFirmado))
                {
                    try
                    {
                        var xmlBytes = System.Text.Encoding.UTF8.GetBytes(cfe.XmlFirmado);
                        var xmlKey   = $"{prefix}/{invoice.Id}-{(int)cfe.Tipo}-{cfe.Numero}.xml";
                        await r2.UploadAsync(xmlKey, xmlBytes, "application/xml");
                        invoice.R2XmlKey = xmlKey;
                    }
                    catch (Exception ex)
                    {
                        app.Logger.LogWarning(ex, "R2 XML upload failed for invoice {InvoiceId}.", invoice.Id);
                    }
                }

                if (invoice.R2PdfKey is not null || invoice.R2XmlKey is not null)
                    await db.SaveChangesAsync();
            }

            // ── Send email notification via Cloudflare Worker (best-effort) ───
            if (!string.IsNullOrWhiteSpace(req.RecipientEmail))
            {
                var mailer = services.GetRequiredService<InvoiceMailerService>();
                var tenant = await db.Tenants.FindAsync(tenantId);
                string? pdfUrl = null;
                if (r2 is not null && invoice.R2PdfKey is not null)
                    pdfUrl = await r2.GetDownloadUrlAsync(invoice.R2PdfKey);

                await mailer.NotifyAsync(new EmailPayload(
                    TenantName:     tenant?.Name ?? string.Empty,
                    RecipientEmail: req.RecipientEmail,
                    RecipientName:  req.RecipientName ?? req.RecipientEmail,
                    InvoiceId:      invoice.Id,
                    InvoiceNumber:  invoice.Numero,
                    TipoCfe:        invoice.TipoCfe,
                    TipoCfeLabel:   cfe.Tipo.ToString(),
                    Total:          invoice.MontoTotal,
                    PdfUrl:         pdfUrl));
            }

            return Results.Created($"/api/invoices/{invoice.Id}", invoice);
        }).RequireAuthorization();

        // ── Download PDF (from R2 if stored, otherwise regenerate) ────────────
        app.MapGet("/api/invoices/{id:guid}/pdf", async (Guid id, AppDbContext db, IConfiguration config,
            HttpContext ctx, IServiceProvider services) =>
        {
            var tenantId = ApiHelpers.GetTenantId(ctx);
            var invoice  = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId);
            if (invoice is null) return Results.NotFound();

            var r2 = services.GetService<CloudflareR2Service>();
            if (r2 is not null && !string.IsNullOrWhiteSpace(invoice.R2PdfKey))
            {
                try
                {
                    var pdfBytes = await r2.DownloadAsync(invoice.R2PdfKey);
                    return Results.File(pdfBytes, "application/pdf", $"factura-{invoice.Numero}.pdf");
                }
                catch (Exception ex)
                {
                    app.Logger.LogWarning(ex, "R2 PDF download failed for invoice {Id}, falling back to on-the-fly generation.", id);
                }
            }

            // Fallback: regenerate PDF from stored data
            var ufConfig = config.GetSection("UruFactura").Get<UruFacturaConfig>()!;
            using var client = new UruFacturaClient(ufConfig);

            var cfe = new Cfe
            {
                Tipo                  = (TipoCfe)invoice.TipoCfe,
                Numero                = invoice.Numero,
                FechaEmision          = invoice.FechaEmision,
                MontoTotal            = invoice.MontoTotal,
                MontoNetoExento       = invoice.MontoNetoExento,
                MontoNetoMinimo       = invoice.MontoNetoMinimo,
                MontoNetoBasico       = invoice.MontoNetoBasico,
                IvaMinimo             = invoice.IvaMinimo,
                IvaBasico             = invoice.IvaBasico,
                RutEmisor             = ufConfig.RutEmisor,
                RazonSocialEmisor     = ufConfig.RazonSocialEmisor,
                DomicilioFiscalEmisor = ufConfig.DomicilioFiscal,
                CiudadEmisor          = ufConfig.Ciudad,
                DepartamentoEmisor    = ufConfig.Departamento,
                XmlFirmado            = invoice.XmlFirmado,
            };

            if (!string.IsNullOrWhiteSpace(invoice.DetalleJson))
            {
                try
                {
                    var detalle = JsonSerializer.Deserialize<List<LineaDetalle>>(invoice.DetalleJson);
                    if (detalle is not null) cfe.Detalle.AddRange(detalle);
                }
                catch (JsonException)
                {
                    return Results.Problem(
                        detail:     "La factura no puede generar el PDF porque su detalle almacenado es inválido o incompatible.",
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        title:      "Detalle de factura inválido");
                }
            }

            if (!string.IsNullOrWhiteSpace(invoice.RutReceptor))
                cfe.Receptor = new Receptor { Documento = invoice.RutReceptor, RazonSocial = invoice.NombreReceptor };

            var pdf = client.GenerarPdfA4(cfe);
            return Results.File(pdf, "application/pdf", $"factura-{invoice.Numero}.pdf");
        }).RequireAuthorization();

        // ── R2 download URLs ───────────────────────────────────────────────────
        app.MapGet("/api/invoices/{id:guid}/r2-urls", async (Guid id, AppDbContext db, HttpContext ctx,
            IServiceProvider services) =>
        {
            var tenantId = ApiHelpers.GetTenantId(ctx);
            var invoice  = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId);
            if (invoice is null) return Results.NotFound();

            var r2 = services.GetService<CloudflareR2Service>();
            if (r2 is null)
                return Results.Problem("Cloudflare R2 is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);

            string? pdfUrl = invoice.R2PdfKey is not null ? await r2.GetDownloadUrlAsync(invoice.R2PdfKey) : null;
            string? xmlUrl = invoice.R2XmlKey is not null ? await r2.GetDownloadUrlAsync(invoice.R2XmlKey) : null;

            return Results.Ok(new { pdfUrl, xmlUrl });
        }).RequireAuthorization();

        return app;
    }
}
