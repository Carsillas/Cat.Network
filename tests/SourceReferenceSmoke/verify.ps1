$ErrorActionPreference = 'Stop'
# Negative builds are inspected below instead of being treated as PowerShell errors.
$PSNativeCommandUseErrorActionPreference = $false

function Invoke-SmokeDotnet {
    param(
        [string[]] $Arguments,
        [string[]] $ExpectedDiagnostics = @()
    )

    $output = & dotnet @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $buildText = $output -join [Environment]::NewLine
    Write-Host $buildText

    if ($ExpectedDiagnostics.Count -eq 0) {
        if ($exitCode -ne 0) {
            throw "dotnet $($Arguments -join ' ') failed with exit code $exitCode."
        }
        return
    }

    if ($exitCode -eq 0) {
        throw "Expected a failed build containing $($ExpectedDiagnostics -join ', ')."
    }
    foreach ($diagnostic in $ExpectedDiagnostics) {
        if ($buildText -notmatch "\berror ${diagnostic}:") {
            throw "The negative build did not report $diagnostic."
        }
    }
}

Push-Location $PSScriptRoot
try {
    $sdkVersion = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^10\.0\.4\d{2}$') {
        throw "Run this fixture with a stable .NET SDK 10.0.4xx (tested: 10.0.400; selected: $sdkVersion)."
    }

    $buildArguments = @('build', 'SourceReferenceSmoke.csproj', '--configuration', 'Release', '--no-incremental')

    Invoke-SmokeDotnet -Arguments ($buildArguments + '-p:SourceReferenceSmokeRuntimeOnly=true') `
        -ExpectedDiagnostics @('CS9248', 'CS0534')

    Invoke-SmokeDotnet -Arguments ($buildArguments + @('--no-restore', '-p:SourceReferenceSmokeInvalidDeclaration=true')) `
        -ExpectedDiagnostics @('CN0001')

    Invoke-SmokeDotnet -Arguments ($buildArguments + '--no-restore')
    Invoke-SmokeDotnet -Arguments @('run', '--project', 'SourceReferenceSmoke.csproj', '--configuration', 'Release', '--no-build', '--no-restore')
    Write-Host 'Source-reference smoke checks passed.'
}
finally {
    Pop-Location
}
