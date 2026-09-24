terraform {
  required_version = ">= 1.5.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.90"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.5"
    }
  }
}

provider "azurerm" {
  features {}
  
  # Hvis du vil låse direkte til et specifikt tenant eller abonnement:
  tenant_id       = var.tenant_id
  subscription_id = var.subscription_id
}

# Tilfældig streng for at sikre unikt globalt URL-navn
resource "random_string" "suffix" {
  length  = 4
  special = false
  upper   = false
}

# Ressourcegruppe dedikeret til Matador
resource "azurerm_resource_group" "rg" {
  name     = var.resource_group_name
  location = var.location

  tags = {
    Environment = "DevSandbox"
    Project     = "Matador"
    ManagedBy   = "Terraform"
  }
}

# App Service Plan (Linux)
# Standard er B1 (Basic), dækkes af $50 kreditterne og understøtter 'Always On'
resource "azurerm_service_plan" "asp" {
  name                = "asp-matador-${var.environment}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  os_type             = "Linux"
  sku_name            = var.sku_name

  tags = azurerm_resource_group.rg.tags
}

# Linux Web App til Matador .NET Core applikationen
resource "azurerm_linux_web_app" "app" {
  name                = "${var.app_name_prefix}-${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  service_plan_id     = azurerm_service_plan.asp.id

  site_config {
    always_on = var.sku_name == "F1" ? false : true

    application_stack {
      dotnet_version = "8.0" # Standard Azure Linux stack (.NET 8/9/10 kører self-contained)
    }

    cors {
      allowed_origins     = ["*"]
      support_credentials = false
    }
  }

  app_settings = {
    "ASPNETCORE_ENVIRONMENT" = "Production"
    "WEBSITE_RUN_FROM_PACKAGE" = "1"
  }

  tags = azurerm_resource_group.rg.tags
}
