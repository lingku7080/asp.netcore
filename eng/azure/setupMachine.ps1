# Install-WindowsFeature -name Web-Server -IncludeManagementTools
$ErrorActionPreference = "Stop"

# Set-ExecutionPolicy -Scope LocalMachine -ExecutionPolicy RemoteSigned -Force
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# git
$logFile = [System.IO.Path]::GetTempFileName()

Invoke-WebRequest -Uri https://github.com/git-for-windows/git/releases/download/v2.21.0.windows.1/Git-2.21.0-64-bit.exe -OutFile git.exe
.\git.exe /SILENT

Invoke-WebRequest -Uri "https://aka.ms/vs/16/pre/vs_enterprise.exe" -OutFile vs.exe
$InstallPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Preview"
$InstallPath = $InstallPath.TrimEnd('\')

# TODO the vs.json file may need to be copied to the target machine if web requests don't work
$arguments += `
    '--productId', "Microsoft.VisualStudio.Product.Enterprise", `
    '--installPath', "`"$InstallPath`"", `
    # '--in', "vs.json", `
    '--norestart', `
    '--quiet' 

.\vs.exe $arguments

Invoke-WebRequest -Uri https://dotnetcli.blob.core.windows.net/dotnet/Sdk/master/dotnet-sdk-latest-win-x64.exe -OutFile dotnetInstall.exe
.\dotnetInstall.exe  /quiet /install /log $logFile
# dotnet
# visual studio
# visual studio code
# IIS?