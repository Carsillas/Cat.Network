# Source-reference smoke fixture

This standalone consumer is deliberately outside `Cat.Network.sln`. It exercises source project references as a consumer would, without using the library's test helpers or prebuilt binaries. Both the consumer and runtime target `net8.0`; the consumer explicitly selects C# 14.

Use a stable .NET SDK in the **10.0.4xx** feature band, the .NET 8 runtime, and PowerShell 7. This fixture was tested with **10.0.400**. The verification script permits patch updates within that band and rejects older SDKs, other feature bands/major versions, and preview SDKs before building. From the checkout root, run:

```powershell
pwsh -File tests/SourceReferenceSmoke/verify.ps1
```

The command verifies three cases:

1. Referencing only `Cat.Network.csproj` fails with **CS9248** (unimplemented partial property) and **CS0534** (unimplemented `Clone()`), even with the correct compiler.
2. Explicitly referencing the runtime, analyzer, and generator rejects an otherwise valid C# subclass missing `[NetworkObject]` with **CN0001**, proving that the consumer's analyzer runs.
3. The valid declaration builds and runs, checking that the generated property and covariant `Clone()` copy values into an independent instance.

The two negative builds are expected. The script fails if either expected diagnostic is absent or if the positive build/run fails. Build output stays in the usual ignored `bin` and `obj` directories; the final build restores the valid configuration.

To build and run only the valid consumer, use the same SDK and runtime:

```powershell
dotnet run --project tests/SourceReferenceSmoke/SourceReferenceSmoke.csproj --configuration Release
```

All three `ProjectReference` paths in `SourceReferenceSmoke.csproj` are relative to that file: `../../` reaches the checkout root. The `SourceReferenceSmokeRuntimeOnly` and `SourceReferenceSmokeInvalidDeclaration` properties exist only to select the negative checks; a normal consumer needs neither property nor the conditional reference group.
