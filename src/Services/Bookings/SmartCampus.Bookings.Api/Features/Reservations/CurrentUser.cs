using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace SmartCampus.Bookings.Api.Features.Reservations;

// Legge i dati dell'utente (claim "sub" e "name") dal JWT inoltrato dal Gateway.
// La firma NON viene verificata qui: lo fa già il Gateway, unico punto di ingresso
// (vedi docs/gateway.md, "I servizi a valle non rivalidano il token").
public static class CurrentUser
{
    public static Guid? TryGetUserId(HttpRequest request) =>
        TryReadClaim(request, "sub") is { } sub && Guid.TryParse(sub, out var userId)
            ? userId
            : null;

    // Nome visualizzato emesso da Identity. Null se il claim manca o è vuoto.
    public static string? TryGetUserName(HttpRequest request) =>
        TryReadClaim(request, "name") is { } name && !string.IsNullOrWhiteSpace(name)
            ? name
            : null;

    private static string? TryReadClaim(HttpRequest request, string claim)
    {
        var header = request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";

        if (!header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Un JWT è "intestazione.contenuto.firma": i claim stanno nella parte centrale, codificata in Base64Url.
        var parts = header[bearer.Length..].Trim().Split('.');

        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            using var payload = JsonDocument.Parse(Base64UrlTextEncoder.Decode(parts[1]));

            return payload.RootElement.ValueKind == JsonValueKind.Object
                   && payload.RootElement.TryGetProperty(claim, out var value)
                   && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }
}
