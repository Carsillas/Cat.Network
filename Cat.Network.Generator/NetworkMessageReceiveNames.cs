using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Generator;

internal static class NetworkMessageReceiveNames {
	public static ImmutableArray<NetworkMessageMethodModel> Assign(
		ImmutableArray<NetworkMessageMethodModel> declaredMessages,
		IEnumerable<NetworkMessageMethodModel> inheritedMessages) {
		// Allocate at the declaring type, so derived dispatch uses the API already emitted for each base.
		NetworkMessageMethodModel[] inherited = inheritedMessages.ToArray();
		NetworkMessageMethodModel[] declaredEvents = declaredMessages.Where(IsEvent).ToArray();
		HashSet<string> inheritedNames = new(inherited.Where(IsEvent).Select(static message => message.ReceiveName), StringComparer.Ordinal);
		HashSet<string> reservedNames = new(inheritedNames, StringComparer.Ordinal);
		reservedNames.UnionWith(declaredEvents.Select(static message => message.Name));
		HashSet<string> messageNames = new(declaredMessages.Concat(inherited).Select(static message => message.Name), StringComparer.Ordinal);
		HashSet<string> overloadedNames = new(
			declaredEvents.GroupBy(static message => message.Name, StringComparer.Ordinal)
				.Where(static group => group.Count() > 1)
				.Select(static group => group.Key),
			StringComparer.Ordinal);

		ImmutableArray<NetworkMessageMethodModel>.Builder result = ImmutableArray.CreateBuilder<NetworkMessageMethodModel>();
		foreach (NetworkMessageMethodModel message in declaredMessages) {
			if (!IsEvent(message) || (!overloadedNames.Contains(message.Name) && !inheritedNames.Contains(message.Name))) {
				result.Add(message);
				continue;
			}

			// The wire ID is already a deterministic hash of the full method signature. Reuse it
			// for the receive suffix without changing either the send method or the wire format.
			string receiveName = message.Name + "_" + message.Id.ToString("X16", CultureInfo.InvariantCulture);
			while (reservedNames.Contains(receiveName) ||
			       messageNames.Contains(receiveName + "Received") ||
			       messageNames.Contains(receiveName + "RpcHandler") ||
			       messageNames.Contains("Raise" + receiveName)) {
				receiveName += "_";
			}
			reservedNames.Add(receiveName);
			result.Add(message.WithReceiveName(receiveName));
		}
		return result.ToImmutable();
	}

	public static NetworkMessageMethodModel PreserveReferencedApi(NetworkMessageMethodModel message, INamedTypeSymbol declaringType) {
		if (!declaringType.DeclaringSyntaxReferences.IsEmpty || !IsEvent(message) || message.ReceiveName == message.Name) {
			return message;
		}

		// Older assemblies can contain inherited overloads with unqualified events on each
		// declaring type. Dispatch must target that existing interface instead of renaming it.
		string interfaceName = message.Kind == NetworkMessageKind.Rpc ? "RPC" : "Broadcast";
		INamedTypeSymbol? receiveInterface = declaringType.GetTypeMembers(interfaceName).FirstOrDefault();
		if (receiveInterface is not null &&
		    !receiveInterface.GetMembers(message.ReceiveName + "Received").OfType<IEventSymbol>().Any() &&
		    receiveInterface.GetMembers(message.Name + "Received").OfType<IEventSymbol>().Any() &&
		    receiveInterface.GetMembers("Raise" + message.Name).OfType<IMethodSymbol>().Any()) {
			return message.WithReceiveName(message.Name);
		}
		return message;
	}

	private static bool IsEvent(NetworkMessageMethodModel message) => message.ReceiveMode == NetworkMessageReceiveModeModel.Event;
}
