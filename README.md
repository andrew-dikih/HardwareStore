# HardwareStore

A web tool to compare hardware store product prices across multiple online retailers. Users describe what they need in natural language; the tool queries selected retailers and returns a normalized price comparison with a best-store recommendation.

## Project Structure

```
HardwareStore.slnx            — .NET solution
src/
  HardwareStore.Core/         — Domain models & interfaces
  HardwareStore.Infrastructure/ — CosmosDB repos, retailer clients, background services
  HardwareStore.Api/          — ASP.NET Core Web API (port 5000)
  HardwareStore.Web/          — React + Vite frontend (port 5173)
```

## Features

- **Public search**: Compare Home Depot vs Lowe's without logging in
- **Authenticated search**: Compare multiple retailers, multiple products, save history
- **Natural language input**: Describe your project in plain English
- **Async search jobs**: Background processing with status polling
- **Report history**: Browse past searches and results
- **Admin panel**: Approve/reject users, manage retailers, set per-user limits

## Getting Started

### Option A – Docker Compose (recommended)

The fastest way to start everything locally. You only need [Docker](https://docs.docker.com/get-docker/) installed.

```bash
# 1. Copy the environment template and fill in your secrets
cp .env.example .env
# Edit .env and set OPENAI_API_KEY (and optionally SMTP / Facebook values)

# 2. Build and start all services
docker compose up --build
```

| Service | URL |
|---------|-----|
| React UI | <http://localhost:5173> |
| API | <http://localhost:5000> |
| Swagger | <http://localhost:5000/swagger> |
| CosmosDB Emulator | <https://localhost:8081/_explorer/index.html> (accept the self-signed cert) |

> **First start**: The CosmosDB emulator takes ~60 s to become ready. The API waits for it automatically. If you see `Failed to initialize CosmosDB` in the logs, wait a moment and retry your request.

To stop everything:

```bash
docker compose down
```

---

### Option B – Manual Setup

#### Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0+ | Required to build and run the API |
| [Node.js](https://nodejs.org/) | 18+ | Required to run the React UI |
| [Azure Cosmos DB Emulator](https://learn.microsoft.com/azure/cosmos-db/local-emulator) | Latest | For local development without a cloud account |
| OpenAI API key | — | Used for natural-language query parsing |

### 1. Configure the API

Open `src/HardwareStore.Api/appsettings.Development.json` and fill in the required values:

```json
{
  "CosmosDb": {
    "ConnectionString": "<your CosmosDB connection string>",
    "DatabaseName": "HardwareStore",
    "ContainerName": "Documents"
  },
  "Email": {
    "SmtpHost": "<your SMTP host>",
    "SmtpPort": 587,
    "SmtpUser": "<your SMTP username>",
    "SmtpPassword": "<your SMTP password>",
    "FromEmail": "noreply@hardwarestore.example.com",
    "FromName": "HardwareStore",
    "AdminEmail": "<admin email address>",
    "EnableSsl": true
  },
  "NaturalLanguage": {
    "OpenAiApiKey": "<your OpenAI API key>",
    "OpenAiEndpoint": "https://api.openai.com/v1",
    "ModelName": "gpt-4o-mini"
  },
  "Jwt": {
    "Key": "<a random secret, at least 32 characters>",
    "Issuer": "HardwareStore",
    "Audience": "HardwareStoreUsers"
  }
}
```

> **Local CosmosDB emulator**: The emulator runs at `https://localhost:8081` with a well-known key. The default `appsettings.Development.json` already contains the emulator connection string, so no changes are needed for `CosmosDb` if you are using the emulator.

> **Email**: If you do not have an SMTP server for local testing, you can leave the email fields blank. User-approval emails will fail silently, but the rest of the app will work.

### 2. Run the API

The Vite dev server proxies all `/api` and `/hubs` requests to `http://localhost:5000`, so the API must listen on that port:

```bash
cd src/HardwareStore.Api
dotnet run --urls http://localhost:5000
# Swagger UI available at http://localhost:5000/swagger
```

### 3. Run the UI

In a separate terminal:

```bash
cd src/HardwareStore.Web
npm install
npm run dev
# App available at http://localhost:5173
```

The Vite dev server automatically proxies:
- `/api/*` → `http://localhost:5000`
- `/hubs/*` → `http://localhost:5000` (WebSocket)

## Branch Strategy

| Branch | Purpose | Merge Rules |
|--------|---------|-------------|
| `main` | Production releases | PR only · must be labeled **`release`** · CI must pass |
| `develop` | Integration branch | PR only · CI must pass |

All PRs must pass unit **and** integration tests, and unit test line coverage must be ≥ 70%.

## CI/CD

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `ci.yml` | PR → `main` or `develop` | Runs unit + integration tests; enforces 70% coverage |
| `label-check.yml` | PR → `main` | Requires the **`release`** label (marks new API version) |
| `deploy.yml` | Push to `main` | Builds and deploys to Azure Web App |
| `setup-repository.yml` | Manual (`workflow_dispatch`) | One-time repo configuration (see below) |

### One-Time Repository Setup

After merging this PR into `main`:

1. Create a GitHub fine-grained Personal Access Token (PAT) with **Administration (read & write)** permission on this repository.
2. Add it as a repository secret named **`GH_SETUP_TOKEN`** (Settings → Secrets and variables → Actions).
3. Go to **Actions → Repository Setup (One-Time)** and run the workflow (type `yes` in the confirmation field).

The workflow will:
- Create the `develop` branch from `main`
- Create the `release` label
- Apply branch protection to `main` (blocks direct push, requires PR with `release` label and passing CI)
- Apply branch protection to `develop` (blocks direct push, requires PR with passing CI)

### Azure Deployment Setup

After the one-time setup, configure the CD pipeline:

1. Create an Azure Web App for the API.
2. Add the following **repository secrets**:
   - `AZURE_CREDENTIALS` — JSON output of `az ad sp create-for-rbac --name "HardwareStore-Deploy" --role contributor --scopes /subscriptions/<sub-id>/resourceGroups/<rg> --json-auth`
3. Add the following **repository variables** (Settings → Secrets and variables → Actions → Variables):
   - `AZURE_WEBAPP_NAME` — name of your Azure Web App
   - `AZURE_RESOURCE_GROUP` — name of your Azure resource group
4. (Optional) Add a `production` environment in GitHub (Settings → Environments) with any required approval gates.

## Running Tests

```bash
# Unit tests
dotnet test tests/HardwareStore.UnitTests

# Integration tests
dotnet test tests/HardwareStore.IntegrationTests

# All tests with coverage report
dotnet test tests/HardwareStore.UnitTests --collect:"XPlat Code Coverage"
```

## Tech Stack

- **Backend**: ASP.NET Core 8, CosmosDB, MailKit, BCrypt, JWT auth
- **Frontend**: React 19, TypeScript, Vite, Tailwind CSS, React Router, Axios
- **Hosting**: Azure Web App (CI/CD via GitHub Actions)
