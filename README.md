# Cat.Network Protocol

This document describes the current packet framing plan. Multi-byte numeric fields are little-endian unless a later section explicitly says otherwise.

## Packet Structure

Every packet begins with a length prefix, followed by a one-byte message channel. Channel-specific fields follow after that.

| Offset | Size | Field | Type | Description |
|---:|---:|---|---|---|
| 0 | 4 bytes | Packet Length | `uint` | Number of bytes following the length prefix. This includes `NetworkMessageChannel` and all channel-specific payload bytes. |
| 4 | 1 byte | Channel | `NetworkMessageChannel` | Selects how the remaining packet bytes should be interpreted. |
| 5 | Variable | Channel Payload | Channel-specific | Payload format depends on `Channel`. |

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
| 5 | 1 byte | Entity Message Kind | `EntityMessageKind` | Selects the entity message format. |
| 6 | Variable | Entity Payload | Kind-specific | Payload format depends on `EntityMessageKind`. |

## EntityMessageKind

`EntityMessageKind` is encoded as a single byte.

| Value | Name | Description |
|---:|---|---|
| 0 | `Create` | Create/spawn an entity. Payload format TBD. |
| 1 | `Delete` | Delete/despawn an entity. Payload format TBD. |
| 2 | `Update` | Update entity state. Payload format TBD. |
| 3 | `Rpc` | Invoke an entity RPC. Payload format TBD. |
| 4 | `Broadcast` | Broadcast an entity-scoped message. Payload format TBD. |

## Notes

- The packet length prefix is not part of the counted length.
- The channel byte is part of the counted length.
- Entity payload layouts are intentionally left open for future expansion.
