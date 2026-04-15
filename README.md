# Serilog-Enriched ASP.NET Core Demo

This repository demonstrates how to integrate Serilog with ASP.NET Core and enrich log events dynamically with user identity information (simulating data retrieved from an Azure AD token). It is configured to write logs to both the console and Azure Application Insights, which is a practical setup for Azure App Service deployments.

## ✨ Features
*   **Contextual Logging**: Identity properties (`UserPrincipalName`, `Role`, etc.) are automatically added to every log event during a request lifecycle.
*   **ASP.NET Core Integration**: Uses the standard modern ASP.NET Core hosting model for deployment readiness.
*   **Application Insights Sink**: Sends Serilog trace events to Azure Application Insights when a connection string is configured.
*   **Custom Enrichment**: Implements a custom Serilog enricher combined with middleware to inject dynamic data per request.

## 📋 Prerequisites
Before running this project, ensure you have the following installed:
*   [.NET 10 SDK](https://dotnet.microsoft.com/download)
*   `curl` (for testing the API locally)

## 🚀 Setup and Installation

Navigate to the project directory and install the necessary NuGet packages:

```bash
cd /Users/stu/code/serilog-demo
dotnet add package Serilog.AspNetCore --version 8.0.0
dotnet add package Serilog.Sinks.Console --version 6.1.1
dotnet add package Microsoft.ApplicationInsights.AspNetCore --version 2.23.0
dotnet add package Serilog.Sinks.ApplicationInsights --version 4.0.0
```

## Application Insights Configuration

The app uses the standard `APPLICATIONINSIGHTS_CONNECTION_STRING` setting.

For local development:

```bash
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=https://..."
dotnet run
```

For Azure App Service:

1. Open the Web App in Azure Portal.
2. Go to **Environment variables** or **Configuration**.
3. Add `APPLICATIONINSIGHTS_CONNECTION_STRING` as an application setting.
4. Restart the app after saving.

## ▶️ Running Locally

To start the web application:

```bash
dotnet run
```

The console will display messages indicating that Kestrel (the built-in web server) is running, typically on `http://localhost:5000`.

### Testing and Verification

Open a new terminal window (leaving the app running in the first one) and hit the API endpoint using `curl`:

```bash
curl http://localhost:5000/api/hello
```

#### 🔍 Log Verification
The most important step is to observe the output **in the console where `dotnet run` is executing**. You should see multiple log lines. The requests should show similar to below with `EasyAuthUserGuid` in the `customDimensions`

Example query:
```sql
union isfuzzy=true
    requests,
    dependencies
| where timestamp > datetime("2026-04-13T11:34:06.322Z")
| where * has "49eaaa-0000-4b41-a784-944432700012"
| order by timestamp desc
| take 100
```

![alt text](<CleanShot 2026-04-15 at 19.54.07@2x.png>)

This verifies that the dynamic data successfully propagated through the web framework and into Serilog's structured logging output. When `APPLICATIONINSIGHTS_CONNECTION_STRING` is present, the same events are also sent to Application Insights.

## GitHub Actions Deployment (Azure App Service)

This repo includes a workflow at [.github/workflows/deploy-azure-webapp.yml](.github/workflows/deploy-azure-webapp.yml) that deploys to:

- `https://seilogdemo-c4cngnhmb5b8dabh.canadacentral-01.azurewebsites.net`

### One-time setup

1. In GitHub, open **Settings** -> **Secrets and variables** -> **Actions**.
2. Add a repository secret named `AZURE_WEBAPP_PUBLISH_PROFILE`.
3. Open the downloaded publish profile file and paste the full XML content into that secret.
4. Do not commit the publish profile file into this repository.

### Trigger deployment

- Push to `main`, or
- Run the workflow manually from the **Actions** tab (`workflow_dispatch`).

### Notes

- The workflow builds and publishes the app, then deploys with `azure/webapps-deploy@v3` using the publish profile secret.
- If the Azure Web App is configured for a different .NET runtime stack than this project target, update either the app stack or the project target framework so they match.