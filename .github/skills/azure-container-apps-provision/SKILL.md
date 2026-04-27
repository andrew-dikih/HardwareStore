---
name: azure-container-apps-provision
description: Provisions Azure infrastructure for a new HardwareStore deployment environment (resource group, Container Registry, Cosmos DB serverless, Container Apps for API and Web, service principal) and wires up GitHub Actions secrets and variables. Use this skill when asked to provision, set up, or create a new Azure deployment environment for HardwareStore.
allowed-tools: shell
---

# Azure Container Apps Provisioning – HardwareStore

Use this skill to provision a complete Azure environment for a HardwareStore deployment target
(e.g. `develop`, `staging`, `production`). All resources are optimised for low cost by default:
Cosmos DB serverless, Container Apps consumption plan with min-replicas=0 (scale to zero), and
ACR Basic SKU.

## Prerequisites

- Azure CLI (`az`) installed and authenticated (`az login --tenant <tenant-id>`)
- GitHub CLI (`gh`) installed and authenticated
- The caller must provide: environment name, Azure subscription ID, Azure region, and GitHub repo

## Steps to Run

Run the `provision.ps1` script from this skill's directory:

```powershell
.\provision.ps1 `
  -EnvironmentName  "develop" `
  -SubscriptionId   "7892b358-b4c1-416f-8b7d-e051911108cf" `
  -Location         "westus3" `
  -GitHubRepo       "andrew-dikih/HardwareStore"
```

The script derives all resource names automatically from the environment name, but each can be
overridden individually if needed (see parameter list in the script).

## What the Script Provisions

1. **Required provider registration** – `Microsoft.DocumentDB`, `Microsoft.App`,
   `Microsoft.OperationalInsights` (skipped if already registered)
2. **Resource group** – `hardwarestore-<env>`
3. **Azure Container Registry** – `hardwarestore<env>` (Basic SKU, admin enabled)
4. **Cosmos DB account** – `hardwarestore-<env>-cosmos` (serverless, SQL API)
   - Database: `HardwareStore`
   - Container: `Documents` partitioned by `/documentType`
5. **Container Apps environment** – `hardwarestore-<env>-env` (consumption plan)
6. **Container App – API** – `hardwarestore-<env>-api`
   - Port 5000, external ingress, min-replicas=0, max-replicas=1, 0.25 CPU / 0.5Gi
7. **Container App – Web** – `hardwarestore-<env>-web`
   - Port 5173, external ingress, min-replicas=0, max-replicas=1, 0.25 CPU / 0.5Gi
8. **Service principal** – `hardwarestore-<env>-deploy` (Contributor on resource group)
9. **GitHub Actions secrets** – `AZURE_CREDENTIALS`, `COSMOS_CONNECTION_STRING`,
   `AZURE_REGISTRY_PASSWORD`, `JWT_KEY`
10. **GitHub Actions variables** – `AZURE_RESOURCE_GROUP`, `AZURE_REGISTRY_URL`,
    `AZURE_REGISTRY_USERNAME`, `AZURE_SUBSCRIPTION_ID`, `AZURE_API_APP_NAME`,
    `AZURE_WEB_APP_NAME`

## After Provisioning

Once the script finishes, update `deploy.yml` if deploying to a new environment:
- Change the `on.push.branches` trigger to the new branch name
- Update the `environment.url` and hardcoded FQDNs to the new Container App URLs
  (the script prints these at the end)
- Update `AllowedOrigins` in `appsettings.json` with the new Web Container App URL

## Notes

- The Vite dev server (`npm run dev`) is used in the Web container. Ensure `vite.config.ts`
  has `server.allowedHosts: 'all'` so the Container App FQDN is not blocked.
- `CosmosDb__AllowInsecure` must be `false` in all cloud environments (only `true` for the
  local emulator).
- The generated JWT key is printed once at the end of the script. Store it securely.
