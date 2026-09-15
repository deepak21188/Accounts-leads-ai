using AccountingLeads.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingLeads.IntegrationTests.Common;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> subclass that redirects the real SQL
/// Server <see cref="ApplicationDbContext"/> wired up in <c>Program.cs</c> at a dedicated
/// LocalDB integration-test database, instead of the developer's personal
/// <c>AccountingLeadsDb</c> database configured in <c>appsettings.Development.json</c>.
///
/// Integration tests intentionally still use a real SQL Server (LocalDB) instance rather
/// than an in-memory provider, to exercise the same persistence technology as production
/// (per docs/architecture.md). Isolation from the developer's own database is achieved
/// purely through configuration override (a different connection string) plus data reset
/// between tests, with no new NuGet package required.
/// </summary>
public sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// Exposed (not private) so tests that call <see cref="WithWebHostBuilder"/> to further
    /// customize the host (e.g. to override the hosting environment) can re-apply the same
    /// connection-string override explicitly. <c>WithWebHostBuilder</c> builds a distinct host
    /// instance layered on top of this factory's own <see cref="ConfigureWebHost"/>, and
    /// re-supplying the same override there removes any doubt about configuration source
    /// ordering between the two.
    /// </summary>
    public const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=AccountingLeadsDb_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Appended after Program.cs's own configuration sources (including
        // appsettings.Development.json), so this value wins and the API under test never
        // touches the developer's personal AccountingLeadsDb database.
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            });
        });

        // Replaces Program.cs's real Microsoft.Identity.Web/JWT Bearer scheme with
        // TestAuthenticationHandler, since this factory cannot call real Entra ID in a test
        // run (see docs/architecture.md §23). PostConfigure runs after Program.cs's own
        // AddAuthentication(...).AddMicrosoftIdentityWebApi(...) call, so the fake scheme
        // becomes the default the fallback authorization policy and [RequiredScope] evaluate
        // against.
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            });
        });
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        // Ensures the dedicated integration-test database exists and is up to date. Safe to
        // run repeatedly; Migrate() only applies pending migrations.
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
        await ResetDatabaseAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Leave the dedicated integration-test database clean for the next run. This never
        // touches the developer's personal AccountingLeadsDb database.
        await ResetDatabaseAsync();
    }

    /// <summary>
    /// Deletes all rows from every table used by this vertical slice. Intended to run before
    /// each test so tests don't leak state into one another. There is no uniqueness
    /// constraint on any field yet, so a full delete is simplest and safe; a per-test
    /// transaction-rollback pattern would be more elaborate than this slice currently needs.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.LeadQualificationResults.ExecuteDeleteAsync();
        await dbContext.LeadAnalyses.ExecuteDeleteAsync();
        await dbContext.OutboxMessages.ExecuteDeleteAsync();
        await dbContext.Leads.ExecuteDeleteAsync();
    }
}
