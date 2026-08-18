# Mission: Integrate DisruptEd/FCBastard fixes into Gibbed.Disrupt

## Context
FCBastard (nCrypTΞD's encrypted fork) handles entity library FCB files for WDL vehicle modding.

## Completed
- [x] M1: WDL attribute type definitions (Objectstaticdata.objParameters restored)
- [x] M2: Type catalog merged into KnownFields.cs (290 new WDL entries from types.user.xml)
- [x] M3: String lookup tables wired (StringLookup.cs loads strings.txt + strings.user.txt at runtime)
- [x] M4: Entity library format documented (.opencode/docs/entity-library-format.md)
- [x] M5: Build verified (0 errors, WD1 duplicate fixed, no regression)
- [x] M6: current.txt set to "Watch Dogs Legion" for WDL work

## Results
- KnownFields: 1399 → 1689 entries (290 WDL-specific additions)
- Export: 193/250 fields resolved by name in omnis.fcb (was mostly BinHex before)
- String lookup: loads 562K strings at startup for CRC32 reverse resolution
- Entity library FmtB parser: documented, deferred to future session (requires significant porting work)

## Known Limitations
- Entity library FmtB parser (24-bit offsets) — requires porting DisruptEd's NomadSerializer
- Some WDL definition files have Vector3/VectorColor size mismatches (pre-existing)
