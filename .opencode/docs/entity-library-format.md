# Entity Library Format (DisruptEd/FCBastard)

## Overview
Entity libraries (.fcb) are a specialized Nomad format used by the Disrupt engine
to store entity prototypes (vehicle archetypes, props, etc.) with a lookup table
mapping UIDs to object offsets.

## File Structure

### Header (8 bytes)
| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0 | 4 | infosOffset | Absolute file offset where prototype table begins |
| 4 | 4 | infosCount | Number of prototype entries |

### Nomad Root Object (starts at offset 8)
Standard Nomad binary object with magic "FCbn" (0x4643626E).

#### Nomad Header (after magic)
| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| +0 | 4 | Magic | 0x4643626E ("FCbn") |
| +4 | 2 | Format | FormatType (0x4005 = Entities) |
| +6 | 2 | Flags | Header flags |
| +8 | 4 | nElems | Total element count |
| +12 | 4 | nAttrs | Total attribute count |

### FormatType.Entities (FmtB)
Uses 24-bit offset references instead of index-based references.

- DescriptorTag size: 4 bytes (not 5)
- Object references use absolute offsets (not indices)
- Attribute data stored separately from attribute hash list

### Prototype Table (at infosOffset)
Array of entries, each 12 bytes (32-bit) or 16 bytes (64-bit):

#### 32-bit Entry (12 bytes)
| Offset | Size | Field |
|--------|------|-------|
| 0 | 4 | UID (uint32) |
| 4 | 4 | Offset (int32) — absolute offset of Nomad object in file |
| 8 | 2 | TotalCount (uint16) |
| 10 | 2 | ChildCount (uint16) |

#### 64-bit Entry (16 bytes)
| Offset | Size | Field |
|--------|------|-------|
| 0 | 8 | UID (int64) |
| 8 | 4 | Offset (int32) |
| 12 | 2 | TotalCount (uint16) |
| 14 | 2 | ChildCount (uint16) |

### Use64Bit Detection
```
Use64Bit = (fileSize - (infosCount * 12)) != infosOffset
```
If the math doesn't match for 12-byte entries, use 16-byte (64-bit) entries.

## Key Differences from Gibbed.Disrupt

1. **8-byte header**: Gibbed's BinaryObjectFile expects magic at offset 0.
   Entity libraries have infosOffset/infosCount before the magic.
2. **FormatType.Entities (FmtB)**: Uses 24-bit offset references.
   Gibbed only handles FmtA (index-based references).
3. **Prototype table**: Additional data after the root object mapping UIDs to offsets.
   Gibbed doesn't parse this.

## Reference: DisruptEd Source
- `Source/Nomad/Serializers/EntityLibrary/EntityLibrarySerializer.cs`
- `Source/Nomad/Serializers/EntityLibrary/EntityPrototypeInfo.cs`
- `Source/Nomad/Types/Descriptors/DescriptorTag.cs` (24-bit offset handling)
- `Source/Nomad/Serializers/NomadResourceSerializer.cs` (FmtA vs FmtB dispatch)
