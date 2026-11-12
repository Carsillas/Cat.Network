# README storage consumer smoke

From the repository root, using .NET SDK 10.0.400, run:

```sh
dotnet run --project tests/ReadmeStorageSmoke/ReadmeStorageSmoke.csproj --configuration Release
```

This external consumer targets .NET 8 with C# 14, nullable analysis, and warnings treated as errors. It explicitly references the runtime, analyzer, and generator projects. It is intentionally separate from the library's test assembly so it can use only the public API.

`WorldStorage.cs` is the complete storage code block from the repository README. The executable checks the embedded sources for equality before exercising registration, lookup, attachment before and after server construction, relevancy, relay replication, and removal through both direct storage calls and client messages. Update the README and this source together. A failed check exits unsuccessfully with the failing contract.

The in-memory client completes its handshake before spawning an entity. The smoke uses only baseline public behavior and does not depend on the separate immediate-spawn handshake fix.
