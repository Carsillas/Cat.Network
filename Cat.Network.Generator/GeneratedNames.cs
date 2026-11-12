using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;

namespace Cat.Network.Generator;

// Keep symbol/wire names raw; escape only identifiers written into C# source.
internal sealed class GeneratedNames {
	private readonly HashSet<string> used;

	public GeneratedNames(IEnumerable<string>? reserved = null) {
		used = reserved is null ? new(StringComparer.Ordinal) : new(reserved, StringComparer.Ordinal);
	}

	public string Allocate(string preferredName) {
		string name = preferredName;
		for (int suffix = 1; !used.Add(name); suffix++) {
			name = preferredName + suffix;
		}
		return Escape(name);
	}

	public static string Escape(string name) {
		return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ||
		       SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
			? "@" + name
			: name;
	}
}
