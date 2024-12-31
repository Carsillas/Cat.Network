using System;

namespace Cat.Network;


[AttributeUsage(AttributeTargets.Property)]
public class PropertyChangedEventAttribute : Attribute {
	
	
}

public struct PropertyChangedEventArgs<T> {
	public int Index { get; init; }
	public string Name { get; init; }
	public T PreviousValue { get; init; }
	public T CurrentValue { get; init; }
}

public struct PropertyChangedEventArgs {
	
	public int Index { get; init; }
	public string Name { get; init; }
	
}

public delegate void NetworkPropertyChanged<in TEntity, TProperty>(TEntity sender, PropertyChangedEventArgs<TProperty> args);
public delegate void NetworkPropertyChanged(object sender, PropertyChangedEventArgs args);