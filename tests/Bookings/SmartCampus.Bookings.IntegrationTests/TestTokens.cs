using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace SmartCampus.Bookings.IntegrationTests;

// Token di prova con i claim "sub", "name" (se indicato) e "role".
// La firma è finta: Bookings non la verifica (lo fa il Gateway).
internal static class TestTokens
{
    public static string For(Guid userId, string? name)
    {
        static string Encode(object value) =>
            Base64UrlTextEncoder.Encode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));

        object payload = name is null
            ? new { sub = userId.ToString(), role = "Student" }
            : new { sub = userId.ToString(), name, role = "Student" };

        return $"{Encode(new { alg = "none", typ = "JWT" })}.{Encode(payload)}.test";
    }
}
