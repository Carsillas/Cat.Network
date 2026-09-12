using System.Reflection;

namespace Cat.Network;

internal static class NetworkStructFields {
	public static bool HasSupportedShape(Type type, FieldInfo[] publicFields) {
		// A zero-field encoding is valid only when there is no hidden instance state.
		return !type.IsEnum && !type.IsPrimitive &&
		       (publicFields.Length > 0 || type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Length == 0);
	}
}
