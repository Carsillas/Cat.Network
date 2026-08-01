using System;

namespace Cat.Network.Generator;

internal sealed class NetworkObjectUpgradeMethodModel : IEquatable<NetworkObjectUpgradeMethodModel> {
	public NetworkObjectUpgradeMethodModel(string name, ushort targetVersion) {
		Name = name;
		TargetVersion = targetVersion;
	}

	public string Name { get; }

	public ushort TargetVersion { get; }

	public bool Equals(NetworkObjectUpgradeMethodModel? other) {
		return other is not null &&
		       Name == other.Name &&
		       TargetVersion == other.TargetVersion;
	}

	public override bool Equals(object? obj) {
		return obj is NetworkObjectUpgradeMethodModel other && Equals(other);
	}

	public override int GetHashCode() {
		unchecked {
			return (Name.GetHashCode() * 397) ^ TargetVersion.GetHashCode();
		}
	}
}
