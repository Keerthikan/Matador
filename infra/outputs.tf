output "resource_group_name" {
  value       = azurerm_resource_group.rg.name
  description = "Navnet på den oprettede Resource Group."
}

output "web_app_name" {
  value       = azurerm_linux_web_app.app.name
  description = "Navnet på den oprettede Web App i Azure."
}

output "web_app_url" {
  value       = "https://${azurerm_linux_web_app.app.default_hostname}"
  description = "Den offentlige HTTPS URL til dit Matador-spil."
}
