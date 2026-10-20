using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Healthz_returns_200()
    {
        var response = await _client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readyz_returns_200_once_started()
    {
        var response = await _client.GetAsync("/readyz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Version_returns_build_metadata()
    {
        var json = await _client.GetFromJsonAsync<JsonElement>("/version");
        Assert.True(json.TryGetProperty("commit", out _));
        Assert.True(json.TryGetProperty("buildNumber", out _));
        Assert.True(json.TryGetProperty("buildTime", out _));
    }

    [Fact]
    public async Task Whoami_returns_hostname()
    {
        var json = await _client.GetFromJsonAsync<JsonElement>("/whoami");
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("hostname").GetString()));
    }

    [Fact]
    public async Task Metrics_exposes_request_counter()
    {
        var body = await _client.GetStringAsync("/metrics");
        Assert.Contains("http_requests_total", body);
    }
}
