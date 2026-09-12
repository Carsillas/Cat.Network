using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cat.Network.Generator;

internal static class NetworkTypeNames {
	public static string Identifier(string name) {
		return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
			? "@" + name : name;
	}

	public static string HintName(INamedTypeSymbol type) {
		Stack<string> segments = new();
		segments.Push(type.MetadataName);
		for (INamespaceSymbol? current = type.ContainingNamespace; current is not null && !current.IsGlobalNamespace; current = current.ContainingNamespace) {
			segments.Push(current.MetadataName);
		}
		string identity = string.Join(".", segments);
		StringBuilder encoded = new("T");
		StringBuilder readable = new();
		int segmentLength = 0;
		foreach (char character in identity) {
			// Fixed-width encoding is injective even on case-insensitive file systems.
			// Bound directory components and the readable filename for emitted files on Windows.
			if (segmentLength == 24) {
				encoded.Append('/');
				segmentLength = 0;
			}
			encoded.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
			segmentLength++;
			if (readable.Length < 100) {
				readable.Append(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' ? character : '_');
			}
		}
		// The identity directory separates both different types and their property/message/serializer output roles.
		return encoded + "/" + readable;
	}
}
