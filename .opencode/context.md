# Gibbed.Disrupt Project Context

## Environment
- C# / .NET 8.0, Build: `dotnet build Disrupt.sln` (0 errors)
- User: selene062398 — WD1 primary, also WD2/WDL

## Completed Work (Gibbed.Disrupt codebase)
1. Fixed WD1 duplicate class (Objectstaticdata.objParameters)
2. Added 290 WDL type hints to KnownFields.cs (1399→1689)
3. Created StringLookup.cs (CRC32 + FNV32 lookup)
4. Created FNV1a32.cs (WD1 FNV32)
5. Verified FNV32 algorithm matches wasdennnoch/WDHashHelpAPI
6. Copied FCBastard catalogs to WDL project dir
7. Documented entity library format (.opencode/docs/entity-library-format.md)
8. Built FCBastard from encryptedsfbc (PRIVATE)
9. **Fixed StringHandler.cs** — swapped bounds check order
10. **Fixed ConvertBinaryObject import** — reads XML root `name` attr; `name="lib"` → `.lib`

## Import/Export Type Detection
- Export detects by name hash: `EntityLibraries`→EntityLib, `lib`→Library, `NomadObjectTemplates`→NomadObj
- Import: `name="lib"` → `.lib`, everything else → `.fcb`
- Fork (Open-Source-Modding) has same default `.fcb` for import

## Active Task: Add Heavy_ArmoredTruck_01 to WDL Traffic

### Working Directory
`~/Documents/Modding/WDL/installpackage/`

### Changes Made
1. **vehiclespawninfo.xml** — Gibbed format (`<object name="lib" def="Vehiclespawninfo">`), added armored truck (id=180): `bIsTraffic=True`, `selVehicleType=14`, `bTruck=True`
2. **vehiclespawninfo.lib** — rebuilt via ConvertBinaryObject
3. **vehiclesbank/London._Basic_Mil_Truck.xml** — added armored truck entry
4. **vehiclesbank.lib** — rebuilt via ConvertBinaryObject
5. **entitylibrary.fcb** — rebuilt via FCBastard
6. **entitylibrary.xml** — path correct

### Still Needed
- Test in-game
