using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cat.Network.Analyzer;

internal static class NetworkAnalyzerHelpers {
	// Invalid generic structs can expand forever without repeating a constructed
	// symbol. Bound field nesting as well as tracking the active symbol path.
	public const int MaxStructNestingDepth = 128;

	public static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType) {
		for (INamedTypeSymbol? current = type.BaseType; current is not null; current = current.BaseType)
			if (SymbolEqualityComparer.Default.Equals(current, baseType)) {
				return true;
			}

		return false;
	}

	public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType) {
		return symbol.GetAttributes().Any(attribute =>
			SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType));
	}

	public static bool IsPartial(INamedTypeSymbol type, CancellationToken cancellationToken) {
		return type.DeclaringSyntaxReferences
			.Select(reference => reference.GetSyntax(cancellationToken))
			.OfType<TypeDeclarationSyntax>()
			.Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
	}

	public static bool IsPartial(IPropertySymbol property, CancellationToken cancellationToken) {
		return property.DeclaringSyntaxReferences
			.Select(reference => reference.GetSyntax(cancellationToken))
			.OfType<PropertyDeclarationSyntax>()
			.Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
	}

	public static bool IsPartial(IMethodSymbol method, CancellationToken cancellationToken) {
		return method.DeclaringSyntaxReferences
			.Select(reference => reference.GetSyntax(cancellationToken))
			.OfType<MethodDeclarationSyntax>()
			.Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
	}
}
