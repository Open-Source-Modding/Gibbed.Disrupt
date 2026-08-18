using System;

namespace Gibbed.Disrupt.FileFormats.Hashing
{
    /// <summary>
    /// WD1 "FNV32" hash — computed as FNV1a64(lowercase) truncated to uint32.
    /// The engine uses this for file name lookups in BigFileV3 archives.
    /// Source: wasdennnoch/WDHashHelpAPI hasher.ts
    /// </summary>
    public static class FNV1a32
    {
        public static uint Compute(string value)
        {
            if (value == null || value.Length == 0)
            {
                return 0xFFFFFFFFu;
            }

            // Compute FNV1a64 on lowercased input, then truncate to uint32
            // This matches BigFileV3.ComputeNameHash exactly
            var hash64 = FNV1a64.Compute(value.ToLowerInvariant());
            var hash32 = (uint)hash64;

            // Special case: if high 16 bits are all 1s, clear bit 16
            // This avoids hash collisions with reserved values
            if ((hash32 & 0xFFFF0000) == 0xFFFF0000)
            {
                return hash32 & ~(1u << 16);
            }
            return hash32;
        }
    }
}
