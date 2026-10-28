---
name: azure-container-apps-provision
description: Provisions Azure infrastructure for a new HardwareStore deployment environment (resource group, Container Registry, Cosmos DB serverless, Container Apps for API and Web, an OIDC deploy identity) and wires up GitHub Actions secrets and variables. Use when asked to provision, set up, or create a new Azure deployment environment for HardwareStore, or to add Easy Auth to one of its Container Apps.
---

# Azure Container Apps Provisioning – HardwareStore

Provision a complete Azure environment for a HardwareStore deployment target (e.g. `develop`,
`staging`, `production`). Low cost by default: Cosmos DB serverless, Container Apps consumption plan
with min-replicas=0 (scale to zero), ACR Basic.

## Prerequisites

- Azure CLI (`az`) logged in (`az login --tenant <tenant-id>`) and GitHub CLI (`gh`) logged in.
- The caller supplies: environment name, Azure subscription ID, Azure region, GitHub repo. Never
  hard-code a subscription ID in this skill or the script; pass it as `-SubscriptionId` or set
  `$env:AZURE_SUBSCRIPTION_ID`.

## Run

```powershell
.\provision.ps1 `
  -EnvironmentName  "develop" `
  -SubscriptionId   $env:AZURE_SUBSCRIPTION_ID `
  -Location         "westus3" `
  -GitHubRepo       "andrew-dikih/HardwareStore"
```

Resource names derive from the environment name; each can be overridden (see the script's parameters).

## What the script provisions

1. Provider registration: `Microsoft.DocumentDB`, `Microsoft.App`, `Microsoft.OperationalInsights`.
2. Resource group `hardwarestore-<env>`.
3. ACR `hardwarestore<env>` (Basic, admin enabled).
4. Cosmos DB `hardwarestore-<env>-cosmos` (serverless, SQL API): database `HardwareStore`, container
   `Documents` partitioned by `/documentType`.
5. Container Apps environment `hardwarestore-<env>-env`.
6. Container App API `hardwarestore-<env>-api` (port 5000) and Web `hardwarestore-<env>-web` (port 5173):
   external ingress, min 0 / max 1 replicas, 0.25 CPU / 0.5Gi.
7. **Deploy identity via OIDC (no client secret)**: app registration `hardwarestore-<env>-deploy` with a
   federated credential for `repo:<owner/repo>:environment:<env>`, Contributor on the resource group only.
8. GitHub Actions secrets `COSMOS_CONNECTION_STRING`, `AZURE_REGISTRY_PASSWORD`, `JWT_KEY`; variables
   `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`,
   `AZURE_REGISTRY_URL`, `AZURE_REGISTRY_USERNAME`, `AZURE_API_APP_NAME`, `AZURE_WEB_APP_NAME`.

## Auth rule: OIDC, never a service-principal secret

Do not run `az ad sp create-for-rbac --json-auth` or store `AZURE_CREDENTIALS`. Workflows log in with:

```yaml
permissions:
  id-token: write
  contents: read
steps:
  - uses: azure/login@v3
    with:
      client-id: ${{ vars.AZURE_CLIENT_ID }}
      tenant-id: ${{ vars.AZURE_TENANT_ID }}
      subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
```

The job must declare the matching `environment:` (the federated subject is environment-scoped).

## After provisioning

- In `deploy.yml`: change the `on.push.branches` trigger and the `environment.url` / hard-coded FQDNs to the
  new Container App URLs (the script prints them), and replace any `creds: ${{ secrets.AZURE_CREDENTIALS }}`
  login with the OIDC login above.
- Add the new Web FQDN to `AllowedOrigins` in `appsettings.json`.

## Easy Auth (Microsoft provider) on a Container App

- Easy Auth requests `response_type=code id_token`, so the app registration must issue ID tokens or sign-in
  fails right after the Microsoft login page:
  `az ad app update --id <appId> --enable-id-token-issuance true`.
- Before handing anyone a link, `curl -I` it and expect 302/200, not 404 (a route may need a path segment).

## Notes

- The Web container runs the Vite dev server (`npm run dev`); `vite.config.ts` needs
  `server.allowedHosts: 'all'` so the Container App FQDN is not blocked.
- `CosmosDb__AllowInsecure` must be `false` in all cloud environments (`true` only for the local emulator).
- The generated JWT key is printed once at the end of the script; store it securely.
- On Windows `az` is `az.cmd`, so cmd.exe re-parses arguments: keep JMESPath `--query` strings free of
  parentheses.
