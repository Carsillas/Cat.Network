using System.Collections.Immutable;
using System.Reflection;
using Cat.Network.Analyzer;
using Cat.Network.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Generator.Test;

public sealed class IdentifierGenerationTests {
	[TestCase("RPC", false)]
	[TestCase("RPC", true)]
	[TestCase("Broadcast", false)]
	[TestCase("Broadcast", true)]
	public async Task MessageParameters_CompileAndArriveThroughLocalAndRemoteDispatch(string kind, bool explicitReceive) {
		string[] names = [
			"data", "writer", "client", "instigator", "context", "rpcId", "@event", "@class",
			"client1", "instigator1", "writer1", "context1", "data1", "rpcId1",
			"__catNetworkParameter0LengthRange", "__catNetworkParameter0ValueStart",
			"__catNetworkParameter0ByteCount", "__catNetworkParameter0ValueData",
			"__catNetworkHasValue", "__catNetworkdataValue", "__catNetworkStructValue",
			"__catNetworkStringByteCount", "__catNetworkStructFieldName", "hasNameValue",
			"networkObject", "serializer", "__catNetworkObjectUpdateMode", "__catNetworkReplacementTypeId",
			"__catNetworkReplacementType", "__catNetworkReplacementSerializer", "__catNetworkReplacementTarget",
			"Peer", "IsOwner", "GetNetworkObjectTypeId", "eventReceived"
		];
		string parameters = string.Join(", ", names.Select(name => "int? " + name));
		string arguments = string.Join(", ", Enumerable.Range(1, names.Length));
		string expected = string.Join(",", Enumerable.Range(1, names.Length));
		string received = "string.Join(\",\", new int?[] { " + string.Join(", ", names) + " })";
		string receiveParameters = "RelayClient receiver, NetworkProfile caller, " + parameters;
		string record = $"Values.Add({received}); Clients.Add(receiver); Callers.Add(caller.Id);";
		string implementation = explicitReceive
			? $"void Entity.{kind}.@event({receiveParameters}) {{ {record} }}"
			: $"public Entity() {{ eventReceived += ({receiveParameters}) => {{ {record} }}; }}";
		string source = $$"""
			[NetworkObject] public partial class Entity : NetworkEntity {
				public readonly List<string> Values = new();
				public readonly List<RelayClient> Clients = new();
				public readonly List<Guid> Callers = new();
				[{{kind}}{{(explicitReceive ? "(NetworkMessageReceiveMode.Explicit)" : "")}}]
				public partial void @event({{parameters}});
				{{implementation}}
			}
			public static class Probe {
				public static void Run() {
					var (server, owner, remote, entity, proxy) = Helper.Connect();
					entity.@event({{arguments}});
					Helper.Check(entity.Values.SequenceEqual(new[] { "{{expected}}" }), "local values");
					Helper.Check(ReferenceEquals(entity.Clients[0], owner) && entity.Callers[0] == owner.Profile!.Id, "local context");
					{{(kind == "RPC" ? $"proxy.@event({arguments});" : "")}}
					Helper.Pump(server, owner, remote);
					var destination = {{(kind == "RPC" ? "entity" : "proxy")}};
					Helper.Check(destination.Values.Count == {{(kind == "RPC" ? 2 : 1)}} && destination.Values[^1] == "{{expected}}", "remote values");
					Helper.Check(ReferenceEquals(destination.Clients[^1], {{(kind == "RPC" ? "owner" : "remote")}}), "receiver client");
					Helper.Check(destination.Callers[^1] == {{(kind == "RPC" ? "remote" : "owner")}}.Profile!.Id, "instigator profile");
					// Message IDs still use the raw method name and original parameter types.
					string signature = "event(" + string.Join(",", Enumerable.Repeat("global::System.Int32?", {{names.Length}})) + ")";
					ulong id = BitConverter.ToUInt64(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(signature)));
					var bytes = new BufferWriter();
					for (int i = 1; i <= {{names.Length}}; i++) { bytes.WriteInt32(5); bytes.WriteByte(1); bytes.WriteInt32(i); }
					var direct = new Entity();
					Helper.Check(((INetworkRpcTarget)direct).TryInvoke{{(kind == "RPC" ? "Rpc" : "Broadcast")}}(remote, owner.Profile!, id, bytes.GetWrittenSpan(), new(Helper.Catalogue())), "stable message ID");
					Helper.Check(direct.Values.Single() == "{{expected}}", "independent wire payload");
				}
			}
			""";
		await CompileAndRun(source);
	}

	[Test]
	public async Task EscapedPropertiesAndCollections_KeepWireNamesAndRoundTrip() {
		await CompileAndRun("""
			[NetworkObject] public partial class Entity : NetworkEntity {
				[NetworkProperty] public partial int @event { get; set; }
				[NetworkProperty] public partial int? @class { get; set; }
				[NetworkCollection] public partial NetworkList<int> @foreach { get; }
			}
			public static class Probe {
				public static void Run() {
					var catalogue = Helper.Catalogue();
					var source = new Entity { @event = 41, @class = 42 };
					source.@foreach.Add(43);
					string? changedName = null;
					var destination = new Entity();
					destination.eventChanged += (_, args) => changedName = args.Name;
					byte[] bytes = Helper.RoundTrip(source, destination, catalogue);
					Helper.Check(destination.@event == 41 && destination.@class == 42 && destination.@foreach.Single() == 43, "escaped property values");
					Helper.Check(changedName == "event", "property event wire name");
					Helper.Check(((INetworkObject)source).NetworkProperties.Select(p => p.Name).ToHashSet().IsSupersetOf(new[] { "class", "event", "foreach" }), "metadata names");
					Helper.Check(NetworkObjectUpgradeReader.TryCreate(bytes.AsSpan(2), new(catalogue), out var reader), "name payload");
					Helper.Check(reader.Get<int>("event") == 41 && reader.Get<int?>("class") == 42, "wire names without escape markers");
					var clone = source.Clone();
					Helper.Check(clone.@event == 41 && clone.@class == 42 && source.Equals(destination), "clone and equality");
				}
			}
			""");
	}

	[Test]
	public async Task RepeatedNestedNullableFieldNames_RoundTripPropertiesAndMessages() {
		await CompileAndRun("""
			public struct Inner { public int? Name; public string @event; }
			public struct Outer { public Inner? Name; public Inner? Name__Name; public int? @class; }
			[NetworkObject] public partial class Payload : NetworkObject { [NetworkProperty] public partial int Value { get; set; } }
			[NetworkObject] public partial class Entity : NetworkEntity {
				[NetworkProperty] public partial Outer? Value { get; set; }
				[RPC] public partial void Send(Outer? __catNetworkStructValue, Payload networkObject, int serializer, int __catNetworkReplacementTarget, int __catNetworkStringByteCount, int __catNetworkStructFieldName, int GetNetworkObjectTypeId);
				public Outer? Received;
				public int PayloadValue;
				public Entity() { SendReceived += (_, _, value, payload, a, b, c, d, e) => { Received = value; PayloadValue = payload.Value + a + b + c + d + e; }; }
			}
			public static class Probe {
				public static void Run() {
					var catalogue = Helper.Catalogue(typeof(Payload));
					var (server, owner, remote, entity, proxy) = Helper.Connect(catalogue);
					Outer?[] values = {
						new Outer { Name = new Inner { Name = 42, @event = "first" }, Name__Name = new Inner { Name = 17, @event = "second" }, @class = 7 },
						new Outer { Name = new Inner { Name = null, @event = "empty" }, Name__Name = null, @class = null },
						new Outer { Name = null, Name__Name = null, @class = 5 },
						null
					};
					foreach (var value in values) {
						var source = new Entity { Value = value };
						var destination = new Entity();
						Helper.RoundTrip(source, destination, catalogue);
						Helper.Check(Equals(value, destination.Value), "nested property value");
						proxy.Send(value, new Payload { Value = 10 }, 1, 2, 3, 4, 5);
						Helper.Pump(server, owner, remote);
						Helper.Check(Equals(value, entity.Received) && entity.PayloadValue == 25, "nested message and object value");
					}
				}
			}
			""");
	}

	private static async Task CompileAndRun(string source) {
		IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
			.Split(Path.PathSeparator).Append(typeof(NetworkObject).Assembly.Location).Distinct()
			.Select(path => MetadataReference.CreateFromFile(path));
		CSharpCompilation input = CSharpCompilation.Create(
			"Identifiers_" + Guid.NewGuid().ToString("N"),
			[CSharpSyntaxTree.ParseText(Helpers + source)], references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		ImmutableArray<DiagnosticAnalyzer> analyzers = [new CatNetworkAnalyzer()];
		Assert.That(await input.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync(), Is.Empty, "Input analyzer diagnostics");
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());
		driver.RunGeneratorsAndUpdateCompilation(input, out Compilation output, out ImmutableArray<Diagnostic> diagnostics);
		Assert.That(diagnostics, Is.Empty, "Generator diagnostics");
		Assert.That(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error), Is.Empty, "Generated compiler errors");
		Assert.That(await output.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync(), Is.Empty, "Output analyzer diagnostics");
		using MemoryStream stream = new();
		var result = output.Emit(stream);
		Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Diagnostics));
		Assembly assembly = Assembly.Load(stream.ToArray());
		Assert.DoesNotThrow(() => assembly.GetType("Probe")!.GetMethod("Run")!.Invoke(null, null));
	}

	private const string Helpers = """
		#nullable enable
		using System;
		using System.Collections.Generic;
		using System.Diagnostics.CodeAnalysis;
		using System.Linq;
		using Cat.Network;
		[NetworkObject] public partial class Profile : NetworkProfile { }
		public sealed class Storage : EntityStorage {
			private readonly Dictionary<Guid, NetworkEntity> entities = new();
			protected override IEnumerable<NetworkEntity> GetRegisteredEntities() => entities.Values;
			protected override void AddEntity(NetworkEntity entity) => entities.Add(entity.Id, entity);
			protected override void RemoveEntity(Guid id) => entities.Remove(id);
			public override bool TryGetEntity(Guid id, [NotNullWhen(true)] out NetworkEntity? entity) => entities.TryGetValue(id, out entity);
			public override void PopulateRelevantEntities(NetworkProfile profile, ICollection<NetworkEntity> result) { foreach (var entity in entities.Values) result.Add(entity); }
		}
		public static class Helper {
			public static void Check(bool success, string message) { if (!success) throw new Exception(message); }
			public static TypeCatalogue Catalogue(params Type[] extra) { var catalogue = new TypeCatalogue(); foreach (var type in new[] { typeof(Entity), typeof(Profile) }.Concat(extra)) catalogue.Register(type); return catalogue; }
			public static void Pump(RelayServer server, params RelayClient[] clients) { for (int i = 0; i < 6; i++) { server.Tick(); foreach (var client in clients) client.Tick(); } }
			public static (RelayServer, RelayClient, RelayClient, Entity, Entity) Connect(TypeCatalogue? catalogue = null) {
				catalogue ??= Catalogue();
				var daemon = new MemoryRelayDaemon(() => new Profile());
				var server = new RelayServer(daemon, catalogue, new Storage());
				var owner = new RelayClient(catalogue); var remote = new RelayClient(catalogue);
				owner.Connect(daemon.Connect()); remote.Connect(daemon.Connect()); Pump(server, owner, remote);
				var entity = new Entity(); owner.Spawn(entity); Pump(server, owner, remote);
				Check(remote.TryGetEntity(entity.Id, out var proxy), "replicated entity");
				return (server, owner, remote, entity, (Entity)proxy!);
			}
			public static byte[] RoundTrip(Entity source, Entity destination, TypeCatalogue catalogue) {
				Check(catalogue.TryFindSerializer(typeof(Entity), out var serializer), "serializer");
				var writer = new BufferWriter();
				serializer!.Serialize(writer, source, new(catalogue), new(MemberSelectionMode.All, MemberIdentificationMode.Name));
				serializer.Deserialize(destination, writer.GetWrittenSpan(), new(catalogue));
				return writer.GetWrittenSpan().ToArray();
			}
		}

		""";
}
