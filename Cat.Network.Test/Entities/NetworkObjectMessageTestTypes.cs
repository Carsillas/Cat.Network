namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class NetworkObjectMessageState : NetworkEntity {
	[RPC]
	public partial void ApplyNullableBase(NetworkObject? value);

	[RPC]
	public partial void ApplyBase(NetworkObject value);

	[RPC]
	public partial void ApplyNullableDerived(RelayMessagePayload? value);

	[Broadcast]
	public partial void PublishNullableBase(NetworkObject? value);

	[Broadcast]
	public partial void PublishBase(NetworkObject value);

	[Broadcast]
	public partial void PublishNullableDerived(RelayMessagePayload? value);
}
