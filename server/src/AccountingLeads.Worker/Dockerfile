# Build context is `server/` (not the repo root, not this directory) so this stage can COPY
# the Worker's project references (Application, Domain, Infrastructure) alongside it — see
# docs/architecture.md §20. Build with:
#   docker build -f src/AccountingLeads.Worker/Dockerfile -t accounting-leads-worker .
# from the `server/` directory.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first so `dotnet restore` is cached as its own layer and only
# reruns when a .csproj actually changes, not on every source edit.
COPY src/AccountingLeads.Worker/AccountingLeads.Worker.csproj src/AccountingLeads.Worker/
COPY src/AccountingLeads.Application/AccountingLeads.Application.csproj src/AccountingLeads.Application/
COPY src/AccountingLeads.Domain/AccountingLeads.Domain.csproj src/AccountingLeads.Domain/
COPY src/AccountingLeads.Infrastructure/AccountingLeads.Infrastructure.csproj src/AccountingLeads.Infrastructure/

RUN dotnet restore src/AccountingLeads.Worker/AccountingLeads.Worker.csproj

# Now copy the rest of the source for each project the Worker depends on (restoring against
# just the Worker's own .csproj above already pulled in Domain transitively via
# Application/Infrastructure, so only these four projects' full sources are needed — not the
# whole solution, which also contains Api and both test projects).
COPY src/AccountingLeads.Worker/ src/AccountingLeads.Worker/
COPY src/AccountingLeads.Application/ src/AccountingLeads.Application/
COPY src/AccountingLeads.Domain/ src/AccountingLeads.Domain/
COPY src/AccountingLeads.Infrastructure/ src/AccountingLeads.Infrastructure/

RUN dotnet publish src/AccountingLeads.Worker/AccountingLeads.Worker.csproj \
    -c Release \
    -o /app \
    --no-restore

# Runtime stage: `dotnet/runtime`, not `dotnet/aspnet` — this Worker is a plain
# Microsoft.Extensions.Hosting BackgroundService host with no Kestrel/ASP.NET Core dependency
# (only Microsoft.Extensions.Hosting, EF Core SqlServer, Azure.Identity, and
# Azure.Messaging.ServiceBus — all pure-managed, no native deps), so the smaller runtime-only
# image is correct and sufficient.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# Explicit rather than relying on the Generic Host's implicit "unset means Production" default
# — makes the intended environment self-documenting in the image, and matches
# AddOutboxPublishing's isDevelopment check (Program.cs) which must be false here so it keeps
# ManagedIdentityCredential in the DefaultAzureCredential chain, unlike local development.
ENV DOTNET_ENVIRONMENT=Production

# ConnectionStrings__DefaultConnection, ServiceBus__FullyQualifiedNamespace, and
# ServiceBus__QueueName are not baked into the image (appsettings.json has no defaults for
# them — only appsettings.Development.json does) and must be supplied at runtime as
# environment variables or Container Apps secrets, e.g.:
#   docker run \
#     -e ConnectionStrings__DefaultConnection="..." \
#     -e ServiceBus__FullyQualifiedNamespace="sb-accounting-leads-dev-ns1.servicebus.windows.net" \
#     -e ServiceBus__QueueName="lead-created" \
#     accounting-leads-worker

ENTRYPOINT ["dotnet", "AccountingLeads.Worker.dll"]
