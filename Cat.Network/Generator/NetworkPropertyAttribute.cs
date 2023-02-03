using System;
using System.Reflection;

namespace Cat.Network.Generator {

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
	public class NetworkPropertyAttribute : Attribute {
		public NetworkPropertyAttribute(AccessModifier accessModifier, Type propertyType, string name) {
			AccessModifier = accessModifier;
			PropertyType = propertyType;
			Name = name;
		}

		public string Name { get; }

		public Type PropertyType { get; }

		public AccessModifier AccessModifier { get; } = AccessModifier.Private;

	}

}
