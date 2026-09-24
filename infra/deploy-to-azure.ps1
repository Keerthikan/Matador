# PowerShell deploy script til Azure Web App
param (
    [string]$ResourceGroupName = "rg-matador-sandbox"
)

$ErrorActionPreference = "Stop"
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Deployer Matador.Web til Azure       " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Hent Web App navn fra Terraform outputs hvis muligt
$infraPath = Join-Path $PSScriptRoot ""
$webAppName = ""

if (Test-Path "$infraPath\terraform.tfstate") {
    Write-Host "Læser Web App navn fra Terraform state..." -ForegroundColor Yellow
    $webAppName = (terraform -chdir="$infraPath" output -raw web_app_name 2>$null)
}

if ([string]::IsNullOrWhiteSpace($webAppName)) {
    $webAppName = Read-Host "Indtast navnet på din Azure Web App (f.eks. matador-game-xxxx)"
}

Write-Host "Udruller til Azure Web App: $webAppName i $ResourceGroupName" -ForegroundColor Green

# 2. Publicer Matador.Web som zip
$publishDir = "c:\work\Matador\src\Matador.Web\bin\Release\net10.0\publish"
$zipPath = "c:\work\Matador\infra\matador-deploy.zip"

Write-Host "Bygger og publicerer .NET projektet..." -ForegroundColor Yellow
dotnet publish "c:\work\Matador\src\Matador.Web\Matador.Web.csproj" -c Release -o "$publishDir"

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "Pakker filer til $zipPath..." -ForegroundColor Yellow
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

# 3. Deploy til Azure
Write-Host "Deployer pakke til Azure Web App via Azure CLI..." -ForegroundColor Yellow
az webapp deploy --resource-group $ResourceGroupName --name $webAppName --src-path $zipPath --type zip

Write-Host "`n✅ DEPLOYMENT GENNEMFØRT!" -ForegroundColor Green
$appUrl = (az webapp show --resource-group $ResourceGroupName --name $webAppName --query defaultHostName -o tsv)
Write-Host "Dit spil er nu live på: https://$appUrl" -ForegroundColor Cyan
