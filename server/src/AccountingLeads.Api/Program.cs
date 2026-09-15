using System.Text.Json.Serialization;
using AccountingLeads.Application;
using AccountingLeads.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

const string ApiCorsPolicy = "ApiCorsPolicy";

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Accountant authentication (Slice 5, docs/architecture.md §23): Entra ID validates the
// delegated access token; anything without explicit [AllowAnonymous] requires a signed-in,
// assigned accountant by default. Per-endpoint scope checks (e.g. [RequiredScope]) are added
// alongside [Authorize] where needed, since an authenticated-but-wrong-scope caller must still
// get a 403, not just pass the authentication check.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Allowed origins come from config (Cors:AllowedOrigins) in every environment, not just
// Development, so the deployed React app's Static Web Apps origin can be added later purely
// as Azure App Service configuration — no code change needed once that URL is known. Missing
// config yields an empty array rather than throwing: unlike the DB connection string, the Api
// should still start with no cross-origin browser access allowed rather than fail to boot.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options => options.AddPolicy(ApiCorsPolicy, policy =>
    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // AllowAnonymous here because the new deny-by-default fallback policy below would
    // otherwise require a signed-in accountant just to view the OpenAPI document.
    app.MapOpenApi().AllowAnonymous();
}

app.UseCors(ApiCorsPolicy);

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> in the integration test project can bootstrap
// this API in-memory. No behavioral change.
public partial class Program;
