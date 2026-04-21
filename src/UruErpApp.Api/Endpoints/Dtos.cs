namespace UruErpApp.Api.Endpoints;

record RegisterRequest(string CompanyName, string Name, string Email, string Password);
record LoginRequest(string Email, string Password);

record CreateInvoiceRequest(
    int TipoCfe,
    long Numero,
    string? RutReceptor,
    string? NombreReceptor,
    List<LineaDetalleDto> Detalle,
    List<RefCfeDto>? Referencias = null,
    string? RecipientEmail = null,
    string? RecipientName  = null);

record LineaDetalleDto(
    string NombreItem,
    decimal Cantidad,
    decimal PrecioUnitario,
    int IndFactIva);

record RefCfeDto(
    int TipoCfe,
    string? Serie,
    long NroCfe,
    DateTime FechaCfe,
    string? Razon);
