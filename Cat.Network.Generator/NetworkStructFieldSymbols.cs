using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Generator;

internal static class NetworkStructFieldSymbols {
	public static IEnumerable<IFieldSymbol> GetSerializableFields(INamedTypeSymbol type) {
		IEnumerable<IFieldSymbol> fields = type.GetMembers().OfType<IFieldSymbol>();
		if (type.IsTupleType) {
			// Roslyn exposes aliases and flattened Item8+ fields, even on TupleUnderlyingType.
			// Reflection sees only Item1..Item7 and Rest; recurse into Rest through the usual
			// struct model. CorrespondingTupleField keeps physical ItemN access for named elements.
			fields = type.TupleElements.Take(7)
				.Select(static element => element.CorrespondingTupleField ?? element)
				.Concat(fields.Where(static field => field.Name == "Rest"));
		}

		return fields
			.Where(static field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public)
			.OrderBy(static field => field.Name, StringComparer.Ordinal);
	}
}
