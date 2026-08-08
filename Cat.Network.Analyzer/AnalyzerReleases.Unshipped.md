; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

 Rule ID | Category | Severity | Notes                                                                  
---------|----------|----------|------------------------------------------------------------------------
 CN0001  | Usage    | Error    | NetworkEntity-derived types must be marked with NetworkEntityAttribute 
 CN0002  | Usage    | Error    | NetworkEntityAttribute can only be used on NetworkEntity-derived types 
 CN0003  | Usage    | Error    | NetworkEntityAttribute requires a partial type                         
 CN0004  | Usage    | Error    | NetworkPropertyAttribute can only be used in NetworkEntity-derived types
 CN0005  | Usage    | Error    | NetworkPropertyAttribute requires a partial property                   
 CN0006  | Usage    | Error    | NetworkPropertyAttribute requires get and set accessors                
 CN0007  | Usage    | Error    | NetworkPropertyAttribute cannot hide an inherited network property     
 CN0008  | Usage    | Error    | NetworkObject types cannot declare parameterized constructors          
 CN0009  | Usage    | Error    | NetworkObject types require a public parameterless constructor         
 CN0010  | Usage    | Error    | NetworkCollectionAttribute can only be used in NetworkObject-derived types
 CN0011  | Usage    | Error    | NetworkCollectionAttribute requires a partial property
 CN0012  | Usage    | Error    | NetworkCollectionAttribute requires a getter-only property
 CN0013  | Usage    | Error    | NetworkCollectionAttribute requires NetworkList<T> or NetworkDictionary<TKey, TValue>
 CN0014  | Usage    | Error    | NetworkCollectionAttribute properties cannot declare an initializer
 CN0015  | Usage    | Error    | NetworkCollectionAttribute requires a supported item type
 CN0016  | Usage    | Error    | NetworkCollectionAttribute requires a supported dictionary key type
 CN0017  | Usage    | Error    | UpgradeToAttribute can only be used in NetworkObject-derived types
 CN0018  | Usage    | Error    | UpgradeToAttribute requires a static upgrade method with the expected signature
 CN0019  | Usage    | Error    | UpgradeToAttribute target versions must be unique
 CN0020  | Usage    | Error    | UpgradeToAttribute target version must be supported by the NetworkObject schema version
 CN0021  | Usage    | Error    | RPCAttribute and BroadcastAttribute can only be used in NetworkEntity-derived types
 CN0022  | Usage    | Error    | RPCAttribute and BroadcastAttribute require non-generic partial void methods without ref, out, or in parameters
 CN0023  | Usage    | Error    | RPCAttribute and BroadcastAttribute require supported parameter types
 CN0024  | Usage    | Error    | RPCAttribute and BroadcastAttribute parameters cannot be NetworkEntity types
 CN0025  | Usage    | Warning  | Explicit RPCAttribute and BroadcastAttribute receive handlers should use explicit interface implementation
 CN0026  | Usage    | Error    | NetworkList<T> and NetworkDictionary<TKey, TValue> require NetworkCollectionAttribute
