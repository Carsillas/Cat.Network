using System;

namespace Cat.Network; 

[AttributeUsage(AttributeTargets.Parameter)]
public class ClientAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter)]
public class InstigatorAttribute : Attribute { }