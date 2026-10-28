using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace SmartCampus.Bookings.Api.Features.Reservations;

// Legge l'id dell'utente (claim "sub") dal JWT inoltrato dal Gateway.
// La firma NON viene verificata qui: lo fa già il Gateway, unico punto di ingresso
// (vedi docs/gateway.md, "I servizi a valle non rivalidano il token").
public static class CurrentUser
{
    public static Guid? TryGetUserId(HttpRequest request)
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

            return payload.RootElement.TryGetProperty("sub", out var sub) && Guid.TryParse(sub.GetString(), out var userId)
                ? userId
                : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }
}
