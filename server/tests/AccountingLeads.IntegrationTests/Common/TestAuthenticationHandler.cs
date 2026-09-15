using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AccountingLeads.IntegrationTests.Common;

/// <summary>
/// Header names a test sets to describe the fake identity it wants for a single request.
/// <see cref="TestAuthenticationHandler"/> reads these instead of parsing a real JWT, since
/// <see cref="IntegrationTestWebApplicationFactory"/> cannot call real Entra ID in a test run
/// (see docs/architecture.md §23's "Acceptance Checks and Remaining Setup").
/// </summary>
public static class TestAuthHeaders
{
    /// <summary>Presence of this header (any value) means "authenticate this request".</summary>
    public const string Authenticated = "X-Test-Authenticated";

    /// <summary>Space-delimited scope values, mapped to the "scp" claim Microsoft.Identity.Web's
    /// <c>[RequiredScope]</c> reads.</summary>
    public const string Scopes = "X-Test-Scopes";

    public const string Oid = "X-Test-Oid";
    public const string Tid = "X-Test-Tid";
}

/// <summary>
/// Replaces the real JWT Bearer scheme in integration tests (see
/// <see cref="IntegrationTestWebApplicationFactory.ConfigureWebHost"/>). Wrong-tenant,
/// wrong-audience, malformed, expired, and ID-token-as-bearer scenarios are real JWT
/// signature/issuer/audience concerns this fake scheme cannot exercise (it never parses a
/// real token) — those stay covered by the manual Entra verification checklist in
/// docs/architecture.md §23, not by tests using this handler.
/// </summary>
public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey(TestAuthHeaders.Authenticated))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user"),
            new("oid", Request.Headers[TestAuthHeaders.Oid].FirstOrDefault() ?? Guid.NewGuid().ToString()),
            new("tid", Request.Headers[TestAuthHeaders.Tid].FirstOrDefault() ?? "test-tenant"),
        };

        var scopes = Request.Headers[TestAuthHeaders.Scopes].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(scopes))
        {
            claims.Add(new Claim("scp", scopes));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
