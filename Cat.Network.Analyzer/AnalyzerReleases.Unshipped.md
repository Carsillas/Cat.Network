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
