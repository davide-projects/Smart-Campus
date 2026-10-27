using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SmartCampus.Gateway.IntegrationTests;

/// <summary>Ciò che il servizio a valle ha ricevuto dal Gateway.</summary>
public sealed record EchoResponse(string Method, string Path, string? Authorization);

/// <summary>
/// Servizio a valle finto (Kestrel su una porta libera): risponde 200 a qualsiasi richiesta restituendo
/// metodo, percorso e intestazione Authorization ricevuti. Serve a verificare cosa passa il Gateway.
/// </summary>
public sealed class FakeDownstream : IAsyncDisposable
{
    private readonly WebApplication _app;

    private FakeDownstream(WebApplication app, string address)
    {
        _app = app;
        Address = address;
    }

    public string Address { get; }

    public static async Task<FakeDownstream> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.MapFallback((HttpContext context) => Results.Ok(new EchoResponse(
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty,
            context.Request.Headers.Authorization.ToString() is { Length: > 0 } header ? header : null)));

        await app.StartAsync();
        return new FakeDownstream(app, app.Urls.First());
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
