using System.Collections.Concurrent;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SmartCampus.Gateway.IntegrationTests;

/// <summary>Ciò che il servizio a valle ha ricevuto dal Gateway.</summary>
public sealed record EchoResponse(string Method, string Path, string? Authorization);

/// <summary>
/// Servizio a valle finto (Kestrel su una porta libera): risponde 200 a qualsiasi richiesta restituendo
/// metodo, percorso e intestazione Authorization ricevuti, e registra i percorsi in <see cref="ReceivedPaths"/>.
/// Serve a verificare cosa passa il Gateway e cosa non arriva a valle.
/// </summary>
public sealed class FakeDownstream : IAsyncDisposable
{
    private readonly WebApplication _app;

    private FakeDownstream(WebApplication app, string address, ConcurrentQueue<string> receivedPaths)
    {
        _app = app;
        Address = address;
        ReceivedPaths = receivedPaths;
    }

    public string Address { get; }

    /// <summary>Percorsi di tutte le richieste arrivate al servizio finto, nell'ordine di arrivo.</summary>
    public ConcurrentQueue<string> ReceivedPaths { get; }

    public static async Task<FakeDownstream> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        var receivedPaths = new ConcurrentQueue<string>();
        app.MapFallback((HttpContext context) =>
        {
            receivedPaths.Enqueue(context.Request.Path.Value ?? string.Empty);
            return Results.Ok(new EchoResponse(
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                context.Request.Headers.Authorization.ToString() is { Length: > 0 } header ? header : null));
        });

        await app.StartAsync();
        return new FakeDownstream(app, app.Urls.First(), receivedPaths);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
