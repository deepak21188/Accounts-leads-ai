using System.Net;
using System.Net.Http.Json;
using AccountingLeads.Api.Contracts;
using AccountingLeads.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace AccountingLeads.IntegrationTests.Cors;

/// <summary>
/// HTTP-pipeline tests for the Development-only CORS policy added in <c>Program.cs</c>
/// (<c>DevClientCorsPolicy</c>), which allows the Vite dev server origin
/// (<c>http://localhost:5173</c>) to call the API cross-origin during local development.
///
/// These exercise the real CORS middleware (<c>app.UseCors(...)</c>) via
/// <see cref="IntegrationTestWebApplicationFactory"/>, asserting on the
/// <c>Access-Control-Allow-Origin</c> response header rather than on middleware
/// registration details, so the tests reflect what a browser would actually observe.
/// </summary>
public class CorsPolicyTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private const string AllowedDevOrigin = "http://localhost:5173";
    private const string DisallowedOrigin = "http://evil.example.com";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";

    private readonly IntegrationTestWebApplicationFactory _factory;

    public CorsPolicyTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Intentionally invalid (missing all required fields) so these "actual request" (as
    // opposed to preflight) tests exercise the CORS middleware on a real, non-OPTIONS
    // request/response without depending on successful Lead persistence — the API-level
    // validation short-circuits before any database call, keeping these tests fast and
    // independent of the outcome asserted by LeadsControllerTests.
    private static CreateLeadRequest InvalidRequest() => new()
    {
        Name = string.Empty,
        Email = string.Empty,
        Inquiry = string.Empty,
    };

    private HttpClient CreateClientForEnvironment(string environmentName) =>
        _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environmentName);

                // Re-applied explicitly via UseSetting (rather than ConfigureAppConfiguration)
                // because changing the environment here can affect configuration-source
                // ordering for the minimal-hosting-model compatibility shim that
                // WebApplicationFactory uses; UseSetting writes directly into the host's
                // configuration and is unaffected by that ordering.
                builder.UseSetting(
                    "ConnectionStrings:DefaultConnection",
                    IntegrationTestWebApplicationFactory.ConnectionString);
            })
            .CreateClient();

    private static HttpRequestMessage PreflightRequest(string origin) =>
        new(HttpMethod.Options, "/api/leads")
        {
            Headers =
            {
                { "Origin", origin },
                { "Access-Control-Request-Method", "POST" },
                { "Access-Control-Request-Headers", "Content-Type" },
            },
        };

    [Fact]
    public async Task Preflight_InDevelopment_FromAllowedViteOrigin_ReturnsAccessControlAllowOriginHeader()
    {
        var client = CreateClientForEnvironment(Environments.Development);

        var response = await client.SendAsync(PreflightRequest(AllowedDevOrigin));

        Assert.True(response.Headers.Contains(AllowOriginHeader));
        Assert.Equal(AllowedDevOrigin, response.Headers.GetValues(AllowOriginHeader).Single());
    }

    [Fact]
    public async Task Preflight_InDevelopment_FromDisallowedOrigin_DoesNotReturnAccessControlAllowOriginHeader()
    {
        var client = CreateClientForEnvironment(Environments.Development);

        var response = await client.SendAsync(PreflightRequest(DisallowedOrigin));

        Assert.False(response.Headers.Contains(AllowOriginHeader));
    }

    [Fact]
    public async Task Preflight_OutsideDevelopment_FromOtherwiseAllowedOrigin_DoesNotReturnAccessControlAllowOriginHeader()
    {
        // The CORS policy is only registered/applied when Environment.IsDevelopment() is true
        // (Program.cs). Outside Development, even the origin that Development explicitly
        // allows must not receive CORS headers.
        var client = CreateClientForEnvironment(Environments.Production);

        var response = await client.SendAsync(PreflightRequest(AllowedDevOrigin));

        Assert.False(response.Headers.Contains(AllowOriginHeader));
    }

    [Fact]
    public async Task ActualRequest_InDevelopment_FromAllowedViteOrigin_ReturnsAccessControlAllowOriginHeader()
    {
        var client = CreateClientForEnvironment(Environments.Development);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/leads")
        {
            Content = JsonContent.Create(InvalidRequest()),
        };
        request.Headers.Add("Origin", AllowedDevOrigin);

        var response = await client.SendAsync(request);

        // CORS headers are added by middleware ahead of MVC's own response handling, so they
        // are present regardless of the eventual status code; asserting 400 here confirms the
        // request actually reached (and was rejected by) API validation rather than short-
        // circuiting somewhere else.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.Contains(AllowOriginHeader));
        Assert.Equal(AllowedDevOrigin, response.Headers.GetValues(AllowOriginHeader).Single());
    }

    [Fact]
    public async Task ActualRequest_OutsideDevelopment_FromOtherwiseAllowedOrigin_DoesNotReturnAccessControlAllowOriginHeader()
    {
        // Confirms the API itself still works outside Development (no CORS policy is a
        // startup requirement) while simply not emitting cross-origin headers.
        var client = CreateClientForEnvironment(Environments.Production);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/leads")
        {
            Content = JsonContent.Create(InvalidRequest()),
        };
        request.Headers.Add("Origin", AllowedDevOrigin);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains(AllowOriginHeader));
    }
}
