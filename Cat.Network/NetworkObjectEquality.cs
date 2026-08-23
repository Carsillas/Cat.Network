using System.Collections.Generic;

namespace Cat.Network;

public static class NetworkObjectEquality {
	public static bool ListEquals<T>(IList<T> left, IList<T> right) {
		if (ReferenceEquals(left, right)) {
			return true;
		}

		if (left.Count != right.Count) {
			return false;
		}

		EqualityComparer<T> comparer = EqualityComparer<T>.Default;
		for (int index = 0; index < left.Count; index++) {
			if (!comparer.Equals(left[index], right[index])) {
				return false;
			}
		}

		return true;
	}

	public static bool DictionaryEquals<TKey, TValue>(IDictionary<TKey, TValue> left, IDictionary<TKey, TValue> right) where TKey : notnull {
		if (ReferenceEquals(left, right)) {
			return true;
		}

		if (left.Count != right.Count) {
			return false;
		}

		EqualityComparer<TValue> valueComparer = EqualityComparer<TValue>.Default;
		foreach ((TKey key, TValue leftValue) in left) {
			if (!right.TryGetValue(key, out TValue? rightValue) || !valueComparer.Equals(leftValue, rightValue)) {
				return false;
			}
		}

		return true;
	}

	public static int ListHashCode<T>(IList<T> items) {
		HashCode hash = new();
		foreach (T item in items) {
			hash.Add(item);
		}

		return hash.ToHashCode();
	}

	public static int DictionaryHashCode<TKey, TValue>(IDictionary<TKey, TValue> items) where TKey : notnull {
		int hashCode = 0;
		foreach ((TKey key, TValue value) in items) {
			HashCode pairHash = new();
			pairHash.Add(key);
			pairHash.Add(value);
			hashCode ^= pairHash.ToHashCode();
		}

		return hashCode;
	}
}
