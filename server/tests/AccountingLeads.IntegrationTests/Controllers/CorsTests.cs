using AccountingLeads.IntegrationTests.Common;

namespace AccountingLeads.IntegrationTests.Controllers;

/// <summary>
/// Verifies the Api's CORS policy (<c>Program.cs</c>) actually enforces the configured
/// <c>Cors:AllowedOrigins</c> list end to end, via a real CORS preflight request through the
/// ASP.NET Core pipeline. <see cref="IntegrationTestWebApplicationFactory"/> runs under the
/// Development environment, so it picks up <c>appsettings.Development.json</c>'s
/// <c>Cors:AllowedOrigins</c> value (<c>http://localhost:5173</c>) the same way the real Api
/// does locally — no test-specific configuration override is needed.
/// </summary>
public class CorsTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorsTests(IntegrationTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage PreflightRequest(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/leads");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return request;
    }

    [Fact]
    public async Task Preflight_FromConfiguredOrigin_IsAllowed()
    {
        var response = await _client.SendAsync(PreflightRequest("http://localhost:5173"));

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal("http://localhost:5173", Assert.Single(values!));
    }

    [Fact]
    public async Task Preflight_FromUnconfiguredOrigin_IsNotAllowed()
    {
        var response = await _client.SendAsync(PreflightRequest("https://evil.example.com"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
