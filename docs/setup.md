# Local setup

## Configuration values

All repeated-digit GUIDs and `your-*` endpoints are placeholders. Do not run the Worker until configured: it sends messages and consumes the specified queue.

1. Install .NET 10, Node.js 24, and SQL Server LocalDB on Windows.
2. Create two single-tenant Entra registrations: SPA and API. Expose `access_as_user` on the API and grant the SPA the delegated permission and required consent. Configure SPA redirect and logout URI `http://localhost:5173/accountant/login`.
3. Require assignment on the SPA and API enterprise applications; assign approved test accountants. Verify unassigned users are denied. IDs are configuration, not secrets.
4. Replace placeholder `AzureAd` values in the API's development settings with your tenant/API IDs. Configure the Worker separately with your tenant, Service Bus namespace, queue, Azure OpenAI endpoint, and deployed model name.
5. Copy `client/accounting-leads-web/.env.example` to `.env.local` in the same directory and replace tenant, SPA ID, and API scope. Keep development API port 5151 and frontend port 5173 aligned with launch settings/CORS.

For local Azure SDK access, sign into your own development tenant with Azure CLI (`az login --tenant YOUR_TENANT_ID`). Grant your developer identity queue-scoped Service Bus Data Sender and Data Receiver, plus Cognitive Services OpenAI User on your AI resource. These workload permissions are separate from accountant application assignment.

## Database and processes

From the repository root, install the EF tool if needed and apply migrations:

```sh
dotnet tool install --global dotnet-ef --version 10.0.11
dotnet ef database update --project server/src/AccountingLeads.Infrastructure --startup-project server/src/AccountingLeads.Api
dotnet run --project server/src/AccountingLeads.Api
```

In separate terminals:

```sh
cd client/accounting-leads-web
npm ci
npm run dev
```

```sh
dotnet run --project server/src/AccountingLeads.Worker
```

Open `http://localhost:5173/` for public submission and `/accountant/login` for sign-in. The API can capture leads without the Worker running; processing waits in the outbox.

## Integration verification

```sh
dotnet test server/tests/AccountingLeads.IntegrationTests/AccountingLeads.IntegrationTests.csproj
```

Tests reset a dedicated `AccountingLeadsDb_IntegrationTests` LocalDB database. Do not point the fixture at a real application database. Azure OpenAI calls and interactive Entra sign-in are not required by these tests.

For a full manual check: submit synthetic data, confirm lead/outbox persistence, observe publication and consumption, check stored analysis/qualification, then sign in as an assigned accountant. Also verify anonymous GET is denied, anonymous POST succeeds, and unassigned users cannot acquire authorized API access.

## Publish your own GitHub repository

This folder has an independent Git repository and no remote. Create an empty GitHub repository, then run these commands **from this folder**, using your own repository URL:

```sh
git add .
git commit -m "Add accounting leads architecture showcase"
git remote add origin https://github.com/YOUR_USERNAME/accounting-leads-ai.git
git push -u origin main
```

Review staged files and choose the desired license before publishing. `.env.local`, build outputs, local agent settings, and certificates are excluded. GitHub CI runs checks only; it does not deploy Azure resources.

