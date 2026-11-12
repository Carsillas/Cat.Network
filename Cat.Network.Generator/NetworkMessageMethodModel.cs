using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Generator;

internal sealed class NetworkMessageMethodModel : IEquatable<NetworkMessageMethodModel> {
	private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Included,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		SymbolDisplayGenericsOptions.IncludeTypeParameters,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

	private NetworkMessageMethodModel(NetworkMessageKind kind, NetworkMessageReceiveModeModel receiveMode, string declaringTypeName, string name, string accessibility, bool hasNewModifier, ulong id, ImmutableArray<NetworkMessageParameterModel> parameters) {
		Kind = kind;
		ReceiveMode = receiveMode;
		DeclaringTypeName = declaringTypeName;
		Name = name;
		Accessibility = accessibility;
		HasNewModifier = hasNewModifier;
		Id = id;
		Parameters = parameters;
	}

	public NetworkMessageKind Kind { get; }

	public NetworkMessageReceiveModeModel ReceiveMode { get; }

	public string DeclaringTypeName { get; }

	public string Name { get; }

	public string Accessibility { get; }

	public bool HasNewModifier { get; }

	public ulong Id { get; }

	public ImmutableArray<NetworkMessageParameterModel> Parameters { get; }

	public static NetworkMessageMethodModel Create(IMethodSymbol method, AttributeData attribute, NetworkMessageKind kind) {
		ImmutableArray<NetworkMessageParameterModel> parameters = method.Parameters
			.Select(NetworkMessageParameterModel.Create)
			.ToImmutableArray();

		return new NetworkMessageMethodModel(
			kind,
			GetReceiveMode(attribute),
			method.ContainingType.ToDisplayString(FullyQualifiedTypeFormat),
			method.Name,
			NetworkMemberAccessibility.GetDeclaration(method),
			NetworkMemberAccessibility.HasNewModifier(method),
			CreateStableMessageId(method),
			parameters);
	}

	public bool Equals(NetworkMessageMethodModel? other) {
		return other is not null &&
		       Kind == other.Kind &&
		       ReceiveMode == other.ReceiveMode &&
		       DeclaringTypeName == other.DeclaringTypeName &&
		       Name == other.Name &&
		       Accessibility == other.Accessibility &&
		       HasNewModifier == other.HasNewModifier &&
		       Id == other.Id &&
		       Parameters.SequenceEqual(other.Parameters);
	}

	public override bool Equals(object? obj) {
		return obj is NetworkMessageMethodModel other && Equals(other);
	}

	public override int GetHashCode() {
		unchecked {
			int hashCode = (int)Kind;
			hashCode = (hashCode * 397) ^ (int)ReceiveMode;
			hashCode = (hashCode * 397) ^ DeclaringTypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ Name.GetHashCode();
			hashCode = (hashCode * 397) ^ Accessibility.GetHashCode();
			hashCode = (hashCode * 397) ^ HasNewModifier.GetHashCode();
			hashCode = (hashCode * 397) ^ Id.GetHashCode();
			foreach (NetworkMessageParameterModel parameter in Parameters) hashCode = (hashCode * 397) ^ parameter.GetHashCode();
			return hashCode;
		}
	}

	private static NetworkMessageReceiveModeModel GetReceiveMode(AttributeData attribute) {
		if (attribute.ConstructorArguments.Length == 1 &&
		    attribute.ConstructorArguments[0].Value is int receiveMode &&
		    receiveMode == 1) {
			return NetworkMessageReceiveModeModel.Explicit;
		}

		return NetworkMessageReceiveModeModel.Event;
	}

	private static ulong CreateStableMessageId(IMethodSymbol method) {
		string signature = method.Name + "(" + string.Join(",", method.Parameters.Select(static parameter => parameter.Type.ToDisplayString(FullyQualifiedTypeFormat))) + ")";
		byte[] bytes = Encoding.UTF8.GetBytes(signature);
		byte[] hash;
		using (SHA256 sha256 = SHA256.Create()) {
			hash = sha256.ComputeHash(bytes);
		}

		return BitConverter.ToUInt64(hash, 0);
	}
}
