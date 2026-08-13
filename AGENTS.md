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

## Key users/contacts
- **qstlijku** — Gibbed Tools fork with fixes, UnpackWD2 source (https://github.com/qstlijku/UnpackWD2), DisruptEd fork, PY-DuniaAnimationExtractor, DETest (Disrupt Editor), ctos-server, triangle-injection
- **HardVatsuki** — WD1ModdingKit, modding tool collection
- **Para** — TextureTools package
- **Expanded Mode** — binary object definitions
