# Azure Deployment Scripts

This folder contains scripts to deploy test machines to Azure for verifying ASP.NET Core functionality in final builds.

## Prerequisites

### Windows

* Latest version of PowerShell (Desktop or Core)
* [`Az` PowerShell Module](https://github.com/Azure/azure-powershell/blob/master/documentation/announcing-az-module.md#migrating-from-azurerm)
    * Install using `Install-Module -Scope CurrentUser Az`
    * If you have the older `Azure`, `AzureRM` or `AzureRM.Netcore` modules installed, you should uninstall them with `Uninstall-Module [modulename]`
    * If you see a warning about trusting the `PSGallery` repository, accept it, and/or run `Set-PSRepository PSGallery -InstallationPolicy Trusted` to trust the gallery