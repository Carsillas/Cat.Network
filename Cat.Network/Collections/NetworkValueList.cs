using System;
using System.Collections.Generic;

namespace Cat.Network.Collections; 

public sealed class NetworkValueList<T> : NetworkList<T> {
	
	private bool FixedSize { get; }

	public NetworkValueList(NetworkEntity owner, List<T> list, bool fixedSize) : base(owner, list) {
		FixedSize = fixedSize;
	}

	protected override void AssertValidAddition(T item) {
		base.AssertValidAddition(item);
		
		if (FixedSize) {
			throw new InvalidOperationException("Attempted to modify a collection of fixed size.");
		}
	}

	protected override void AssertValidRemoval() {
		base.AssertValidRemoval();
		
		if (FixedSize) {
			throw new InvalidOperationException("Attempted to modify a collection of fixed size.");
		}
	}
}