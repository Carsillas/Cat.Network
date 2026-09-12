param([string] $Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $PSScriptRoot 'PackageConsumer.csproj'
# A new cache ensures that repeated local runs consume the newly packed binaries.
$packages = Join-Path $repository ('artifacts/smoke-packages/' + [Guid]::NewGuid().ToString('N'))

Push-Location $repository
try {
    $version = dotnet msbuild Cat.Network/Cat.Network.csproj -getProperty:PackageVersion "-p:Configuration=$Configuration"
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the local package version.' }

    dotnet restore $project "-p:CatNetworkPackageVersion=$version" --packages $packages --configfile (Join-Path $PSScriptRoot 'NuGet.Config')
    if ($LASTEXITCODE -ne 0) { throw 'Package consumer restore failed.' }

    dotnet run --project $project --configuration $Configuration --no-restore "-p:CatNetworkPackageVersion=$version"
    if ($LASTEXITCODE -ne 0) { throw 'Package consumer failed.' }
} finally {
    Pop-Location
}
