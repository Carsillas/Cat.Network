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
|     0 | `Create` | Create/spawn an entity. Payload format TBD. |
|     1 | `Update` | Update entity state. Payload format TBD. |
|     2 | `Delete` | Delete/despawn an entity. Payload format TBD. |
|     3 | `Rpc` | Invoke an entity RPC. Payload format TBD. |
|     4 | `Broadcast` | Broadcast an entity-scoped message. Payload format TBD. |

## Notes

- The protocol payload begins at `NetworkMessageChannel`.
- Transport-level framing, buffering, and packet reassembly are transport responsibilities.
- Entity payload layouts are intentionally left open for future expansion.
