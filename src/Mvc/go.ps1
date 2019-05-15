pushd C:\GitHub\AspNetCore\src\Mvc\Mvc.Analyzers\src
dotnet build

if ($lastExitCode -ne 0)
{
	popd
	return
}

cp "C:\GitHub\AspNetCore\src\Mvc\Mvc.Analyzers\src\bin\Debug\netstandard2.0\Microsoft.AspNetCore.Mvc.Analyzers.dll" "C:\Users\nimullen\.nuget\packages\microsoft.aspnetcore.mvc.analyzers\3.0.0-preview6-t000\analyzers\dotnet\cs\Microsoft.AspNetCore.Mvc.Analyzers.dll"

popd

pushd C:\Users\nimullen\Documents\Projects\AnalyzerTest

dotnet build --no-incremental
popd