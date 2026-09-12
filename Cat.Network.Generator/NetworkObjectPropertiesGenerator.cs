using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cat.Network.Generator;

internal static class NetworkObjectPropertiesGenerator {
	public static void Generate(SourceProductionContext context, NetworkObjectTypeModel model) {
		context.AddSource($"{model.HintName}.g.cs", GenerateSource(model));
	}

	private static string GenerateSource(NetworkObjectTypeModel model) {
		string source = string.Format(
			SourceTemplate,
			Namespace(model),
			Attributes(model),
			model.TypeName,
			Properties(model),
			PartialProperties(model),
			CloneMethod(model),
			EqualityMembers(model),
			PropertyAccessorMethods(model),
			CollectionAccessorMethods(model),
			InitializeMembers(model),
			UpgradeMethods(model));

		return SyntaxFactory.ParseCompilationUnit(source).NormalizeWhitespace().ToFullString();
	}

	private static string Namespace(NetworkObjectTypeModel model) {
		return string.IsNullOrWhiteSpace(model.Namespace)
			? string.Empty
			: string.Format(NamespaceTemplate, model.Namespace);
	}

	private static string Attributes(NetworkObjectTypeModel model) {
		if (model.IsAbstract) {
			return string.Empty;
		}

		return string.Format(
			AttributesTemplate,
			model.TypeId,
			model.SerializerTypeName);
	}

	private static string Properties(NetworkObjectTypeModel model) {
		return string.Format(
			PropertiesTemplate,
			model.HasBaseProperties ? "new " : string.Empty,
			InheritedProperties(model),
			PropertyInfoEntries(model));
	}

	private static string InheritedProperties(NetworkObjectTypeModel model) {
		return model.HasBaseProperties
			? string.Format(InheritedPropertiesTemplate, model.BaseTypeName)
			: string.Empty;
	}

	private static string PropertyInfoEntries(NetworkObjectTypeModel model) {
		IEnumerable<(string Name, int PropertyIndex)> members = model.DeclaredProperties
			.Select(static property => (property.Name, property.PropertyIndex))
			.Concat(model.DeclaredCollections.Select(static collection => (collection.Name, collection.PropertyIndex)))
			.OrderBy(static member => member.PropertyIndex);

		return string.Join(
			",\n",
			members.Select(member => string.Format(
				PropertyInfoEntryTemplate,
				member.PropertyIndex,
				member.Name)));
	}

	private static string PartialProperties(NetworkObjectTypeModel model) {
		return string.Join(
			"\n\n",
			model.DeclaredProperties.Select(PartialProperty).Concat(model.DeclaredCollections.Select(CollectionPartialProperty)));
	}

	private static string CloneMethod(NetworkObjectTypeModel model) {
		if (model.IsAbstract) {
			return $$"""
				public abstract override {{model.FullyQualifiedName}} Clone();
				""";
		}

		string assignments = string.Join(
			"\n",
			model.Properties
				.OrderBy(static property => property.PropertyIndex)
				.Select(CloneAssignment));

		return $$"""
			public override {{model.FullyQualifiedName}} Clone()
			{
				{{model.FullyQualifiedName}} clone = new {{model.FullyQualifiedName}}();
				{{assignments}}
				return clone;
			}
			""";
	}

	private static string CloneAssignment(NetworkPropertyModel property) {
		if (property.SerializationKind == NetworkPropertySerializationKind.NetworkObject) {
			return $$"""
				if (Get{{property.Name}}(this) is {{property.RuntimeTypeName}} {{property.Name}}Value)
				{
					Set{{property.Name}}(clone, ({{property.TypeName}}){{property.Name}}Value.Clone());
				}
				else
				{
					Set{{property.Name}}(clone, null!);
				}
				""";
		}

		return $"Set{property.Name}(clone, Get{property.Name}(this));";
	}

	private static string PropertyAccessorMethods(NetworkObjectTypeModel model) {
		return string.Join(
			"\n\n",
			model.Properties
				.OrderBy(static property => property.PropertyIndex)
				.Select(PropertyAccessorMethods));
	}

	private static string EqualityMembers(NetworkObjectTypeModel model) {
		string equalsBody = EqualityBody(model);
		string hashCodeBody = HashCodeBody(model);

		return $$"""
			public bool Equals({{model.FullyQualifiedName}}? other)
			{
				if (global::System.Object.ReferenceEquals(null, other))
				{
					return false;
				}
				if (global::System.Object.ReferenceEquals(this, other))
				{
					return true;
				}
				{{equalsBody}}
				return true;
			}

			public override bool Equals(object? obj)
			{
				return obj is {{model.FullyQualifiedName}} other && Equals(other);
			}

			public override int GetHashCode()
			{
				global::System.HashCode hash = new global::System.HashCode();
				{{hashCodeBody}}
				return hash.ToHashCode();
			}
			""";
	}

	private static string InitializeMembers(NetworkObjectTypeModel model) {
		string collectionInitializers = string.Join(
			"\n",
			model.Collections
				.OrderBy(static collection => collection.PropertyIndex)
				.Select(CollectionInitializer));

		return string.Format(
			InitializeMembersTemplate,
			model.Namespace == "Cat.Network" ? "INetworkObject" : "global::Cat.Network.INetworkObject",
			collectionInitializers);
	}

	private static string CollectionAccessorMethods(NetworkObjectTypeModel model) {
		return string.Join(
			"\n\n",
			model.Collections
				.OrderBy(static collection => collection.PropertyIndex)
				.Select(CollectionAccessorMethod));
	}

	private static string UpgradeMethods(NetworkObjectTypeModel model) {
		return string.Join(
			"\n\n",
			model.UpgradeMethods.Select(UpgradeMethod));
	}

	private static string UpgradeMethod(NetworkObjectUpgradeMethodModel method) {
		return $$"""
			internal static void __CatNetworkUpgradeTo{{method.TargetVersion}}(global::Cat.Network.NetworkObjectUpgradeReader reader, global::Cat.Network.NetworkObjectUpgradeWriter writer) {
				{{method.Name}}(reader, writer);
			}
			""";
	}

	private static string EqualityBody(NetworkObjectTypeModel model) {
		List<string> comparisons = [];
		if (HasEqualityBase(model)) {
			comparisons.Add("if (!base.Equals(other))\n\t\t{\n\t\t\treturn false;\n\t\t}");
		}

		comparisons.AddRange(model.DeclaredProperties
			.OrderBy(static property => property.PropertyIndex)
			.Select(PropertyEqualityComparison));
		comparisons.AddRange(model.DeclaredCollections
			.OrderBy(static collection => collection.PropertyIndex)
			.Select(CollectionEqualityComparison));

		return comparisons.Count == 0
			? string.Empty
			: string.Join("\n\t\t", comparisons) + "\n\t\t";
	}

	private static string PropertyEqualityComparison(NetworkPropertyModel property) {
		return $$"""
			if (!global::System.Collections.Generic.EqualityComparer<{{property.TypeName}}>.Default.Equals(Get{{property.Name}}(this), Get{{property.Name}}(other)))
			{
				return false;
			}
			""";
	}

	private static string CollectionEqualityComparison(NetworkCollectionModel collection) {
		string comparerCall = collection.Kind switch {
			NetworkCollectionKind.List => $"global::Cat.Network.NetworkObjectEquality.ListEquals<{collection.ItemTypeName}>",
			NetworkCollectionKind.Dictionary => $"global::Cat.Network.NetworkObjectEquality.DictionaryEquals<{collection.RuntimeKeyTypeName}, {collection.ItemTypeName}>",
			_ => throw new global::System.InvalidOperationException($"Unsupported collection kind '{collection.Kind}'.")
		};

		return $$"""
			if (!{{comparerCall}}(Get{{collection.Name}}(this), Get{{collection.Name}}(other)))
			{
				return false;
			}
			""";
	}

	private static string HashCodeBody(NetworkObjectTypeModel model) {
		List<string> additions = [];
		if (HasEqualityBase(model)) {
			additions.Add("hash.Add(base.GetHashCode());");
		}

		additions.AddRange(model.DeclaredProperties
			.OrderBy(static property => property.PropertyIndex)
			.Select(static property => $"hash.Add(Get{property.Name}(this));"));
		additions.AddRange(model.DeclaredCollections
			.OrderBy(static collection => collection.PropertyIndex)
			.Select(CollectionHashCodeAddition));

		return additions.Count == 0
			? string.Empty
			: string.Join("\n\t\t", additions) + "\n\t\t";
	}

	private static string CollectionHashCodeAddition(NetworkCollectionModel collection) {
		string hasherCall = collection.Kind switch {
			NetworkCollectionKind.List => $"global::Cat.Network.NetworkObjectEquality.ListHashCode<{collection.ItemTypeName}>",
			NetworkCollectionKind.Dictionary => $"global::Cat.Network.NetworkObjectEquality.DictionaryHashCode<{collection.RuntimeKeyTypeName}, {collection.ItemTypeName}>",
			_ => throw new global::System.InvalidOperationException($"Unsupported collection kind '{collection.Kind}'.")
		};

		return $"hash.Add({hasherCall}(Get{collection.Name}(this)));";
	}

	private static bool HasEqualityBase(NetworkObjectTypeModel model) {
		return model.BaseTypeName != "global::Cat.Network.NetworkObject";
	}

	private static string PartialProperty(NetworkPropertyModel property) {
		if (property.SerializationKind == NetworkPropertySerializationKind.NetworkObject) {
			return string.Format(
				NetworkObjectPartialPropertyTemplate,
				property.Accessibility,
				property.TypeName,
				property.Name,
				property.GetterAccessibility,
				property.SetterAccessibility,
				property.PropertyIndex,
				PropertyChangedEvent(property),
				PropertyChangedNotification(property));
		}

		return string.Format(
			PartialPropertyTemplate,
			property.Accessibility,
			property.TypeName,
			property.Name,
			property.GetterAccessibility,
			property.SetterAccessibility,
			property.PropertyIndex,
			PropertyChangedEvent(property),
			PropertyChangedNotification(property));
	}

	private static string PropertyChangedEvent(NetworkPropertyModel property) {
		return $$"""
			{{property.Accessibility}} event global::Cat.Network.NetworkPropertyChanged<{{property.DeclaringTypeName}}, {{property.TypeName}}>? {{property.Name}}Changed;
			""";
	}

	private static string PropertyChangedNotification(NetworkPropertyModel property) {
		return $$"""
			global::Cat.Network.PropertyChangedEventArgs propertyChangedArgs = new global::Cat.Network.PropertyChangedEventArgs
			{
				Index = {{property.PropertyIndex}},
				Name = nameof({{property.Name}})
			};
			((global::Cat.Network.INetworkObject)this).OnPropertyChanged(propertyChangedArgs);

			global::Cat.Network.PropertyChangedEventArgs<{{property.TypeName}}> args = new global::Cat.Network.PropertyChangedEventArgs<{{property.TypeName}}>
			{
				Index = {{property.PropertyIndex}},
				Name = nameof({{property.Name}}),
				PreviousValue = oldValue,
				CurrentValue = field
			};
			{{property.Name}}Changed?.Invoke(this, args);
			""";
	}

	private static string CollectionPartialProperty(NetworkCollectionModel collection) {
		return string.Format(
			CollectionPartialPropertyTemplate,
			collection.TypeName,
			collection.Name,
			collection.GetterAccessibility,
			CollectionConcreteType(collection),
			collection.Accessibility);
	}

	private static string CollectionInitializer(NetworkCollectionModel collection) {
		return $"((global::Cat.Network.INetworkCollection)Get{collection.Name}(this)).Initialize(this, {collection.PropertyIndex});";
	}

	private static string CollectionAccessorMethod(NetworkCollectionModel collection) {
		return string.Format(
			CollectionAccessorMethodTemplate,
			collection.Name,
			collection.TypeName,
			collection.DeclaringTypeName);
	}

	private static string PropertyAccessorMethods(NetworkPropertyModel property) {
		return $$"""
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "get_{{property.Name}}")]
			private static extern {{property.TypeName}} Get{{property.Name}}({{property.DeclaringTypeName}} target);

			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "set_{{property.Name}}")]
			private static extern void Set{{property.Name}}({{property.DeclaringTypeName}} target, {{property.TypeName}} value);
			""";
	}

	private static string CollectionConcreteType(NetworkCollectionModel collection) {
		return collection.Kind switch {
			NetworkCollectionKind.List when collection.IsNetworkObjectItem => $"global::Cat.Network.NetworkObjectList<{collection.ItemTypeName}>",
			NetworkCollectionKind.List => $"global::Cat.Network.NetworkValueList<{collection.ItemTypeName}>",
			NetworkCollectionKind.Dictionary when collection.IsNetworkObjectItem => $"global::Cat.Network.NetworkObjectDictionary<{collection.RuntimeKeyTypeName}, {collection.ItemTypeName}>",
			NetworkCollectionKind.Dictionary => $"global::Cat.Network.NetworkValueDictionary<{collection.RuntimeKeyTypeName}, {collection.ItemTypeName}>",
			_ => throw new global::System.InvalidOperationException($"Unsupported collection kind '{collection.Kind}'.")
		};
	}

	private const string SourceTemplate = """
	                                      // <auto-generated/>
	                                      #nullable enable
	                                      #pragma warning disable CS0628
	                                      {0}
	                                      {1}
	                                      partial class {2} : global::System.IEquatable<{2}>, global::Cat.Network.INetworkObject
	                                      {{
	                                      {3}

	                                      {4}

	                                      {5}

	                                      {6}

	                                      {7}

	                                      {8}

	                                      {9}

	                                      {10}
	                                      }}
	                                      """;

	private const string NamespaceTemplate = """

	                                         namespace {0};

	                                         """;

	private const string AttributesTemplate = """
	                                          [global::Cat.Network.NetworkObjectTypeId("{0}")]
	                                          [global::Cat.Network.NetworkObjectSerializerAttribute<{1}>]
	                                          """;

	private const string PropertiesTemplate = """

	                                          	protected static {0}global::System.Collections.Immutable.ImmutableArray<global::Cat.Network.NetworkPropertyInfo> Properties {{ get; }} = [
	                                           {1}
	                                           {2}
	                                          	];

	                                          	global::System.Collections.Immutable.ImmutableArray<global::Cat.Network.NetworkPropertyInfo> global::Cat.Network.INetworkObject.NetworkProperties => Properties;
	                                          """;

	private const string InheritedPropertiesTemplate = """
	                                          		..{0}.Properties,
	                                          """;

	private const string PropertyInfoEntryTemplate = """
	                                                 		new global::Cat.Network.NetworkPropertyInfo {{
	                                                 			Index = {0},
	                                                 			Name = nameof({1}),
	                                                 			EncodedName = global::System.Collections.Immutable.ImmutableArray.Create(global::System.Text.Encoding.UTF8.GetBytes(nameof({1})))
	                                                 		}}
	                                                 """;

	private const string PartialPropertyTemplate = """
	                                               	{0} partial {1} {2}
	                                               	{{
	                                               		{3}get => field;
	                                               		{4}set
	                                               		{{
	                                               			{1} oldValue = field;
	                                               			if (global::System.Collections.Generic.EqualityComparer<{1}>.Default.Equals(oldValue, value))
	                                               			{{
	                                               				return;
	                                               			}}
	                                               			field = value;
	                                               			global::Cat.Network.INetworkObject current = this;
	                                               			current.PropertyStates[{5}] |= global::Cat.Network.NetworkPropertyState.Replaced;
	                                               			while (current.Parent is global::Cat.Network.NetworkObject parent)
	                                               			{{
	                                               				global::Cat.Network.INetworkObject parentObject = parent;
	                                               				parentObject.PropertyStates[current.PropertyIndex] |= global::Cat.Network.NetworkPropertyState.Modified;
	                                               				current = parentObject;
	                                               			}}
	                                               			{7}
	                                               		}}
	                                               	}}

	                                               {6}
	                                               """;

	private const string NetworkObjectPartialPropertyTemplate = """
	                                                     	{0} partial {1} {2}
	                                                     	{{
	                                                     		{3}get => field;
	                                                     		{4}set
	                                                     		{{
	                                                     			{1} oldValue = field;
	                                                     			int propertyIndex = {5};
	                                                     			if (global::System.Object.ReferenceEquals(oldValue, value))
	                                                     			{{
	                                                     				return;
	                                                     			}}
	                                                     			if (value is not null)
	                                                     			{{
	                                                     				global::Cat.Network.INetworkObject networkValue = value;
	                                                     				if (networkValue.Parent is not null && (!global::System.Object.ReferenceEquals(networkValue.Parent, this) || networkValue.PropertyIndex != propertyIndex))
	                                                     				{{
	                                                     					throw new global::System.InvalidOperationException("NetworkObjects may only occupy one networked property at a time.");
	                                                     				}}
	                                                     			}}
	                                                     			field = value;
	                                                     			if (oldValue is not null)
	                                                     			{{
	                                                     				global::Cat.Network.INetworkObject oldNetworkValue = oldValue;
	                                                     				oldNetworkValue.Parent = null;
	                                                     				oldNetworkValue.PropertyIndex = -1;
	                                                     				oldNetworkValue.IsCollectionItem = false;
	                                                     			}}
	                                                     			if (value is not null)
	                                                     			{{
	                                                     				global::Cat.Network.INetworkObject attachedValue = value;
	                                                     				attachedValue.Parent = this;
	                                                     				attachedValue.PropertyIndex = propertyIndex;
	                                                     				attachedValue.IsCollectionItem = false;
	                                                     			}}
	                                                     			global::Cat.Network.INetworkObject current = this;
	                                                     			current.PropertyStates[propertyIndex] |= global::Cat.Network.NetworkPropertyState.Replaced;
	                                                     			while (current.Parent is global::Cat.Network.NetworkObject parent)
	                                                     			{{
	                                                     				global::Cat.Network.INetworkObject parentObject = parent;
	                                                     				parentObject.PropertyStates[current.PropertyIndex] |= global::Cat.Network.NetworkPropertyState.Modified;
	                                                     				current = parentObject;
	                                                     			}}
	                                                     			{7}
	                                                     		}}
	                                                     	}}

	                                                     {6}
	                                                     """;

	private const string CollectionPartialPropertyTemplate = """
	                                                   	{4} partial {0} {1}
	                                                   	{{
	                                                   		{2}get => field;
	                                                   	}} = new {3}();
	                                                   """;

	private const string CollectionAccessorMethodTemplate = """
	                                                 	[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "get_{0}")]
	                                                 	private static extern {1} Get{0}({2} target);
	                                                 """;

	private const string InitializeMembersTemplate = """

	                                         	void {0}.Initialize()
	                                         	{{
	                                         		((global::Cat.Network.INetworkObject)this).PropertyStates = new global::Cat.Network.NetworkPropertyState[Properties.Length];
	                                         		{1}
	                                         	}}
	                                         """;
}
