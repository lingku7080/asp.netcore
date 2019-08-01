# dotnet-microsoft.openapi

`Microsoft.dotnet-openapi` is a tool which can be used to manage OpenAPI references within your project.

## Commands

### Add Commands

<!-- TODO: Restore after https://github.com/aspnet/AspNetCore/issues/12738
 #### Add Project

##### Options

| Short option | Long option | Description | Example |
|-------|------|-------|---------|
| -v|--verbose | Show verbose output. |dotnet microsoft.openapi add project *-v* ../Ref/ProjRef.csproj |
| -p|--project | The project to operate on. |dotnet microsoft.openapi add project *--project .\Ref.csproj* ../Ref/ProjRef.csproj |

##### Arguments

|  Argument  | Description | Example |
|-------------|-------------|---------|
| source-file | The source to create a reference from. Must be a project file. |dotnet microsoft.openapi add project *../Ref/ProjRef.csproj* | -->

#### Add File

##### Options

| Short option| Long option| Description | Example |
|-------|------|-------|---------|
| -v|--verbose | Show verbose output. |dotnet microsoft.openapi add file *-v* .\openapi.json |
| -p|--project | The project to operate on. |dotnet microsoft.openapi add file *--project .\Ref.csproj* .\openapi.json |

##### Arguments

|  Argument  | Description | Example |
|-------------|-------------|---------|
| source-file | The source to create a reference from. Must be an openapi file. |dotnet microsoft.openapi add file *.\openapi.json* |

#### Add URL

##### Options

| Short option| Long option| Description | Example |
|-------|------|-------------|---------|
| -v|--verbose | Show verbose output. |dotnet microsoft.openapi add url *-v* <http://contoso.com/openapi.json> |
| -p|--project | The project to operate on. |dotnet microsoft.openapi add url *--project .\Ref.csproj* <http://contoso.com/openapi.json> |
| -o|--output-file | The file to create a local copy of. |dotnet microsoft.openapi add url <https://contoso.com/openapi.json> *--output-file myclient.json* |

##### Arguments

|  Argument  | Description | Example |
|-------------|-------------|---------|
| source-file | The source to create a reference from. Must be a URL. |dotnet microsoft.openapi add url <https://contoso.com/openapi.json> |

### Remove

##### Options

| Short option| Long option| Description| Example |
|-------|------|------------|---------|
| -v|--verbose | Show verbose output. |dotnet microsoft.openapi remove *-v*|
| -p|--project | The project to operate on. |dotnet microsoft.openapi remove *--project .\Ref.csproj* .\openapi.json |

#### Arguments

|  Argument  | Description| Example |
| ------------|------------|---------|
| source-file | The source to remove the reference to. |dotnet microsoft.openapi remove *.\openapi.json* |

### Refresh

#### Options

| Short option| Long option| Description | Example |
|-------|------|-------------|---------|
| -v|--verbose | Show verbose output. | dotnet microsoft.openapi refresh *-v* <https://contoso.com/openapi.json> |
| -p|--project | The project to operate on. | dotnet microsoft.openapi refresh *--project .\Ref.csproj* <https://contoso.com/openapi.json> |

#### Arguments

|  Argument  | Description | Example |
| ------------|-------------|---------|
| source-file | The URL to refresh the reference from. | dotnet microsoft.openapi refresh *<https://contoso.com/openapi.json*> |
