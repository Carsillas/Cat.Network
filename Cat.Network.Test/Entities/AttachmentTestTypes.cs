namespace Cat.Network.Test.Entities;

[NetworkObject]
public sealed partial class AttachmentNode : NetworkObject {
	[NetworkProperty]
	public partial int Value { get; set; }

	[NetworkProperty]
	public partial AttachmentNode? Child { get; set; }

	[NetworkProperty]
	public partial AttachmentNode? OtherChild { get; set; }

	[NetworkCollection]
	public partial NetworkList<AttachmentNode?> Children { get; }

	[NetworkCollection]
	public partial NetworkDictionary<int, AttachmentNode?> ChildrenByKey { get; }
}
