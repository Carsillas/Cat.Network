namespace Cat.Network;

public sealed class NetworkValueList<T>(NetworkObject owner, int propertyIndex) : NetworkList<T>(owner, propertyIndex);
