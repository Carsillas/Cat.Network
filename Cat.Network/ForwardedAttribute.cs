using System;

namespace Cat.Network; 

[AttributeUsage(AttributeTargets.Property)]
public class ForwardedAttribute : Attribute {
	public string AttributeSource { get; }

	public ForwardedAttribute(string attributeSource) {
		AttributeSource = attributeSource;
	}
}