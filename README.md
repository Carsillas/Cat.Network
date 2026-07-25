# Cat.Network Protocol

This document describes the current protocol payload format delivered to message handlers. Multi-byte numeric fields are little-endian unless a later section explicitly says otherwise.

Transport implementations may apply their own framing on the wire, such as a length prefix, but that framing is not part of the protocol payload described here.

## Packet Structure

Every protocol payload begins with a one-byte message channel. Channel-specific fields follow after that.

| Offset | Size | Field | Type | Description |
|---:|---:|---|---|---|
| 0 | 1 byte | Channel | `NetworkMessageChannel` | Selects how the remaining payload bytes should be interpreted. |
| 1 | Variable | Channel Payload | Channel-specific | Payload format depends on `Channel`. |

## NetworkMessageChannel

`NetworkMessageChannel` is encoded as a single byte.

| Value | Name | Payload |
|---:|---|---|
| 0 | `Application` | Application-defined payload. |
| 1 | `EntityMessage` | Entity message payload. The next byte is `EntityMessageKind`. |

## EntityMessage Channel

When `NetworkMessageChannel` is `EntityMessage`, the channel payload begins with `EntityMessageKind`.

| Offset | Size | Field | Type | Description |
|---:|---:|---|---|---|
| 1 | 1 byte | Entity Message Kind | `EntityMessageKind` | Selects the entity message format. |
| 2 | Variable | Entity Payload | Kind-specific | Payload format depends on `EntityMessageKind`. |

## EntityMessageKind

`EntityMessageKind` is encoded as a single byte.

| Value | Name | Description |
|------:|---|---|
|     0 | `Create` | Create/spawn an entity. Payload includes an entity type id and object data. |
|     1 | `Update` | Update entity state. Payload includes object data. |
|     2 | `Delete` | Delete/despawn an entity. Payload format TBD. |
|     3 | `Rpc` | Invoke an entity RPC. Payload format TBD. |
|     4 | `Broadcast` | Broadcast an entity-scoped message. Payload format TBD. |

## Create Message

`Create` entity messages currently use this layout after the `EntityMessageKind` byte:

| Offset | Size | Field | Type | Description |
|---:|---:|---|---|---|
| 2 | 16 bytes | Entity Id | `Guid` | Unique id for the created entity. |
| 18 | 16 bytes | Type Id | `Guid` | Stable type identifier for the entity type. |
| 34 | Variable | Object Data | Object payload | Serialized member data for the created object. |

## Update Message

`Update` entity messages currently use this layout after the `EntityMessageKind` byte:

| Offset | Size | Field | Type | Description |
|---:|---:|---|---|---|
| 2 | 16 bytes | Entity Id | `Guid` | Unique id for the target entity. |
| 18 | Variable | Object Data | Object payload | Serialized member data for the target object. |

## Object Data

Object data is a self-delimiting sequence of serialized fields.

| Order | Size | Field | Type | Description |
|---:|---:|---|---|---|
| 1 | 1 byte | Member Identification Mode | `MemberIdentificationMode` | Controls how each field is identified. |
| 2 | 2 bytes | Field Count | `ushort` | Number of serialized fields that follow. |
| 3 | Variable | Field Entries | Repeated | One entry per serialized field. |

Each field entry has this shape:

- Identifier:
  - `Index` mode: `2 bytes` for the member index as a `ushort`.
  - `Name` mode: `4 bytes` for UTF-8 byte length, followed by the UTF-8 member name bytes.
- `4 bytes` for the serialized value byte length.
- `N bytes` of serialized value data, where `N` is the preceding value byte length.

For non-`NetworkObject` member types, the value bytes are the serialized representation of that member.

For nullable value types such as `int?`, `Guid?`, or `float?`, the value bytes use this nested shape:

- `1 byte` presence flag
- if the flag is `0`, the value is `null` and no additional bytes are present
- if the flag is `1`, the remaining bytes are the serialized representation of the underlying non-nullable value type

Any other presence flag value is invalid.

For struct member types, the value bytes are the serialized representation of that struct's public instance fields in ascending ordinal field-name order.

- only public instance fields are included
- supported struct field types are the same scalar types already supported as member values:
  - primitive numeric types
  - `bool`
  - `string`
  - `Guid`
  - nullable versions of those value types
- supported nested struct fields recurse using this same struct-field encoding
- nullable nested struct fields use the nullable struct encoding described below
- `NetworkObject` fields inside structs are not supported
- unsupported struct field types make the containing struct unsupported for generated deserialization
- cyclic or self-recursive struct layouts are not supported

Struct field values are encoded sequentially with no per-field identifier metadata:

- fixed-width primitive and `Guid` fields use their normal little-endian byte representation
- `string` fields use `4 bytes` of UTF-8 byte length followed by the UTF-8 bytes
- nullable value-type fields use the nullable value-type encoding described above
- nested struct fields use their own sequential struct payload directly inline

Nullable struct member types use this nested shape:

- `1 byte` presence flag
- if the flag is `0`, the value is `null` and no additional struct bytes are present
- if the flag is `1`, the remaining bytes are the serialized struct payload described above

Any other presence flag value is invalid.

For `NetworkObject` member types, the value bytes contain a nested object update payload:

- `1 byte` object update mode
- mode-specific content

The nested object update modes are:

- `Modify`:
  - the remaining bytes are passed directly to the existing nested object's deserializer
- `Replace`:
  - `16 bytes` type id as a `Guid`
  - the remaining bytes are passed to the replacement object's deserializer
  - the containing deserializer is responsible for constructing the replacement object from `TypeCatalogue` and assigning it before deserializing the payload
- `Clear`:
  - no additional bytes are required
  - the containing deserializer clears the existing nested object reference

## MemberIdentificationMode

`MemberIdentificationMode` is encoded as a single byte.

| Value | Name | Description |
|---:|---|---|
| 0 | `Index` | Members are identified by their generated numeric index. Intended for network payloads. |
| 1 | `Name` | Members are identified by their UTF-8 name. Intended for storage-oriented payloads. |

## ObjectUpdateMode

`ObjectUpdateMode` is encoded as a single byte inside the value payload for `NetworkObject`-typed members.

| Value | Name | Description |
|---:|---|---|
| 0 | `Modify` | Apply the nested payload to the existing object instance. |
| 1 | `Replace` | Construct a replacement object from the nested type id and apply the nested payload to it. |
| 2 | `Clear` | Clear the current nested object reference without applying a nested payload. |

## Notes

- The protocol payload begins at `NetworkMessageChannel`.
- Transport-level framing, buffering, and packet reassembly are transport responsibilities.
- Multi-byte numeric fields in object data are little-endian.
- `Index` mode is intended for over-the-wire payloads.
- `Name` mode is intended for disk or storage payloads.
