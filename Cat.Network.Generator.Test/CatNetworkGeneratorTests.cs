using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cat.Network.Generator.Test;

public sealed class CatNetworkGeneratorTests {
	[Test]
	public void NetworkObjectAttributeIsClassOnlyAndNotInherited() {
		AttributeUsageAttribute usage = typeof(NetworkObjectAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.OfType<AttributeUsageAttribute>()
			.Single();

		Assert.Multiple(() => {
			Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Class));
			Assert.That(usage.Inherited, Is.False);
		});
	}

	[Test]
	public void NetworkPropertyAttributeIsPropertyOnlyAndNotInherited() {
		AttributeUsageAttribute usage = typeof(NetworkPropertyAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.OfType<AttributeUsageAttribute>()
			.Single();

		Assert.Multiple(() => {
			Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Property));
			Assert.That(usage.Inherited, Is.False);
		});
	}

	[Test]
	public void GeneratedRpcIncludesEventInterfaceStubAndDispatch() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public sealed partial class PlayerProfile : NetworkProfile {
		                      }

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : NetworkEntity {
		                      	[RPC]
		                      	public partial void TookDamage(int amount, string label);
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string propertySource = GetGeneratedSource(runResult, "Game_Player.g.cs");
		string messageSource = GetGeneratedSource(runResult, "Game_Player_Messages.g.cs");

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(propertySource, Does.Contain("partial class Player : global::System.IEquatable<Player>, global::Cat.Network.INetworkObject"));
			Assert.That(propertySource, Does.Not.Contain("TookDamage"));
			Assert.That(messageSource, Does.Contain("partial class Player : global::Cat.Network.INetworkRpcTarget, global::Game.Player.RPC"));
			Assert.That(messageSource, Does.Contain("public partial interface RPC"));
			Assert.That(messageSource, Does.Contain("public delegate void TookDamageRpcHandler(global::Cat.Network.RelayClient client, global::Cat.Network.NetworkProfile instigator, global::System.Int32 amount, global::System.String label);"));
			Assert.That(messageSource, Does.Contain("public event TookDamageRpcHandler? TookDamageReceived;"));
			Assert.That(messageSource, Does.Contain("void global::Game.Player.RPC.RaiseTookDamage(global::Cat.Network.RelayClient client, global::Cat.Network.NetworkProfile instigator, global::System.Int32 amount, global::System.String label)"));
			Assert.That(messageSource, Does.Contain("public partial void TookDamage(global::System.Int32 amount, global::System.String label)"));
			Assert.That(messageSource, Does.Contain("client.RentRpcMessageWriter(this, "));
			Assert.That(messageSource, Does.Contain("client.QueueRentedMessageWriter(writer);"));
			Assert.That(messageSource, Does.Contain("bool global::Cat.Network.INetworkRpcTarget.TryInvokeRpc("));
			Assert.That(messageSource, Does.Contain("((global::Game.Player.RPC)this).RaiseTookDamage(client, instigator, amount, label);"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedRpcDispatchCanLiveOnAbstractDeclaringBaseWithoutDerivedMessageStub() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public abstract partial class Actor : NetworkEntity {
		                      	[RPC]
		                      	public partial void TookDamage(int amount);
		                      }

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : Actor {
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string actorMessageSource = GetGeneratedSource(runResult, "Game_Actor_Messages.g.cs");
		bool generatedPlayerMessages = runResult.GeneratedTrees.Any(tree => tree.FilePath.Contains("Game_Player_Messages.g.cs", StringComparison.Ordinal));

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(actorMessageSource, Does.Contain("partial class Actor : global::Cat.Network.INetworkRpcTarget, global::Game.Actor.RPC"));
			Assert.That(actorMessageSource, Does.Contain("bool global::Cat.Network.INetworkRpcTarget.TryInvokeRpc("));
			Assert.That(actorMessageSource, Does.Contain("((global::Game.Actor.RPC)this).RaiseTookDamage(client, instigator, amount);"));
			Assert.That(generatedPlayerMessages, Is.False);
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedBroadcastIncludesOwnerGuardAndExplicitReceiveHook() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : NetworkEntity {
		                      	[Broadcast(NetworkMessageReceiveMode.Explicit)]
		                      	public partial void PlayImpact(int effectId);

		                      	void Player.Broadcast.PlayImpact(RelayClient client, NetworkProfile instigator, int effectId) {
		                      	}
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string propertySource = GetGeneratedSource(runResult, "Game_Player.g.cs");
		string messageSource = GetGeneratedSource(runResult, "Game_Player_Messages.g.cs");

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(propertySource, Does.Contain("partial class Player : global::System.IEquatable<Player>, global::Cat.Network.INetworkObject"));
			Assert.That(propertySource, Does.Not.Contain("PlayImpact"));
			Assert.That(messageSource, Does.Contain("partial class Player : global::Cat.Network.INetworkRpcTarget, global::Game.Player.Broadcast"));
			Assert.That(messageSource, Does.Contain("public partial interface Broadcast"));
			Assert.That(messageSource, Does.Contain("void PlayImpact(global::Cat.Network.RelayClient client, global::Cat.Network.NetworkProfile instigator, global::System.Int32 effectId);"));
			Assert.That(messageSource, Does.Contain("if (!IsOwner)"));
			Assert.That(messageSource, Does.Contain("client.RentBroadcastMessageWriter(this, "));
			Assert.That(messageSource, Does.Contain("client.QueueRentedMessageWriter(writer);"));
			Assert.That(messageSource, Does.Contain("((global::Game.Player.Broadcast)this).PlayImpact(client, instigator, effectId);"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedRootPropertiesDoNotReferenceObjectProperties() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Root : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string generatedSource = string.Join(Environment.NewLine, runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(runResult.GeneratedTrees, Has.Length.EqualTo(2));
			Assert.That(generatedSource, Does.Contain("protected static new global::System.Collections.Immutable.ImmutableArray<global::Cat.Network.NetworkPropertyInfo> Properties { get; } = ["));
			Assert.That(generatedSource, Does.Not.Contain("..global::System.Object.Properties"));
			Assert.That(generatedSource, Does.Contain("Index = 0"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedPropertiesIncludeStructuralEqualityAndHashCode() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Actor : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : Actor {
		                      	[NetworkProperty]
		                      	public partial string Name { get; set; } = string.Empty;

		                      	[NetworkCollection]
		                      	public partial NetworkList<int> Scores { get; }

		                      	[NetworkCollection]
		                      	public partial NetworkDictionary<int, string> Labels { get; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string propertySource = GetGeneratedSource(runResult, "Game_Player.g.cs");

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(propertySource, Does.Contain("partial class Player : global::System.IEquatable<Player>, global::Cat.Network.INetworkObject"));
			Assert.That(propertySource, Does.Contain("public bool Equals(global::Game.Player? other)"));
			Assert.That(propertySource, Does.Contain("if (!base.Equals(other))"));
			Assert.That(propertySource, Does.Contain("EqualityComparer<global::System.String>.Default.Equals(GetName(this), GetName(other))"));
			Assert.That(propertySource, Does.Contain("global::Cat.Network.NetworkObjectEquality.ListEquals<global::System.Int32>(GetScores(this), GetScores(other))"));
			Assert.That(propertySource, Does.Contain("global::Cat.Network.NetworkObjectEquality.DictionaryEquals<global::System.Int32, global::System.String>(GetLabels(this), GetLabels(other))"));
			Assert.That(propertySource, Does.Contain("public override int GetHashCode()"));
			Assert.That(propertySource, Does.Contain("hash.Add(base.GetHashCode());"));
			Assert.That(propertySource, Does.Contain("hash.Add(GetName(this));"));
			Assert.That(propertySource, Does.Contain("hash.Add(global::Cat.Network.NetworkObjectEquality.ListHashCode<global::System.Int32>(GetScores(this)));"));
			Assert.That(propertySource, Does.Contain("hash.Add(global::Cat.Network.NetworkObjectEquality.DictionaryHashCode<global::System.Int32, global::System.String>(GetLabels(this)));"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratorUsesNetworkObjectAttributeMetadataNamePipelineToEmitPartialClassStub() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string propertySource = GetGeneratedSource(runResult, "Game_Player.g.cs");
		string serializerSource = GetGeneratedSource(runResult, "Game_Player_Serializer.g.cs");
		string expectedPropertyBlock = """
			protected static new global::System.Collections.Immutable.ImmutableArray<global::Cat.Network.NetworkPropertyInfo> Properties { get; } = [..global::Cat.Network.NetworkObject.Properties, new global::Cat.Network.NetworkPropertyInfo
			{
				Index = 0,
				Name = nameof(Health),
				EncodedName = global::System.Collections.Immutable.ImmutableArray.Create(global::System.Text.Encoding.UTF8.GetBytes(nameof(Health)))
			}
			];
			""";
		string expectedDeserializeHealthBlock = """
			private static void DeserializeHealth(global::Game.Player typedTarget, global::System.ReadOnlySpan<byte> valueData, global::Cat.Network.SerializationContext context)
			{
				if (valueData.Length != 4)
				{
					return;
				}
				SetHealth(typedTarget, global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(valueData));
			}
			""";
		string expectedAccessorBlock = """
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "get_Health")]
			private static extern global::System.Int32 GetHealth(global::Game.Player target);
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "set_Health")]
			private static extern void SetHealth(global::Game.Player target, global::System.Int32 value);
			""";

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(runResult.GeneratedTrees, Has.Length.EqualTo(2));
			Assert.That(propertySource, Does.Contain($"[global::Cat.Network.NetworkObjectTypeId(\"{CreateStableTypeId("global::Game.Player")}\")]"));
			Assert.That(propertySource, Does.Contain("[global::Cat.Network.NetworkObjectSerializerAttribute<__CatNetwork_Player_Serializer>]"));
			AssertGeneratedSourceEqual(expectedPropertyBlock, ExtractStatementBlock(propertySource, "protected static new global::System.Collections.Immutable.ImmutableArray<global::Cat.Network.NetworkPropertyInfo> Properties", "];"));
			Assert.That(propertySource, Does.Contain("void global::Cat.Network.INetworkObject.Initialize()"));
			Assert.That(propertySource, Does.Contain("((global::Cat.Network.INetworkObject)this).PropertyStates = new global::Cat.Network.NetworkPropertyState[Properties.Length];"));
			AssertGeneratedSourceEqual(expectedDeserializeHealthBlock, ExtractMemberBlock(serializerSource, "private static void DeserializeHealth("));
			AssertGeneratedSourceEqual(expectedAccessorBlock, ExtractTailBlock(serializerSource, "[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"get_Health\")]"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedPropertiesAreInitializedFromImmediateBaseProperties() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Actor : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : Actor {
		                      	[NetworkProperty]
		                      	public partial int Mana { get; set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string generatedSource = string.Join(Environment.NewLine, runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(runResult.GeneratedTrees, Has.Length.EqualTo(4));
			Assert.That(generatedSource, Does.Contain("..global::Cat.Network.NetworkObject.Properties"));
			Assert.That(generatedSource, Does.Contain("..global::Game.Actor.Properties"));
			Assert.That(generatedSource, Does.Contain("Index = 1"));
			Assert.That(generatedSource, Does.Contain("Name = nameof(Mana)"));
			Assert.That(generatedSource, Does.Contain("EncodedName = global::System.Collections.Immutable.ImmutableArray.Create(global::System.Text.Encoding.UTF8.GetBytes(nameof(Mana)))"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedDerivedSerializerHandlesInheritedNetworkProperties() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Actor : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : Actor {
		                      	[NetworkProperty]
		                      	public partial int Mana { get; set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string playerSerializerSource = runResult.GeneratedTrees
			.Where(tree => tree.FilePath.Contains("Game_Player_Serializer", StringComparison.Ordinal))
			.Select(tree => tree.GetText().ToString())
			.Single();

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(playerSerializerSource, Does.Contain("case 0:"));
			Assert.That(playerSerializerSource, Does.Contain("DeserializeHealth(typedTarget, valueData, context);"));
			Assert.That(playerSerializerSource, Does.Contain("case 1:"));
			Assert.That(playerSerializerSource, Does.Contain("DeserializeMana(typedTarget, valueData, context);"));
			Assert.That(playerSerializerSource, Does.Contain("private static void DeserializeHealth(global::Game.Player typedTarget, global::System.ReadOnlySpan<byte> valueData, global::Cat.Network.SerializationContext context)"));
			Assert.That(playerSerializerSource, Does.Contain("private static extern void SetHealth(global::Game.Actor target, global::System.Int32 value);"));
			Assert.That(playerSerializerSource, Does.Contain("private static void DeserializeMana(global::Game.Player typedTarget, global::System.ReadOnlySpan<byte> valueData, global::Cat.Network.SerializationContext context)"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedPropertiesPreservePropertyAndAccessorAccessibility() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkProperty]
		                      	internal partial int Health { get; private set; }

		                      	[NetworkProperty]
		                      	protected internal partial string Name { get; protected set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string generatedSource = string.Join(Environment.NewLine, runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(generatedSource, Does.Contain("internal partial global::System.Int32 Health"));
			Assert.That(generatedSource, Does.Contain("get => field;"));
			Assert.That(generatedSource, Does.Contain("private set"));
			Assert.That(generatedSource, Does.Contain("current.PropertyStates[0] |= global::Cat.Network.NetworkPropertyState.Replaced;"));
			Assert.That(generatedSource, Does.Contain("protected internal partial global::System.String Name"));
			Assert.That(generatedSource, Does.Contain("protected set"));
			Assert.That(generatedSource, Does.Contain("current.PropertyStates[1] |= global::Cat.Network.NetworkPropertyState.Replaced;"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedPartialClassHeader_DoesNotRepeatBaseTypeOrInterfaces() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Root : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string propertySource = GetGeneratedSource(runResult, "Game_Root.g.cs");

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(propertySource, Does.Contain("partial class Root : global::System.IEquatable<Root>, global::Cat.Network.INetworkObject"));
			Assert.That(propertySource, Does.Not.Contain("partial class Root : global::Cat.Network.NetworkObject"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedSerializerIncludesNetworkObjectUpdateHandling() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Child : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Value { get; set; }
		                      }

		                      [NetworkObjectAttribute]
		                      public partial class Parent : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial Child? Child { get; set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string generatedSource = string.Join(Environment.NewLine, runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(runResult.GeneratedTrees, Has.Length.EqualTo(4));
			Assert.That(generatedSource, Does.Contain("global::System.Range fieldCountRange = writer.Reserve(2);"));
			Assert.That(generatedSource, Does.Contain("ushort fieldCount = 0;"));
			Assert.That(generatedSource, Does.Contain("current.PropertyStates[0] != global::Cat.Network.NetworkPropertyState.Unchanged"));
			Assert.That(generatedSource, Does.Contain("fieldCount++;"));
			Assert.That(generatedSource, Does.Contain("writer.WriteUInt16(fieldCountRange, fieldCount);"));
			Assert.That(generatedSource, Does.Contain("global::Cat.Network.NetworkObjectUpdateMode NetworkObjectUpdateMode = (global::Cat.Network.NetworkObjectUpdateMode)valueData[0];"));
			Assert.That(generatedSource, Does.Contain("case global::Cat.Network.NetworkObjectUpdateMode.Modify:"));
			Assert.That(generatedSource, Does.Contain("case global::Cat.Network.NetworkObjectUpdateMode.Replace:"));
			Assert.That(generatedSource, Does.Contain("case global::Cat.Network.NetworkObjectUpdateMode.Clear:"));
			Assert.That(generatedSource, Does.Contain("global::Game.Child? currentTarget = GetChild(typedTarget);"));
			Assert.That(generatedSource, Does.Contain("context.TypeCatalogue.TryFindSerializer(currentTarget.GetType(), out global::Cat.Network.INetworkObjectSerializer? nestedSerializer)"));
			Assert.That(generatedSource, Does.Contain("global::Cat.Network.NetworkPropertyState propertyState = current.PropertyStates[propertyIndex];"));
			Assert.That(generatedSource, Does.Contain("nestedModifiedSerializer.Serialize(writer, currentValue, context, new global::Cat.Network.SerializationOptions(global::Cat.Network.MemberSelectionMode.Dirty, options.MemberIdentificationMode));"));
			Assert.That(generatedSource, Does.Contain("nestedReplacementSerializer.Serialize(writer, currentValue, context, new global::Cat.Network.SerializationOptions(global::Cat.Network.MemberSelectionMode.All, options.MemberIdentificationMode));"));
			Assert.That(generatedSource, Does.Contain("context.TypeCatalogue.TryFindType(replacementTypeId, out global::System.Type? replacementType)"));
			Assert.That(generatedSource, Does.Contain("global::System.Activator.CreateInstance(replacementType)is not global::Game.Child replacementTarget"));
			Assert.That(generatedSource, Does.Contain("Name = \"get_Child\""));
			Assert.That(generatedSource, Does.Contain("Name = \"set_Child\""));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedNetworkObjectPropertySetterMaintainsParentOwnership() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Child : NetworkObject {
		                      }

		                      [NetworkObjectAttribute]
		                      public partial class Parent : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial Child? Child { get; set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string propertySource = GetGeneratedSource(runResult, "Game_Parent.g.cs");
		string expectedPropertyBlock = """
			public partial global::Game.Child? Child
			{
				get => field;
				set
				{
					global::Game.Child? oldValue = field;
					int propertyIndex = 0;
					if (global::System.Object.ReferenceEquals(oldValue, value))
					{
						return;
					}
					if (value is not null)
					{
						global::Cat.Network.INetworkObject networkValue = value;
						if (networkValue.Parent is not null && (!global::System.Object.ReferenceEquals(networkValue.Parent, this) || networkValue.PropertyIndex != propertyIndex))
						{
							throw new global::System.InvalidOperationException("NetworkObjects may only occupy one networked property at a time.");
						}
					}
					field = value;
					if (oldValue is not null)
					{
						global::Cat.Network.INetworkObject oldNetworkValue = oldValue;
						oldNetworkValue.Parent = null;
						oldNetworkValue.PropertyIndex = -1;
						oldNetworkValue.IsCollectionItem = false;
					}
					if (value is not null)
					{
						global::Cat.Network.INetworkObject attachedValue = value;
						attachedValue.Parent = this;
						attachedValue.PropertyIndex = propertyIndex;
						attachedValue.IsCollectionItem = false;
					}
					global::Cat.Network.INetworkObject current = this;
					current.PropertyStates[propertyIndex] |= global::Cat.Network.NetworkPropertyState.Replaced;
					while (current.Parent is global::Cat.Network.NetworkObject parent)
					{
						global::Cat.Network.INetworkObject parentObject = parent;
						parentObject.PropertyStates[current.PropertyIndex] |= global::Cat.Network.NetworkPropertyState.Modified;
						current = parentObject;
					}
					global::Cat.Network.PropertyChangedEventArgs propertyChangedArgs = new global::Cat.Network.PropertyChangedEventArgs
					{
						Index = 0,
						Name = nameof(Child)
					};
					((global::Cat.Network.INetworkObject)this).OnPropertyChanged(propertyChangedArgs);
					global::Cat.Network.PropertyChangedEventArgs<global::Game.Child?> args = new global::Cat.Network.PropertyChangedEventArgs<global::Game.Child?>
					{
						Index = 0,
						Name = nameof(Child),
						PreviousValue = oldValue,
						CurrentValue = field
					};
					ChildChanged?.Invoke(this, args);
				}
			}
			""";

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			AssertGeneratedSourceEqual(expectedPropertyBlock, ExtractMemberBlock(propertySource, "public partial global::Game.Child? Child"));
			Assert.That(propertySource, Does.Contain("public event global::Cat.Network.NetworkPropertyChanged<global::Game.Parent, global::Game.Child?>? ChildChanged;"));
			Assert.That(propertySource, Does.Not.Contain("private void OnChildChanged("));
			Assert.That(propertySource, Does.Contain("((global::Cat.Network.INetworkObject)this).OnPropertyChanged(propertyChangedArgs);"));
			Assert.That(propertySource, Does.Contain("ChildChanged?.Invoke(this, args);"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedPropertySetterMarksPropertyStates() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Player : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string propertySource = GetGeneratedSource(runResult, "Game_Player.g.cs");

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(propertySource, Does.Contain("global::Cat.Network.INetworkObject current = this;"));
			Assert.That(propertySource, Does.Contain("current.PropertyStates[0] |= global::Cat.Network.NetworkPropertyState.Replaced;"));
			Assert.That(propertySource, Does.Contain("while (current.Parent is global::Cat.Network.NetworkObject parent)"));
			Assert.That(propertySource, Does.Contain("parentObject.PropertyStates[current.PropertyIndex] |= global::Cat.Network.NetworkPropertyState.Modified;"));
			Assert.That(propertySource, Does.Contain("public event global::Cat.Network.NetworkPropertyChanged<global::Game.Player, global::System.Int32>? HealthChanged;"));
			Assert.That(propertySource, Does.Not.Contain("OnHealthChanged("));
			Assert.That(propertySource, Does.Contain("global::Cat.Network.PropertyChangedEventArgs propertyChangedArgs = new global::Cat.Network.PropertyChangedEventArgs"));
			Assert.That(propertySource, Does.Contain("global::Cat.Network.PropertyChangedEventArgs<global::System.Int32> args = new global::Cat.Network.PropertyChangedEventArgs<global::System.Int32>"));
			Assert.That(propertySource, Does.Contain("HealthChanged?.Invoke(this, args);"));
			Assert.That(propertySource, Does.Contain("global::System.Collections.Immutable.ImmutableArray<global::Cat.Network.NetworkPropertyInfo> global::Cat.Network.INetworkObject.NetworkProperties => Properties;"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedCollectionProperty_UsesNetworkListInitialization() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial NetworkList<int> Scores { get; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string propertySource = GetGeneratedSource(runResult, "Game_Player.g.cs");

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(propertySource, Does.Contain("public partial global::Cat.Network.NetworkList<global::System.Int32> Scores"));
			Assert.That(propertySource, Does.Contain("get => field;"));
			Assert.That(propertySource, Does.Contain("} = new global::Cat.Network.NetworkValueList<global::System.Int32>();"));
			Assert.That(propertySource, Does.Contain("Index = 0"));
			Assert.That(propertySource, Does.Contain("[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"get_Scores\")]"));
			Assert.That(propertySource, Does.Contain("private static extern global::Cat.Network.NetworkList<global::System.Int32> GetScores(global::Game.Player target);"));
			Assert.That(propertySource, Does.Contain("((global::Cat.Network.INetworkCollection)GetScores(this)).Initialize(this, 0);"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedDictionaryCollectionProperty_UsesNetworkDictionaryInitialization() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial NetworkDictionary<int, string> Scores { get; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string propertySource = GetGeneratedSource(runResult, "Game_Player.g.cs");

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(propertySource, Does.Contain("public partial global::Cat.Network.NetworkDictionary<global::System.Int32, global::System.String> Scores"));
			Assert.That(propertySource, Does.Contain("get => field;"));
			Assert.That(propertySource, Does.Contain("} = new global::Cat.Network.NetworkValueDictionary<global::System.Int32, global::System.String>();"));
			Assert.That(propertySource, Does.Contain("private static extern global::Cat.Network.NetworkDictionary<global::System.Int32, global::System.String> GetScores(global::Game.Player target);"));
			Assert.That(propertySource, Does.Contain("((global::Cat.Network.INetworkCollection)GetScores(this)).Initialize(this, 0);"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedSerializerCanAccessPrivateCollectionProperty() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	private partial NetworkList<int> Scores { get; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string propertySource = GetGeneratedSource(runResult, "Game_Player.g.cs");
		string serializerSource = GetGeneratedSource(runResult, "Game_Player_Serializer.g.cs");

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(propertySource, Does.Contain("private partial global::Cat.Network.NetworkList<global::System.Int32> Scores"));
			Assert.That(propertySource, Does.Contain("get => field;"));
			Assert.That(propertySource, Does.Contain("} = new global::Cat.Network.NetworkValueList<global::System.Int32>();"));
			Assert.That(serializerSource, Does.Contain("[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"get_Scores\")]"));
			Assert.That(serializerSource, Does.Contain("private static extern global::Cat.Network.NetworkList<global::System.Int32> GetScores(global::Game.Player target);"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedSerializerIncludesNullableValueTypeHandling() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Player : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int? Health { get; set; }

		                      	[NetworkProperty]
		                      	public partial global::System.Guid? SessionId { get; set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string serializerSource = GetGeneratedSource(runResult, "Game_Player_Serializer.g.cs");
		string expectedNullableHealthBlock = """
			private static void DeserializeHealth(global::Game.Player typedTarget, global::System.ReadOnlySpan<byte> valueData, global::Cat.Network.SerializationContext context)
			{
				if (valueData.Length < 1)
				{
					return;
				}
				byte hasValue = valueData[0];
				valueData = valueData[1..];
				switch (hasValue)
				{
					case 0:
						if (valueData.Length != 0)
						{
							return;
						}
						SetHealth(typedTarget, null);
						return;
					case 1:
						break;
					default:
						return;
				}
				if (valueData.Length != 4)
				{
					return;
				}
				SetHealth(typedTarget, global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(valueData));
			}
			""";
		string expectedNullableSessionIdBlock = """
			private static void DeserializeSessionId(global::Game.Player typedTarget, global::System.ReadOnlySpan<byte> valueData, global::Cat.Network.SerializationContext context)
			{
				if (valueData.Length < 1)
				{
					return;
				}
				byte hasValue = valueData[0];
				valueData = valueData[1..];
				switch (hasValue)
				{
					case 0:
						if (valueData.Length != 0)
						{
							return;
						}
						SetSessionId(typedTarget, null);
						return;
					case 1:
						break;
					default:
						return;
				}
				if (valueData.Length != 16)
				{
					return;
				}
				SetSessionId(typedTarget, new global::System.Guid(valueData));
			}
			""";
		string expectedNullableAccessorTail = """
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "get_Health")]
			private static extern global::System.Int32? GetHealth(global::Game.Player target);
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "set_Health")]
			private static extern void SetHealth(global::Game.Player target, global::System.Int32? value);
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "get_SessionId")]
			private static extern global::System.Guid? GetSessionId(global::Game.Player target);
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "set_SessionId")]
			private static extern void SetSessionId(global::Game.Player target, global::System.Guid? value);
			""";

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			AssertGeneratedSourceEqual(expectedNullableHealthBlock, ExtractMemberBlock(serializerSource, "private static void DeserializeHealth("));
			AssertGeneratedSourceEqual(expectedNullableSessionIdBlock, ExtractMemberBlock(serializerSource, "private static void DeserializeSessionId("));
			AssertGeneratedSourceEqual(expectedNullableAccessorTail, ExtractTailBlock(serializerSource, "[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"get_Health\")]"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedSerializerIncludesStructAndNullableStructHandling() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      public struct UltraDetailedStats {
		                        public double Vision;
		                      }
		                      
		                      public struct DetailStats {
		                      	public double CriticalChance;
		                      	public UltraDetailedStats UltraDetails;
		                      }

		                      public struct Stats {
		                        public int Health;
		                      	public float Accuracy;
		                      	
		                      	public DetailStats Details;
		                      }

		                      [NetworkObjectAttribute]
		                      public partial class Player : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial Stats Stats { get; set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string generatedSource = string.Join(Environment.NewLine, runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);

			
			
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	private static CSharpCompilation CreateCompilation(string source) {
		SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
		IEnumerable<MetadataReference> references = GetMetadataReferences();

		return CSharpCompilation.Create(
			"GeneratorTestAssembly",
			new[] { syntaxTree },
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
	}

	private static IEnumerable<MetadataReference> GetMetadataReferences() {
		string trustedPlatformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

		foreach (string assemblyPath in trustedPlatformAssemblies.Split(Path.PathSeparator)) yield return MetadataReference.CreateFromFile(assemblyPath);

		yield return MetadataReference.CreateFromFile(typeof(NetworkObject).Assembly.Location);
	}

	private static string GetGeneratedSource(GeneratorDriverRunResult runResult, string filePathContains) {
		return runResult.GeneratedTrees
			.Where(tree => tree.FilePath.Contains(filePathContains, StringComparison.Ordinal))
			.Select(tree => tree.GetText().ToString())
			.Single();
	}

	private static void AssertGeneratedSourceEqual(string expected, string actual) {
		string[] expectedLines = NormalizeLines(expected);
		string[] actualLines = NormalizeLines(actual);

		Assert.That(actualLines, Is.EqualTo(expectedLines));
	}

	private static string[] NormalizeLines(string source) {
		return source
			.Replace("\r\n", "\n")
			.Split('\n')
			.Select(static line => line.Trim())
			.Where(static line => !string.IsNullOrWhiteSpace(line))
			.ToArray();
	}

	private static string ExtractBlock(string source, string startMarker, string endMarker) {
		int startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
		Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), $"Could not find start marker: {startMarker}");

		int endIndex = source.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
		Assert.That(endIndex, Is.GreaterThanOrEqualTo(0), $"Could not find end marker: {endMarker}");

		int afterEndMarker = endIndex + endMarker.Length;
		return source[startIndex..afterEndMarker];
	}

	private static string ExtractMemberBlock(string source, string memberStartMarker) {
		int startIndex = source.IndexOf(memberStartMarker, StringComparison.Ordinal);
		Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), $"Could not find member start marker: {memberStartMarker}");

		int braceStart = source.IndexOf('{', startIndex);
		Assert.That(braceStart, Is.GreaterThanOrEqualTo(0), $"Could not find opening brace for marker: {memberStartMarker}");

		int depth = 0;
		for (int index = braceStart; index < source.Length; index++) {
			char current = source[index];
			if (current == '{') {
				depth++;
			} else if (current == '}') {
				depth--;
				if (depth == 0) {
					return source[startIndex..(index + 1)];
				}
			}
		}

		Assert.Fail($"Could not find matching closing brace for marker: {memberStartMarker}");
		return string.Empty;
	}

	private static string ExtractStatementBlock(string source, string startMarker, string endMarker) {
		int startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
		Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), $"Could not find statement start marker: {startMarker}");

		int endIndex = source.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
		Assert.That(endIndex, Is.GreaterThanOrEqualTo(0), $"Could not find statement end marker: {endMarker}");

		return source[startIndex..(endIndex + endMarker.Length)];
	}

	private static string ExtractTailBlock(string source, string startMarker) {
		int startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
		Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), $"Could not find tail start marker: {startMarker}");

		int helperIndex = source.IndexOf("private static global::System.Guid GetNetworkObjectTypeId(", startIndex, StringComparison.Ordinal);
		int classCloseIndex = helperIndex >= 0
			? helperIndex
			: source.LastIndexOf('}');
		Assert.That(classCloseIndex, Is.GreaterThan(startIndex), "Could not find tail block end.");

		return source[startIndex..classCloseIndex];
	}

	private static string CreateStableTypeId(string fullyQualifiedTypeName) {
		string assemblyQualifiedName = $"{fullyQualifiedTypeName}, GeneratorTestAssembly";
		byte[] bytes = Encoding.UTF8.GetBytes(assemblyQualifiedName);
		byte[] hash;
		using (SHA256 sha256 = SHA256.Create()) {
			hash = sha256.ComputeHash(bytes);
		}

		byte[] guidBytes = new byte[16];
		Array.Copy(hash, guidBytes, guidBytes.Length);
		return new Guid(guidBytes).ToString();
	}
}
