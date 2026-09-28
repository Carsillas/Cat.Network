# Package consumer smoke check

This standalone `net8.0` application uses the documented C# 14 setup and only a package reference. It is intentionally outside the solution so repository project references cannot supply its generator. The declaration exercises generated `field` accessors, and the executable verifies a dirty property update through the generated serializer on the .NET 8 runtime.

Install the SDK selected by the root `global.json` and the .NET 8 SDK, then run from the repository root with PowerShell 7:

```powershell
dotnet --version
dotnet build Cat.Network.sln --configuration Release
dotnet pack Cat.Network/Cat.Network.csproj --configuration Release --no-build --output artifacts
./smoke/PackageConsumer/verify.ps1
```

The script reads the runtime project's package version and restores with a fresh cache under `artifacts/smoke-packages`. Package source mapping requires `Carsillas.Cat.Network` to come from the local `artifacts` feed; NuGet.org is available for framework targeting packs. Generated source is saved under the consumer's `obj` directory for inspection. All build, package, and cache output is ignored by Git.

CI runs this check after packing and before uploading the package artifact. Tagged publication remains gated by the existing publish job condition.
