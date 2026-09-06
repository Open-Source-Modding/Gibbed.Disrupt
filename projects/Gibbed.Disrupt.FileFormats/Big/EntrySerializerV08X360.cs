/* Copyright (c) 2020 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System.IO;
using Gibbed.IO;

namespace Gibbed.Disrupt.FileFormats.Big
{
    // X360 FAT2 entry format: 24 bytes.
    //
    // Unlike FAT3 V08's 16-byte entries, FAT2 has a 64-bit hash, a full 32-bit
    // uncompressed size, and a 0x7F7F7F7F sentinel word.
    //
    // The compressed-size + offset encoding matches FAT3 V08's split scheme:
    //
    // [hash]     u64   CRC64 name hash
    // [field_b]  u32   uncompressed size (full 32 bits)
    // [sentinel] u32   always 0x7F7F7F7F
    // [field_d]  u32   compressed_size[28:0] | offset_low3[31:29]
    // [field_e]  u32   offset_high28[27:0]
    //
    // Offset = (field_e << 3) | (field_d >> 29)   [35-bit byte offset]
    // CompressedSize = field_d & 0x1FFFFFFF        [29-bit compressed size]
    //
    // There is NO per-entry compression scheme in FAT2 — all X360 entries
    // use XMemCompress (scheme 3), determined by the archive header's
    // compressionVersion field.
    internal class EntrySerializerV08X360 : IEntrySerializer<ulong>
    {
        public void Serialize(Stream output, Entry<ulong> entry, Endian endian)
        {
            output.WriteValueU64(entry.NameHash, endian);
            output.WriteValueU32((uint)entry.UncompressedSize, endian);
            output.WriteValueU32(0x7F7F7F7Fu, endian); // sentinel

            // Pack compressed size (low 29 bits) and offset_low3 (high 3 bits) into field_d.
            uint fieldD = 0;
            fieldD |= (uint)(entry.CompressedSize & 0x1FFFFFFFu);
            fieldD |= (uint)((entry.Offset & 0x7u) << 29);
            output.WriteValueU32(fieldD, endian);

            // Pack offset_high28 into field_e.
            output.WriteValueU32((uint)((entry.Offset >> 3) & 0x1FFFFFFF), endian);
        }

        public void Deserialize(Stream input, Endian endian, out Entry<ulong> entry)
        {
            var hash = input.ReadValueU64(endian);
            var fieldB = input.ReadValueU32(endian);  // uncompressed size
            var sentinel = input.ReadValueU32(endian); // should be 0x7F7F7F7F
            var fieldD = input.ReadValueU32(endian);   // compressed_size[28:0] | offset_low3[31:29]
            var fieldE = input.ReadValueU32(endian);   // offset_high28[27:0]

            // Decode split offset (same scheme as FAT3 V08).
            var compressedSize = (int)(fieldD & 0x1FFFFFFFu);
            var offsetLow3 = (fieldD >> 29) & 0x7u;
            var offset = ((long)fieldE << 3) | (long)offsetLow3;

            entry = new Entry<ulong>()
            {
                NameHash = hash,
                UncompressedSize = (int)fieldB,
                CompressedSize = compressedSize,
                Offset = offset,
                CompressionScheme = 3, // XMemCompress — archive-level, not per-entry
                Name = null,
                DataHash = 0,
            };
        }
    }
}
