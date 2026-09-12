using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cat.Network.Generator;

internal static class NetworkMemberAccessibility {
	public static bool HasNewModifier(ISymbol member) {
		return member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() switch {
			MethodDeclarationSyntax method => method.Modifiers.Any(SyntaxKind.NewKeyword),
			PropertyDeclarationSyntax property => property.Modifiers.Any(SyntaxKind.NewKeyword),
			_ => false
		};
	}

	public static string GetDeclaration(ISymbol member) {
		// Partial members distinguish omitted accessibility from an explicit private modifier.
		SyntaxNode? declaration = member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
		SyntaxTokenList modifiers;
		switch (declaration) {
			case MethodDeclarationSyntax method:
				modifiers = method.Modifiers;
				break;
			case PropertyDeclarationSyntax property:
				modifiers = property.Modifiers;
				break;
			default:
				return GetKeywords(member.DeclaredAccessibility);
		}

		return string.Join(" ", modifiers
			.Where(static modifier => modifier.IsKind(SyntaxKind.PublicKeyword) ||
			                          modifier.IsKind(SyntaxKind.PrivateKeyword) ||
			                          modifier.IsKind(SyntaxKind.ProtectedKeyword) ||
			                          modifier.IsKind(SyntaxKind.InternalKeyword))
			.Select(static modifier => modifier.Text));
	}

	public static string GetKeywords(Accessibility accessibility) {
		return accessibility switch {
			Accessibility.Public => "public",
			Accessibility.Internal => "internal",
			Accessibility.Protected => "protected",
			Accessibility.Private => "private",
			Accessibility.ProtectedAndInternal => "private protected",
			Accessibility.ProtectedOrInternal => "protected internal",
			_ => "private"
		};
	}
}
