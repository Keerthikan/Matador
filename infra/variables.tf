variable "tenant_id" {
  type        = string
  default     = null # Sæt f.eks. "98883028-2636-4c61-ac34-82a7a6ef1a1d" hvis du vil låse den fast
  description = "Valgfrit: Azure Tenant ID."
}

variable "subscription_id" {
  type        = string
  default     = null # Sæt dit Visual Studio Subscription ID her for at være 100% sikker
  description = "Valgfrit: Azure Subscription ID for dine $50 kreditter."
}

variable "resource_group_name" {
  type        = string
  default     = "rg-matador-sandbox"
  description = "Navnet på den dedikerede Azure Resource Group."
}

variable "location" {
  type        = string
  default     = "westeurope"
  description = "Azure datacenter region (f.eks. westeurope eller northeurope)."
}

variable "environment" {
  type        = string
  default     = "dev"
  description = "Miljø-navn."
}

variable "app_name_prefix" {
  type        = string
  default     = "matador-game"
  description = "Præfiks for Web App navnet (bliver til f.eks. matador-game-xxxx.azurewebsites.net)."
}

variable "sku_name" {
  type        = string
  default     = "B1" # Kan ændres til "F1" hvis du vil have den 100% gratis plan uden Always On
  description = "App Service Plan SKU: 'B1' (Basic, dækket af $50 kreditterne med Always On) eller 'F1' (Free tier)."
}
