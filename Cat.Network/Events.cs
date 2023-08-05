using System;

namespace Cat.Network;


[AttributeUsage(AttributeTargets.Property)]
public class PropertyChangedEventAttribute : Attribute {
	
	
}

public struct PropertyChangedEventArgs<T> {
	public T PreviousValue { get; init; }
	public T CurrentValue { get; init; }
}

public delegate void NetworkPropertyChanged<TEntity, TProperty>(TEntity sender, PropertyChangedEventArgs<TProperty> args);