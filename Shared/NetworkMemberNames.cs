using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Shared;

internal static class NetworkMemberNames {
	public static bool IsNetworkMember(IPropertySymbol property) {
		return property.GetAttributes().Any(static attribute =>
			attribute.AttributeClass?.ToDisplayString() is "Cat.Network.NetworkPropertyAttribute" or "Cat.Network.NetworkCollectionAttribute");
	}

	public static IPropertySymbol? FindInheritedMemberWithSameName(IPropertySymbol property, CancellationToken cancellationToken) {
		for (INamedTypeSymbol? current = property.ContainingType.BaseType; current is not null; current = current.BaseType) {
			cancellationToken.ThrowIfCancellationRequested();
			// Private members still occupy names in the inherited serialization schema.
			foreach (IPropertySymbol candidate in current.GetMembers(property.Name).OfType<IPropertySymbol>()) {
				if (IsNetworkMember(candidate)) {
					return candidate;
				}
			}
		}

		return null;
	}

	public static bool HasInheritedNameCollision(INamedTypeSymbol type, CancellationToken cancellationToken) {
		// A descendant cannot emit an unambiguous schema if an ancestor already has a collision.
		for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType) {
			cancellationToken.ThrowIfCancellationRequested();
			foreach (IPropertySymbol property in current.GetMembers().OfType<IPropertySymbol>()) {
				if (IsNetworkMember(property) && FindInheritedMemberWithSameName(property, cancellationToken) is not null) {
					return true;
				}
			}
		}

		return false;
	}
}
