using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Cat.Network.Generator {
	internal static class GeneratorUtils {

		public static Func<SyntaxNode, CancellationToken, bool> PassNodesOfType<T>() {
			return (syntaxNode, cancellationToken) => {
				if (syntaxNode is T) {
					return true;
				}
				return false;
			};
		}

		public static Func<SyntaxNode, CancellationToken, bool> PassNodesWithExplicitInterfaceSpecifier(string simpleName) {
			return (syntaxNode, cancellationToken) => {
				ExplicitInterfaceSpecifierSyntax explicitInterfaceSyntax = syntaxNode.ChildNodes().OfType<ExplicitInterfaceSpecifierSyntax>().FirstOrDefault();
				if (explicitInterfaceSyntax == null) {
					return false;
				}

				IdentifierNameSyntax identifierName = explicitInterfaceSyntax.DescendantNodes().OfType<IdentifierNameSyntax>().LastOrDefault();

				return identifierName?.Identifier.Text == simpleName;
			};
		}

		public static Func<SyntaxNode, CancellationToken, bool> And(Func<SyntaxNode, CancellationToken, bool> predicate1, Func<SyntaxNode, CancellationToken, bool> predicate2) {
			return (syntaxNode, cancellationToken) => {
				return predicate1(syntaxNode, cancellationToken) && predicate2(syntaxNode, cancellationToken);
			};
		}

	}
}
