using System;

namespace Cat.Network; 

[AttributeUsage(AttributeTargets.Method)]
public class AutoEventAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class BroadcastAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter)]
public class ClientAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter)]
public class InstigatorAttribute : Attribute { }