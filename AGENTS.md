# Gibbed.Disrupt

Tools for modding Disrupt-engine games (Watch Dogs series). Originally .NET Framework 4.7.2, ported to net8.0. **User:** selene062398 — WDL primary, also WD1/WD2.

## Build

```bash
git submodule update --init   # required — .csproj refs won't resolve without it
dotnet restore
dotnet build Disrupt.sln
```

Output goes to `bin/`. No test projects, no linter — verify by building. CI (`.github/workflows/build.yml`) builds **Debug** only, `windows-latest`, `submodules: recursive`.

**Submodules** (under `projects/`): Gibbed.IO, NDesk.Options, XCompression, Gibbed.ProjectData. Plus file-list submodules under `bin/projects/*/files/`. All must be initialized.

## Two `projects/` dirs — don't confuse them

- **`projects/`** (repo root) = C# **source code** (one folder per csproj).
- **`bin/projects/`** (build output) = **game definition data** + project selection. This is loaded at runtime, relative to the EXE location.

### Runtime project selection
`ProjectHelpers.LoadProject()` (in `Gibbed.Disrupt.FileFormats`) reads `bin/projects/current.txt`, then loads `bin/projects/<name>.xml` (or `.json`). Currently `Watch Dogs Legion`. To switch games, change `current.txt` (a `*.xml` project file must exist next to it).

Each `bin/projects/<Game>/` has:
- `binary objects/classes/` — `.binaryclass.xml` definitions
- `binary objects/files/` — `.binaryobjectfile.xml` definitions
- `files/` — file lists (git submodule)
- `strings.txt` + `strings.user.txt` — loaded by `StringLookup` (CRC32+FNV32 reverse lookup)
- `generic_names.txt` (WD2 uses `generic_names_wd2.txt`)

## Project structure

Solution has 19 csproj projects (Gibbed.LZ4LW on disk is NOT in the solution — see note). Key ones:

| Project | Purpose |
|---------|---------|
| `Gibbed.Disrupt.BinaryObjectInfo` | Definition loader, field type handlers, `StringLookup`, KnownFields |
| `Gibbed.Disrupt.ConvertBinaryObject` | **Main tool:** .fcb/.lib ↔ XML |
| `Gibbed.Disrupt.ConvertXml` | RML ↔ XML converter (separate `--rml`/`--xml` tool, uses `XmlResourceFile`) |
| `Gibbed.Disrupt.FileFormats` | `BinaryObjectFile`, `BinaryObject`, Big/archive + compression schemes, Hashing |
| `Gibbed.Disrupt.Packing` | Shared Unpack/Pack/RebuildFileLists logic; `EntryDecompression` |
| `Gibbed.WatchDogs.Packing` | `HashOverrides` (name-hash → override table) |
| `Gibbed.WatchDogs*` .Unpack/.Pack/.RebuildFileLists | Game-specific archive tools (thin wrappers over Disrupt.Packing) |
| `Gibbed.IO`, `NDesk.Options`, `XCompression`, `Gibbed.ProjectData` | Submodules. All target **net8.0 only** (legacy .NET Framework 4.x / netstandard2.0 / net5.0 targets dropped Aug 2026). |

Game-specific projects grouped in solution folders `Watch Dogs`, `Watch Dogs 2`, `Watch Dogs Legion`. Executable projects set `OutputPath=..\..\bin\` + `AppendTargetFrameworkToOutputPath=false` (so `.exe`s land directly in `bin/`).

> **Note:** `projects/Gibbed.LZ4LW/` (a standalone `LZ4LWDecoderStream`) is orphaned — referenced by no csproj/solution. The LZ4LW decoder was **inlined** into `Disrupt.Packing/EntryDecompression.cs` instead.

## ConvertBinaryObject usage

```bash
# Export .fcb/.lib → XML
bin/Gibbed.Disrupt.ConvertBinaryObject.exe --export input.fcb output.xml

# Import XML → .fcb/.lib
bin/Gibbed.Disrupt.ConvertBinaryObject.exe --import input.xml output.fcb
```

**Import extension auto-detect (recent):** if output path is omitted, the extension is derived from the XML root element's `name` attribute (`name="lib"` → `.lib`, `name="obj"` → `.obj`). If there's no `name` attr, it defaults to `.fcb`. So keep the `name` attr in exported XML or you'll silently get `.fcb`.

**`--nme` / `--no-multi-export` is currently a no-op.** In `Program.cs`, `useMultiExporting` defaults to `true` and the flag handler sets it to `v == null` (= `true` for a bare flag), so multi-export is effectively always on. Multi-export (creating a directory of per-object XML) runs automatically when a lib/entitylibrary/template is suitable. The old doc claimed `--nme` prevents a multi-export directory crash — that no longer matches the code; verify before trusting this flag.

## PS4 (Orbis) / compression support (recent, hard-earned)

`EntryDecompression.cs` + `FileFormats/Big/CompressionScheme*.cs` handle multiple schemes:
- **Orbis platform** (6) + **CompressionSchemeV9B** (v8/Orbis, CV=9, NHV=21 — 2013 PS4 beta): scheme 0 with size 0 = stored; scheme 0 with size = **LZMA** (1-byte leading flag before the standard LZMA header); scheme 4 = **LZ4LW** (in-place variant).
- LZMA uses `LZMA-SDK` package; LZ4 uses `K4os.Compression.LZ4.Streams`; classic XMemCompress uses `XCompression` + SharpZipLib.
- Verified against `windy_city.fat` (149013 entries) and `installpackage.fat` (31934 entries).

## Definition file conventions

- Classes use `<inherit name="SharedParameters"/>` / `<inherit name="KnotsParameters"/>` for DRY.
- Color fields → `type="VectorColor"` (hex `#RRGGBBAA` in XML). Generic vectors → `type="Vector"` (adaptive: 8/12/16 bytes → 2/3/4 floats).
- Field types: `Vector/2/3/4`, `VectorColor`, `Quaternion`, `Float`, `Boolean`, `Int8/16/32/64`, `UInt8/16/32/64`, `String`, `BinHex` (unknown/opaque), `Enum`, `Rml`, `StringId`, `NoCaseStringId`, `PathId` (+64-bit variants for WDL), `Array32`.
- Naming heuristics: `color*`/`clr*` → VectorColor, `vec2*` → Vector2, `vector4*` → Vector4; unknown fields fall back to `BinHex` safely.

## Pointer preservation

Export emits `<object id="N">` / `<object ref="N"/>` via `Dictionary<BinaryObject,int>`; import resolves via `_importedObjects`. `BinaryObject.Equals` is value equality (`NameHash` + `Fields`); `GetHashCode` added.

## Hash system

- **CRC32** — FCB field/object names (all games), case-sensitive. `FileFormats/Hashing/CRC32.cs`
- **FNV1a32 (WD1)** — `FNV1a64(lowercase(s))` truncated to uint32, with `0xFFFF0000` fix. NOT standard FNV-1a. `Hashing/FNV1a32.cs`
- **FNV1a64 (WD2/WDL)** — 64-bit FNV-1a with custom mask `0xA000000000000000 | (hash & 0x1FFFFFFFFFFFFFFF)`. `Hashing/FNV1a64.cs`
- `BigFileV3.ComputeNameHash` = `FNV1a64(s.ToLowerInvariant())` truncated, matching the FNV1a32 path.

## Known issues / pitfalls

- **ConvertBinaryObject "type mismatch"** (`System.ArgumentException: type mismatch / Parameter name: def`): XML describes a class layout that doesn't match the binary. Fix definitions for the correct game version.
- **Gibbed pack crashes on boot for WLU/FCB/BIN** while OBJ/LIB/SFX/XBT pack fine; works on Xbox 360 but not PC. Likely repacker bug for those binary types (files pack "successfully" but corrupt). Remux the file to XML and check for errors before repacking.
- **Texture mip chain:** `_high`/`_med`/`_low` XBT variants; header `0x18` field links mip levels; game often uses `_med` even on Ultra. Workaround: rename `_high` to drop suffix, or set `AlwaysMip0Loading`.

## XBT texture format (modding-community knowledge)

### PC header layout (verified via hex analysis + round-trip)
- `+0x00` magic `TBX\x00`; `+0x04` u32 platform/build version — **0x92** (WDL BC7, 52-byte hdr), **0x8F** (WD1 DXT5, 44-byte hdr); `+0x08` u32 header size (0x34/0x2C); `+0x0C` u32 flags (usually 0)
- `+0x10` format ID: 0x02000003 (BC7), 0x01040401 (DXT5), 0x01010101 (DX10 array); `+0x14` format hint (2 BC7 / 1 DXT5)
- `+0x18` packed metadata (byte at +0x19 = mip level: 0xFF010101 high / 0xFF010301 med); `+0x1C` asset CRC (game-internal, NOT filename/data CRC, same across mips); `+0x20` metadata; `+0x24` unknown; `+0x28` padding

### Platform differences
- **Xbox 360**: big-endian header + tiled GPU textures (needs `imageUntile360DXT` in Noesis). **PC**: raw DDS after header (DXT1/DXT5/BC7/DX10).
- Version byte at 0x04 must match target platform/build or game rejects the texture. No embedded path string in PC XBT headers (path stored externally in material XML).

### Modding workflow
- **XBT → DDS:** strip 0x34-byte header (save as `.xbt.header`), edit DDS in a matching-compression editor. **DDS → XBT:** prepend header back — bit-perfect round trip.
- **DO NOT use `dds2xbt`** — it writes a generic header that breaks textures. Replace DDS data in the XBT via hex editor, preserving the header.

### Noesis Xbox 360 plugin (`dunia_xbt.py`)
- Big-endian: header u32s byte-swapped via `swap32()`; format `(fmt & 0x3F)` → 18=DXT1, 19=DXT3, 20=DXT5, 6=RAW, +ATI1/ATI2 normal maps; dimensions packed at `dataOffset-0x10` (11+11+10 bits WDH, or 13+13 bits WH); GPU data tiled (morton) — needs `imageUntile360DXT`/`imageUntile360Raw`; `Gamma Converter` for brightness.

## Sources of definitions

- Original Gibbed repo (lost from git, survived in `~/Downloads/modding tools-61-1-1706992415.zip`)
- `binary objects.zip` from Expanded Mode (Discord) — had `type="Vector"` generic, many BinHex
- `Gibbed-Tools-main.zip` from qstlijku — WDL-specific files
- WD1/WD2 generic_names from various sources merged

## Texture workflow (separate tools)

See `~/Tools/WD Modding Tools/AGENTS.md`. XBT ↔ DDS via hardVatsuki's WD1ModdingKit; DDS ↔ PNG via texconv; texconv CANNOT read XBT directly.

## Tool references

- **WD2ModelStudio.exe** — standalone WD2 model viewer/editor (Xbg.Net48, author "Frank")
- **hV_WD_ModdingKit_PLUS** — BConv64, MaterialConverter, Noesis, TextureTools, ZModeler
- **PackLegion** — insert individual files into WDL patch archive (no full repack)
- **FCBastard** — FMTB entity library parser (source at `~/Documents/Code/game-tools/encryptedsfbc/` — PRIVATE)
- **qstlijku** — Gibbed Tools fork with fixes, UnpackWD2, PY-DuniaAnimationExtractor
