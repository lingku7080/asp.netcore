# Azure Deployment Scripts

This folder contains scripts to deploy test machines to Azure for verifying ASP.NET Core functionality in final builds.

## Prerequisites

### Windows

* Latest version of PowerShell (Desktop or Core)
* [`Az` PowerShell Module](https://github.com/Azure/azure-powershell/blob/master/documentation/announcing-az-module.md#migrating-from-azurerm)
    * Install using `Install-Module -Scope CurrentUser Az`
    * If you have the older `Azure`, `AzureRM` or `AzureRM.Netcore` modules installed, you should uninstall them with `Uninstall-Module [modulename]`
    * If you see a warning about trusting the `PSGallery` repository, accept it, and/or run `Set-PSRepository PSGallery -InstallationPolicy Trusted` to trust the gallery

## Editing

**NOTE**: Because these templates need to be accessible to Azure Resource Manager, when you want to modify one and test it out, you need to push your changes to GitHub and get the GitHub Raw URL (something like `https://raw.githubusercontent.com/aspnet/AspNetCore/[commit hash]/eng/azure/win2019.json`). GitHub caches raw content by branch name so if you use a branch name in `[commit hash]` it may not reflect recent commits you've made. For this reason, you should use the commit hash in these URLs.

The simplest inner-loop workflow for editing is the following:

1. Edit one of the templates.
1. Commit and push to a branch, get the commit hash.
1. Compose the necessary URL and try deploying the template.
