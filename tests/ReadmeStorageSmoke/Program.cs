using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Cat.Network;

// Compile the same storage implementation the README shows, and fail if the copies drift.
Match sample = Regex.Match(ReadResource("RepositoryReadme.md"),
	"```csharp\n(?<code>[^`]*public sealed class WorldStorage[^`]*)\n```");
Require(sample.Success && sample.Groups["code"].Value.Trim() == ReadResource("WorldStorage.cs").Trim(),
	"WorldStorage.cs must match the complete README storage code block.");

TypeCatalogue catalogue = new();
catalogue.Register(typeof(PlayerProfile));
catalogue.Register(typeof(ProjectileEntity));

WorldStorage storage = new();
ProjectileEntity beforeServer = new() { Damage = 17 };
Require(storage.RegisterEntity(beforeServer), "Registration before server construction failed.");
Require(beforeServer.Id != Guid.Empty && !beforeServer.IsSpawned,
	"Registration should assign an ID while leaving the entity detached until a server exists.");
Require(!storage.RegisterEntity(beforeServer), "Duplicate detached registration should return false.");
if (!storage.TryGetEntity(beforeServer.Id, out NetworkEntity? found) || found.Id != beforeServer.Id) {
	throw new InvalidOperationException("Lookup failed; a successful lookup must return a non-null entity.");
}
Require(ReferenceEquals(found, beforeServer), "Lookup must preserve the registered instance.");

IDaemon daemon = new MemoryRelayDaemon(() => new PlayerProfile());
RelayServer server = new(daemon, catalogue, storage);
server.Tick();
Require(beforeServer.IsSpawned && ReferenceEquals(beforeServer.Peer, server),
	"Server construction must attach pre-registered entities through GetRegisteredEntities.");

ProjectileEntity afterServer = new() { Damage = 42 };
Require(storage.RegisterEntity(afterServer) && ReferenceEquals(afterServer.Peer, server),
	"Registration after server construction must attach the entity through the base class.");
List<NetworkEntity> relevant = [beforeServer];
storage.PopulateRelevantEntities(new PlayerProfile(), relevant);
Require(relevant.Count == 3 && ReferenceEquals(relevant[0], beforeServer) &&
	relevant.Exists(entity => ReferenceEquals(entity, afterServer)),
	"Relevancy must append all stored entities to the caller's results.");

RelayClient client = new(catalogue);
client.Connect(((MemoryRelayDaemon)daemon).Connect());
PumpUntil(() => client.Profile is not null, "Client handshake did not complete.");
PumpUntil(() => client.TryGetEntity(beforeServer.Id, out _) && client.TryGetEntity(afterServer.Id, out _),
	"The relay did not publish all entities returned by storage relevancy.");
Require(client.TryGetEntity(afterServer.Id, out NetworkEntity? observed) &&
	observed is ProjectileEntity { Damage: 42 } && !ReferenceEquals(observed, afterServer),
	"The client should receive a deserialized entity with its generated property intact.");

Require(storage.UnregisterEntity(beforeServer.Id), "Removal of a stored entity failed.");
Require(!beforeServer.IsSpawned && !storage.TryGetEntity(beforeServer.Id, out NetworkEntity? missing) && missing is null,
	"Removal must clear both the backing lookup and the base-owned peer attachment.");
Require(!storage.UnregisterEntity(beforeServer.Id), "Removing an unknown ID should return false.");
relevant.Clear();
storage.PopulateRelevantEntities(new PlayerProfile(), relevant);
Require(relevant.Count == 1 && ReferenceEquals(relevant[0], afterServer),
	"Removed entities must no longer be relevant.");
PumpUntil(() => !client.TryGetEntity(beforeServer.Id, out _),
	"The relay did not delete an entity removed from storage.");
Require(client.TryGetEntity(afterServer.Id, out _), "Removing one entity should preserve the other.");

// Complete the baseline handshake before Spawn so this check isolates storage integration.
ProjectileEntity spawned = new() { Damage = 63 };
client.Spawn(spawned);
PumpUntil(() => storage.TryGetEntity(spawned.Id, out _), "Client spawn did not reach WorldStorage.");
Require(storage.TryGetEntity(spawned.Id, out NetworkEntity? received) &&
	received is ProjectileEntity { Damage: 63 } && ReferenceEquals(received.Peer, server),
	"Client-created entities must be registered and attached through the storage base class.");
client.Delete(spawned);
PumpUntil(() => !storage.TryGetEntity(spawned.Id, out _), "Client deletion did not remove the stored entity.");
Require(received is not null && !received.IsSpawned, "Client deletion must detach the server entity.");

Console.WriteLine("README storage smoke passed: exact sample, registration, lookup, attachment, relevancy, relay replication, and removal.");

void PumpUntil(Func<bool> condition, string message) {
	for (int tick = 0; tick < 8 && !condition(); tick++) {
		server.Tick();
		client.Tick();
	}

	Require(condition(), message);
}

static string ReadResource(string name) {
	using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
		?? throw new InvalidOperationException($"Missing embedded source: {name}");
	using StreamReader reader = new(stream);
	return reader.ReadToEnd().Replace("\r\n", "\n");
}

static void Require(bool condition, string message) {
	if (!condition) {
		throw new InvalidOperationException(message);
	}
}

[NetworkObject]
public partial class PlayerProfile : NetworkProfile {
	[NetworkProperty]
	public partial string DisplayName { get; set; } = string.Empty;
}

[NetworkObject]
public partial class ProjectileEntity : NetworkEntity {
	[NetworkProperty]
	public partial int Damage { get; set; }
}
