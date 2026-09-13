# Gibbed's Disrupt Tools - Selene's Fork
Tools for modding various [Disrupt](https://en.wikipedia.org/wiki/Ubisoft#Disrupt)-based games, such as the Watch_Dogs series published by Ubisoft.

**Credit to hV for DefinitionsFixer!**

## Notice

*Experimental software.*

## Compression schemes

Real WDL (Legion) archives use **LZ4LW** (scheme 3) for the vast majority of
entries, with Oodle/LZMA rare and plain stored (scheme 0) for the rest. The
WDL `Pack` tool's `-c` flag compresses with LZ4LW accordingly.

**LZO1x is NOT used by WDL** — it never appears in any real `common.fat` /
`shadersobj.fat` / `patch.fat`. The dead LZO1x decompression branch
(`PlatformNotSupportedException`) was removed. LZO1x remains only as an enum
value for older legacy archive versions (BigFileV2/V3/V4/V5, other games).

## TODO

- Everything.
