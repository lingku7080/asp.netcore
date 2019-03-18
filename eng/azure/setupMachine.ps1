Install-WindowsFeature -name Web-Server -IncludeManagementTools
# $ErrorActionPreference = "Stop"

# [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# $git = git.exe
# if (![System.IO.File]::Exists($git))
# {
#     Invoke-WebRequest -Uri https://github.com/git-for-windows/git/releases/download/v2.21.0.windows.1/Git-2.21.0-64-bit.exe -OutFile git.exe
# }
# .\git.exe /SILENT

# $dotnet = dotnetInstall.exe
# if (![System.IO.File]::Exists($dotnet))
# {
#     Invoke-WebRequest -Uri https://dotnetcli.blob.core.windows.net/dotnet/Sdk/master/dotnet-sdk-latest-win-x64.exe -OutFile $dotnet
# }
# .\dotnetInstall.exe /quiet /install $

# $vscode = vscode.exe
# if (![System.IO.File]::Exists($vscode))
# {
#     Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/?Linkid=852157" -OutFile $vscode
# }
# .\vscode.exe /VERYSILENT /MERGETASKS=!runcode

# # visual studio code
# Invoke-WebRequest -Uri "https://aka.ms/vs/16/pre/vs_enterprise.exe" -OutFile vs.exe
# $InstallPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Preview"
# $InstallPath = $InstallPath.TrimEnd('\')

# # TODO the vs.json file may need to be copied to the target machine if web requests don't work
# $arguments += `
#     '--productId', "Microsoft.VisualStudio.Product.$Edition", `
#     '--installPath', "`"$InstallPath`"", `
#     '--quiet', `
#     '--norestart'

# .\vs.exe $arguments
