<#
.SYNOPSIS
    Provisions Azure infrastructure for a HardwareStore deployment environment.

.DESCRIPTION
    Creates a resource group, ACR, Cosmos DB (serverless), Container Apps environment,
    two Container Apps (API + Web), a service principal, and wires up all GitHub Actions
    secrets and variables. Optimised for low cost (scale-to-zero, serverless).

.PARAMETER EnvironmentName
    Short name for the environment, e.g. "develop", "staging", "production".
    Used to derive all resource names.

.PARAMETER SubscriptionId
    Azure subscription ID to deploy into.

.PARAMETER Location
    Azure region, e.g. "westus3", "eastus".

.PARAMETER GitHubRepo
    GitHub repository in "owner/repo" format, e.g. "andrew-dikih/HardwareStore".

.PARAMETER ResourceGroupName
    Override the derived resource group name.

.PARAMETER AcrName
    Override the derived ACR name (must be globally unique, lowercase, no hyphens).

.PARAMETER CosmosAccountName
    Override the derived Cosmos DB account name.

.PARAMETER ContainerAppsEnvName
    Override the derived Container Apps environment name.

.PARAMETER ApiAppName
    Override the derived API Container App name.

.PARAMETER WebAppName
    Override the derived Web Container App name.

.EXAMPLE
    .\provision.ps1 -EnvironmentName develop -SubscriptionId "7892b358-..." -Location westus3 -GitHubRepo "andrew-dikih/HardwareStore"
#>
param(
    [Parameter(Mandatory)][string] $EnvironmentName,
    [Parameter(Mandatory)][string] $SubscriptionId,
    [Parameter(Mandatory)][string] $Location,
    [Parameter(Mandatory)][string] $GitHubRepo,

    [string] $ResourceGroupName    = "hardwarestore-$EnvironmentName",
    [string] $AcrName              = "hardwarestore$EnvironmentName",
    [string] $CosmosAccountName    = "hardwarestore-$EnvironmentName-cosmos",
    [string] $ContainerAppsEnvName = "hardwarestore-$EnvironmentName-env",
    [string] $ApiAppName           = "hardwarestore-$EnvironmentName-api",
    [string] $WebAppName           = "hardwarestore-$EnvironmentName-web",
    [string] $SpName               = "hardwarestore-$EnvironmentName-deploy"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$az = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"

function Invoke-Az {
    & $az @args
    if ($LASTEXITCODE -ne 0) { throw "az command failed (exit $LASTEXITCODE): az $args" }
}

Write-Host "`n=== HardwareStore Azure Provisioning ===" -ForegroundColor Cyan
Write-Host "Environment : $EnvironmentName"
Write-Host "Subscription: $SubscriptionId"
Write-Host "Location    : $Location"
Write-Host "GitHub Repo : $GitHubRepo`n"

# ── 1. Register required providers ────────────────────────────────────────────
Write-Host "[ 1/9 ] Registering Azure providers..." -ForegroundColor Yellow
foreach ($ns in @("Microsoft.DocumentDB", "Microsoft.App", "Microsoft.OperationalInsights")) {
    $state = Invoke-Az provider show --namespace $ns --subscription $SubscriptionId --query registrationState -o tsv
    if ($state -ne "Registered") {
        Write-Host "  Registering $ns..."
        Invoke-Az provider register --namespace $ns --subscription $SubscriptionId
        do {
            Start-Sleep 10
            $state = Invoke-Az provider show --namespace $ns --subscription $SubscriptionId --query registrationState -o tsv
            Write-Host "  $ns : $state"
        } while ($state -ne "Registered")
    } else {
        Write-Host "  $ns already registered."
    }
}

# ── 2. Resource group ──────────────────────────────────────────────────────────
Write-Host "`n[ 2/9 ] Creating resource group '$ResourceGroupName'..." -ForegroundColor Yellow
Invoke-Az group create --name $ResourceGroupName --location $Location --subscription $SubscriptionId --output none

# ── 3. Azure Container Registry ───────────────────────────────────────────────
Write-Host "`n[ 3/9 ] Creating Container Registry '$AcrName'..." -ForegroundColor Yellow
Invoke-Az acr create --name $AcrName --resource-group $ResourceGroupName --sku Basic `
    --admin-enabled true --location $Location --subscription $SubscriptionId --output none

# ── 4. Cosmos DB account ───────────────────────────────────────────────────────
Write-Host "`n[ 4/9 ] Creating Cosmos DB account '$CosmosAccountName' (serverless)..." -ForegroundColor Yellow
Invoke-Az cosmosdb create --name $CosmosAccountName --resource-group $ResourceGroupName `
    --locations regionName=$Location --capabilities EnableServerless `
    --subscription $SubscriptionId --output none

Write-Host "  Creating database 'HardwareStore'..."
Invoke-Az cosmosdb sql database create --account-name $CosmosAccountName `
    --resource-group $ResourceGroupName --name HardwareStore `
    --subscription $SubscriptionId --output none

Write-Host "  Creating container 'Documents' (pk: /documentType)..."
Invoke-Az cosmosdb sql container create --account-name $CosmosAccountName `
    --resource-group $ResourceGroupName --database-name HardwareStore `
    --name Documents --partition-key-path /documentType `
    --subscription $SubscriptionId --output none

# ── 5. Container Apps environment ─────────────────────────────────────────────
Write-Host "`n[ 5/9 ] Creating Container Apps environment '$ContainerAppsEnvName'..." -ForegroundColor Yellow
Invoke-Az containerapp env create --name $ContainerAppsEnvName --resource-group $ResourceGroupName `
    --location $Location --subscription $SubscriptionId --output none

# ── 6 & 7. Container Apps ─────────────────────────────────────────────────────
$placeholder = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"

Write-Host "`n[ 6/9 ] Creating API Container App '$ApiAppName' (min-replicas=0)..." -ForegroundColor Yellow
$apiFqdn = Invoke-Az containerapp create `
    --name $ApiAppName --resource-group $ResourceGroupName --environment $ContainerAppsEnvName `
    --image $placeholder --target-port 5000 --ingress external `
    --min-replicas 0 --max-replicas 1 --cpu 0.25 --memory 0.5Gi `
    --subscription $SubscriptionId --query "properties.configuration.ingress.fqdn" -o tsv

Write-Host "`n[ 7/9 ] Creating Web Container App '$WebAppName' (min-replicas=0)..." -ForegroundColor Yellow
$webFqdn = Invoke-Az containerapp create `
    --name $WebAppName --resource-group $ResourceGroupName --environment $ContainerAppsEnvName `
    --image $placeholder --target-port 5173 --ingress external `
    --min-replicas 0 --max-replicas 1 --cpu 0.25 --memory 0.5Gi `
    --subscription $SubscriptionId --query "properties.configuration.ingress.fqdn" -o tsv

# ── 8. Service principal ───────────────────────────────────────────────────────
Write-Host "`n[ 8/9 ] Creating service principal '$SpName'..." -ForegroundColor Yellow
$scope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName"
$spJson = Invoke-Az ad sp create-for-rbac --name $SpName --role contributor `
    --scopes $scope --json-auth --output json | ConvertFrom-Json

# ── 9. GitHub secrets & variables ─────────────────────────────────────────────
Write-Host "`n[ 9/9 ] Configuring GitHub Actions secrets and variables..." -ForegroundColor Yellow

$cosmosKey     = Invoke-Az cosmosdb keys list --name $CosmosAccountName `
    --resource-group $ResourceGroupName --subscription $SubscriptionId `
    --query "primaryMasterKey" -o tsv
$cosmosConn    = "AccountEndpoint=https://$CosmosAccountName.documents.azure.com:443/;AccountKey=$cosmosKey"
$acrPassword   = Invoke-Az acr credential show --name $AcrName `
    --subscription $SubscriptionId --query "passwords[0].value" -o tsv
$jwtKey        = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 48 | % { [char]$_ })
$azureCreds    = $spJson | ConvertTo-Json -Compress

# Secrets
$azureCreds  | gh secret set AZURE_CREDENTIALS       --repo $GitHubRepo
$cosmosConn  | gh secret set COSMOS_CONNECTION_STRING --repo $GitHubRepo
$acrPassword | gh secret set AZURE_REGISTRY_PASSWORD  --repo $GitHubRepo
$jwtKey      | gh secret set JWT_KEY                  --repo $GitHubRepo

# Variables
gh variable set AZURE_RESOURCE_GROUP   --body $ResourceGroupName           --repo $GitHubRepo
gh variable set AZURE_REGISTRY_URL     --body "$AcrName.azurecr.io"        --repo $GitHubRepo
gh variable set AZURE_REGISTRY_USERNAME --body $AcrName                    --repo $GitHubRepo
gh variable set AZURE_SUBSCRIPTION_ID  --body $SubscriptionId              --repo $GitHubRepo
gh variable set AZURE_API_APP_NAME     --body $ApiAppName                  --repo $GitHubRepo
gh variable set AZURE_WEB_APP_NAME     --body $WebAppName                  --repo $GitHubRepo

# ── Summary ───────────────────────────────────────────────────────────────────
Write-Host "`n=== Provisioning Complete ===" -ForegroundColor Green
Write-Host "Resource Group : $ResourceGroupName"
Write-Host "ACR            : $AcrName.azurecr.io"
Write-Host "Cosmos DB      : $CosmosAccountName"
Write-Host "API URL        : https://$apiFqdn"
Write-Host "Web URL        : https://$webFqdn"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Update deploy.yml trigger branch to match your environment branch"
Write-Host "  2. Update the environment.url and VITE_API_URL FQDNs in deploy.yml:"
Write-Host "       API: https://$apiFqdn"
Write-Host "       Web: https://$webFqdn"
Write-Host "  3. Add 'https://$webFqdn' to AllowedOrigins in appsettings.json"
Write-Host "  4. Ensure vite.config.ts has server.allowedHosts: 'all'"
Write-Host ""
Write-Host "JWT_KEY (store securely): $jwtKey" -ForegroundColor DarkYellow
