# make sure Az powershell is installed
$ErrorActionPreference = "Stop"
# Make sure resource group is created

Try {
    $temp = Get-AzResourceGroupDeployment "AspNetCore1"
}
Catch {
    # echo that we didn't find a resource, creating one.
    New-AzResourceGroup -Name "AspNetCore1"
}

New-AzResourceGroupDeployment -ResourceGroupName "AspNetCore1" -Name "verification" -TemplateFile "AzureDeploy.json"