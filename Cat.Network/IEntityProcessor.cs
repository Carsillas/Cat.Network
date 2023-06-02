using System.Collections.Generic;

namespace Cat.Network;

public interface IEntityProcessor {

	public struct FastEnumerable {
		private HashSet<NetworkEntity>.Enumerator Internal { get; }
		public FastEnumerable(HashSet<NetworkEntity>.Enumerator wrappedEnumerator) {
			Internal = wrappedEnumerator;
		}
		public HashSet<NetworkEntity>.Enumerator GetEnumerator() => Internal;
	}

	void DeleteEntity(NetworkEntity entity);
	void CreateOrUpdate(NetworkEntity entity);

	FastEnumerable RelevantEntities { get; }

}

