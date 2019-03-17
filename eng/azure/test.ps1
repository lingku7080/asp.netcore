# make sure Az powershell is installed
$ErrorActionPreference = "Stop"
# Make sure resource group is created

Try {
    $temp = Get-AzResourceGroupDeployment "AspNetCoreVerification"
}
Catch {
    # echo that we didn't find a resource, creating one.
    New-AzResourceGroup -Name "AspNetCoreVerification"
}

#
New-AzResourceGroupDeployment -ResourceGroupName "AspNetCoreVerification" -Name "test" -TemplateUri "https://raw.githubusercontent.com/aspnet/AspNetCore/jkotalik/customScript/eng/azure/AzureDeploy.json"