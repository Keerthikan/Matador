# Deploy Matador til Azure

Denne mappe indeholder et færdigt PowerShell deploy-script, som først publicerer .NET applikationen til en zip-pakke og derefter udruller den til din Azure Web App.

## Forudsætninger:
1. Azure CLI (`az`)
2. Terraform CLI (`terraform`)

## 1. Vælg Tenant og Dit $50 Visual Studio Abonnement:
Hvis du har flere konti eller organisationer, sikrer du dig, at du rammer det rigtige sted:

```powershell
# 1. Log ind på din specifikke Logos tenant:
az login --tenant "98883028-2636-4c61-ac34-82a7a6ef1a1d"

# 2. Se dine tilgængelige abonnementer:
az account list --output table

# 3. Sæt dit personlige Visual Studio abonnement som aktivt:
az account set --subscription "DIT-SUBSCRIPTION-NAVN-ELLER-ID"
```

*(Valgfrit: Du kan også oprette en `infra/terraform.tfvars` fil og skrive `tenant_id = "..."` og `subscription_id = "..."` så Terraform automatisk låser sig fast).*

## 2. Opret infrastrukturen med Terraform:
```powershell
cd c:\work\Matador\infra
terraform init
terraform apply
```
*(Når Terraform er færdig, udskriver den din nye URL, f.eks. `https://matador-game-xxxx.azurewebsites.net`).*

## 3. Deploy .NET appen til Azure:
Kør deploy-scriptet fra roden af Matador:
```powershell
cd c:\work\Matador
.\infra\deploy-to-azure.ps1
```
Scriptet pakker `Matador.Web` og sender det direkte op til din Azure Web App via Azure CLI.
