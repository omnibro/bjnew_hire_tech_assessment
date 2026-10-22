using System.Runtime.InteropServices;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// GIT_SHA, BUILD_NUMBER and BUILD_TIME are injected at image build time (Dockerfile ARG -> ENV).
// APP_VERSION is set at deploy time (docker run -e) to the image tag being released.
// Environment variables are part of the default configuration sources.
var build = new BuildMetadata(
    AppVersion: app.Configuration["APP_VERSION"] ?? "dev",
    Commit: app.Configuration["GIT_SHA"] ?? "dev",
    BuildNumber: app.Configuration["BUILD_NUMBER"] ?? "local",
    BuildTime: app.Configuration["BUILD_TIME"] ?? "unknown");

// Trust X-Forwarded-* headers so client IP/scheme are correct behind a proxy or ingress.
// By default only loopback proxies are trusted; widen KnownNetworks deliberately in real deployments.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Count every request for the /metrics endpoint.
app.Use(async (context, next) =>
{
    Interlocked.Increment(ref Counters.Requests);
    await next();
});

// Readiness flips to true once the host has started and to false as soon as shutdown begins,
// so an orchestrator stops sending traffic before the process exits.
app.Lifetime.ApplicationStarted.Register(() => ReadyState.IsReady = true);
app.Lifetime.ApplicationStopping.Register(() => ReadyState.IsReady = false);

// What is running right now? Answers "which commit/pipeline run produced this container?"
app.MapGet("/version", () => Results.Ok(new
{
    build.AppVersion,
    build.Commit,
    build.BuildNumber,
    build.BuildTime,
    Runtime = RuntimeInformation.FrameworkDescription
}));

// Liveness: the process is up and able to serve a request.
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

// Readiness: the app is willing to receive traffic.
app.MapGet("/readyz", () => Results.Json(
    new { status = ReadyState.IsReady ? "ready" : "not ready" },
    statusCode: ReadyState.IsReady ? 200 : 503));

// Which replica answered, and what did the proxy pass along? Sensitive headers are not echoed.
app.MapGet("/whoami", (HttpContext ctx) =>
{
    var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Authorization", "Cookie" };
    var headers = ctx.Request.Headers
        .Where(h => !hidden.Contains(h.Key))
        .ToDictionary(h => h.Key, h => h.Value.ToString());

    return Results.Ok(new
    {
        hostname = Environment.MachineName,
        clientIp = ctx.Connection.RemoteIpAddress?.ToString(),
        scheme = ctx.Request.Scheme,
        headers
    });
});

// Prometheus text exposition format, written by hand (no library needed).
app.MapGet("/metrics", () =>
{
    var body =
        "# HELP http_requests_total Total HTTP requests received.\n" +
        "# TYPE http_requests_total counter\n" +
        $"http_requests_total {Interlocked.Read(ref Counters.Requests)}\n";
    return Results.Text(body, "text/plain; version=0.0.4");
});

app.Logger.LogInformation("Starting BuildInfo API. commit={Commit} build={BuildNumber}", build.Commit, build.BuildNumber);
app.Run();

// ---- Types must come after top-level statements ----
record BuildMetadata(string AppVersion, string Commit, string BuildNumber, string BuildTime);

static class ReadyState { public static volatile bool IsReady; }

static class Counters { public static long Requests; }

// Makes the entry point visible to WebApplicationFactory<Program> in the test project.
public partial class Program { }
