# Gibbed.Disrupt

Tools for modding Disrupt-engine games (Watch Dogs series). Originally .NET Framework 4.7.2, ported to net8.0.

**User:** selene062398 — WD1 primary, also WD2/WDL

## Build

```bash
dotnet restore
dotnet build Disrupt.sln
```

Output goes to `bin/`. No test projects, no linter. Verify by building.

## Project structure

- `Gibbed.IO` — netstandard2.0+net472+net48+net5.0
- `Gibbed.Disrupt.BinaryObjectInfo` — net8.0 (class/object definition loader, field type handlers)
- `Gibbed.Disrupt.ConvertBinaryObject` — net8.0 (.fcb/.lib ↔ XML)
- `Gibbed.WatchDogs*.Unpack` / `Gibbed.WatchDogs*.Pack` — archive extract/repack
- `Gibbed.Disrupt.FileFormats` — net8.0 (BinaryObjectFile, BinaryObject, Hashing)
- Game-specific projects under solution folders: `Watch Dogs`, `Watch Dogs 2`, `Watch Dogs Legion`

## Definition files (`bin/projects/`)

Three game dirs: `Watch Dogs`, `Watch Dogs 2`, `Watch Dogs Legion`.
Each has `binary objects/classes/` (.binaryclass.xml), `binary objects/files/` (.binaryobjectfile.xml), `files/` (filelists), `generic_names.txt` (WD2 uses `generic_names_wd2.txt`).

Classes use `<inherit name="SharedParameters"/>` / `<inherit name="KnotsParameters"/>` for DRY.
Color fields use `type="VectorColor"` (hex `#RRGGBBAA` in XML output).
Generic vectors use `type="Vector"` (adaptive: 8/12/16 bytes → 2/3/4 floats).

### Field types
- `Vector`, `Vector2`, `Vector3`, `Vector4`, `VectorColor`, `Quaternion`
- `Float`, `Boolean`, `Int8/16/32/64`, `UInt8/16/32/64`, `String`
- `BinHex` (unknown / opaque), `Enum`, `Rml`
- `StringId`, `NoCaseStringId`, `PathId` (+ 64-bit variants for WDL)
- `Array32`

### Color naming convention
- `color*`, `clr*` prefixed fields → `VectorColor`
- `vec2*` → `Vector2`, `vector4*` → `Vector4`
- Unknown fields fall back to `BinHex` safely

## Pointer preservation
- Export emits `<object id="N">` / `<object ref="N"/>` via `Dictionary<BinaryObject, int>`
- Import resolves via `_importedObjects` list
- `BinaryObject.Equals` uses value equality (`NameHash` + `Fields`); `GetHashCode` added

## Sources of definitions
- Original Gibbed repo (now lost from git, survived in `~/Downloads/modding tools-61-1-1706992415.zip`)
- `binary objects.zip` from Expanded Mode (Discord) — had `type="Vector"` generic, many BinHex
- `Gibbed-Tools-main.zip` from qstlijku — WDL-specific files
- WD1/WD2 generic_names from various sources merged

## Texture workflow (separate tools)

See `~/Tools/WD Modding Tools/AGENTS.md`. TL;DR:
- XBT ↔ DDS via hardVatsuki's WD1ModdingKit
- DDS ↔ PNG via texconv (TextureTools)
- texconv CANNOT read XBT directly

## XBT texture format (modding community knowledge)

### Header layout (PC, verified via hex analysis + round-trip)
- `+0x00`: magic `TBX\x00` (0x00584254 LE)
- `+0x04`: u32 platform/build version — **0x92** (WDL BC7, 52-byte hdr), **0x8F** (WD1 DXT5, 44-byte hdr)
- `+0x08`: u32 header size (0x34=52 or 0x2C=44)
- `+0x0C`: u32 flags (usually 0)
- `+0x10`: u32 format ID — 0x02000003 (BC7), 0x01040401 (DXT5), 0x01010101 (DX10 array)
- `+0x14`: u32 format hint (2 for BC7, 1 for DXT5)
- `+0x18`: u32 packed metadata — byte at +0x19 distinguishes mip level (0xFF010101=high, 0xFF010301=med)
- `+0x1C`: u32 asset CRC/hash (same across mip levels — game-internal asset ID, NOT filename or data CRC)
- `+0x20`: u32 metadata (0x72 for BC7, 0x02 for DXT5)
- `+0x24`: u32 unknown (varies)
- `+0x28`: u32 padding

### Platform differences
- **Xbox 360** uses big-endian header + tiled GPU textures (requires `imageUntile360DXT` in Noesis)
- **PC** stores raw DDS data after header — standard DXT1/DXT5/BC7/DX10
- Version byte at 0x04 must match target platform/build or the game rejects the texture
- No embedded path string in PC XBT headers — path is stored externally (material XML lookup)

### Modding workflow
- **XBT → DDS:** Strip 0x34-byte header, save header separately as `.xbt.header`
- **Edit DDS** in any image editor using correct compression format (match original fourCC)
- **DDS → XBT:** Prepend saved header back — **bit-perfect round trip**
- DO NOT use `dds2xbt` converter: it generates a generic header that breaks textures
- Use hex editor to replace DDS data directly in XBT while preserving the header
- The `0x18` field encodes a link to `_high` mip variant; game rarely loads `_high` even on Ultra without header changes or `AlwaysMip0Loading` config tweak
- Normal maps may need extra processing when porting across platforms

### Noesis Xbox 360 plugin (`dunia_xbt.py`)
- Big-endian: all header u32s byte-swapped via `swap32()`
- Format: `(fmt & 0x3F)` → 18=DXT1, 19=DXT3, 20=DXT5, 6=RAW, plus ATI1/ATI2 normal maps
- Dimension packed at header offset `dataOffset-0x10`: 11+11+10 bits (WDH) or 13+13 bits (WH)
- GPU data is tiled (morton order) — requires `imageUntile360DXT` / `imageUntile360Raw`
- `Gamma Converter` tool recommended for brightness correction after conversion

## FNV hash system
- **WD1** uses FNV-1 32-bit (FNV32) for asset identification
- **WD2/WDL** uses FNV-1a 64-bit (FNV64)
- ModBot hash lookup format: `!hash <text>` → returns FNV32 (WD1), FNV64 (WD2), CRC32
- Hashes displayed in both little-endian and big-endian hex formats
- XBG files embed a 64-bit FNV-1a hash at file offset 0x18 for cache validation
- Reference: `Gibbed.Disrupt.FileFormats/Hashing/FNV64.cs`, `Gibbed.Disrupt.FileFormats/Hashing/CRC32.cs`

## Known issues / pitfalls

### ConvertBinaryObject "type mismatch"
```
Unhandled Exception: System.ArgumentException: type mismatch
Parameter name: def
```
Caused by mismatched binaryclass.xml definitions — the XML describes a class layout that doesn't match the binary being converted. Fix by updating definitions for the correct game version.

### Gibbed pack crashing on specific types
- WLU, FCB, BIN files pack successfully but cause game crash on boot when included
- Same pack works fine for OBJ, LIB, SFX, XBT
- Likely a bug in the repacker for certain binary types — files aren't being packed correctly even though no error is shown
- Works correctly for Xbox 360 (same Gibbed version), suggesting platform-specific issue
- Remux the file to XML and check for errors before repacking

### Texture mip chain
- Game has `_high`, `_med`, `_low` XBT variants per texture
- The 0x18 field in the header links mip levels together
- Even on Ultra settings, game often uses `_med` textures and ignores `_high`
- Workaround: rename `_high` textures to remove suffix, or edit the render config's `AlwaysMip0Loading`
- VB/PS3 emulator modding may need XBT header format RE for the specific platform

## Tool references from modding community

- **WD2ModelStudio.exe** (`Xbg.Net48`, author "Frank", source `C:\Users\Frank\source\repos\Xbg.Net48`) — standalone WD2 model viewer/editor with full XBG parse, HKX collision, material editor, OBJ injection, bounds analysis, FNV64 hash tool
- **hV_WD_ModdingKit_PLUS** — hardVatsuki's collection: BConv64 (XBT/SBAO converter), MaterialConverter, Gibbed tools, Noesis, TextureTools (crunch + texconv), ZModeler
- **WDTextureTools** (`hV_WD1_ModdingKit.exe`) — texconv + batch scripts for DDS/PNG/XBT conversion
- **WD2SModelStdio_BETA-PREVIEW.zip** — WPF .NET 4.8 WD2 model tool with full class hierarchy (XbgParser, XbgMeshInjectorFixed, HkxParserDisrupt, etc.)

### Asset dependency pipeline (from Ubisoft leak)
- `adp_lib.py` (4261 lines) at `~/Code/re/ubisoft/extracted/td_tools/PythonTools/DDV/adp_lib.py` — complete asset dependency resolver
- Geometry XML sidecar format: `<entities model_file="*.glm">` with `<material_reference_list>`, `<havok_primitive>`, `<trimesh>`, `<primitivelod>`
- Material XML format: `<material matBaseMaterial="..." shader="...">` with `<parameter name="..." value="..."/>`
- Material descriptors at `~/Code/re/ubisoft/extracted/data/engine/shaders/materialdescriptors/` — 85 XMLs defining shader parameters per material family
- DAG/PolyMesh binary API: `gx.data.DAG.FromBuffer()`, `PolyMesh.FromBytes()` (library not in leak, only references)

## Key users/contacts
- **qstlijku** — Gibbed Tools fork with fixes, UnpackWD2 source (https://github.com/qstlijku/UnpackWD2), DisruptEd fork, PY-DuniaAnimationExtractor, DETest (Disrupt Editor), ctos-server, triangle-injection
- **HardVatsuki** — WD1ModdingKit, modding tool collection
- **Para** — TextureTools package
- **Expanded Mode** — binary object definitions
- **Scuba** — XBT header RE, cross-platform XBT porting (360↔PC), normal map handling
- **The Silver** — XBT mip chain linking in header, binary object definition contributions
- **Miru (みる97)** — animation modding, texture format expertise
