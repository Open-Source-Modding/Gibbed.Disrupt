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

using System;
using System.IO;
using Gibbed.Disrupt.FileFormats.Big;
using Gibbed.IO;

namespace Gibbed.Disrupt.Packing
{
    public static partial class Pack<TArchive, THash>
    {
        internal static class EntryCompression
        {
            public static void Compress(
                Platform platform,
                ref Entry<THash> entry,
                Stream input,
                bool compress,
                Stream output)
            {
                if (input.Length == 0)
                {
                    entry.CompressionScheme = 0 /* CompressionScheme.None */;
                    entry.UncompressedSize = 0;
                    entry.CompressedSize = 0;
                }
                else if (compress == false)
                {
                    // Stored (uncompressed): for v13 (WDL) the engine expects
                    // UncompressedSize == CompressedSize == file length for scheme 0.
                    // (The reference EncryptedsPatch.fat has us == cs for every stored entry.)
                    entry.CompressionScheme = 0 /* CompressionScheme.None */;
                    entry.UncompressedSize = (int)input.Length;
                    entry.CompressedSize = (int)input.Length;
                    output.WriteFromStream(input, input.Length);
                }
                else
                {
                    // LZ4LW (WDL v13 scheme 3): the dominant compression in real WDL
                    // archives (common.fat, shadersobj.fat, patch.fat are mostly LZ4LW).
                    // On-disk layout (matches DecompressLZ4LW / DecompressLZ4LWInPlace):
                    //   [header packed s32 = tail count] [LZ4 block] [tail raw bytes]
                    // The decoder allocates a us-byte buffer, reads the compressed bytes
                    // at inputStart = us - cs + headerSize (filling up to 'us'), decodes
                    // the LZ4 block in-place up to safeDecodingOffset = us - header, and
                    // the last 'header' bytes are the raw tail already in the buffer.
                    var data = new byte[input.Length];
                    input.Read(data, 0, data.Length);
                    LZ4CompressBlock(data, out var block, out int tailCount);
                    // Write header varint (tailCount; 7-bit packed)
                    int tailBytes = tailCount;
                    byte[] hdr;
                    if (tailBytes < 0x80) hdr = new byte[] { (byte)tailBytes };
                    else
                    {
                        var hdrList = new System.Collections.Generic.List<byte>();
                        int v = tailBytes;
                        while (v >= 0x80) { hdrList.Add((byte)((v & 0x7F) | 0x80)); v >>= 7; }
                        hdrList.Add((byte)v);
                        hdr = hdrList.ToArray();
                    }
                    int total = hdr.Length + block.Length + tailBytes;
                    if (total < data.Length)
                    {
                        output.Write(hdr, 0, hdr.Length);
                        output.Write(block, 0, block.Length);
                        // tail = raw bytes at [coveredLen .. len)
                        int coveredLen = data.Length - tailBytes;
                        output.Write(data, coveredLen, tailBytes);
                        entry.CompressionScheme = 3; // LZ4LW
                        entry.UncompressedSize = data.Length;
                        entry.CompressedSize = total;
                    }
                    else
                    {
                        // incompressible -> store uncompressed (scheme 0)
                        entry.CompressionScheme = 0;
                        entry.UncompressedSize = data.Length;
                        entry.CompressedSize = data.Length;
                        output.Write(data, 0, data.Length);
                    }
                }
            }

            // Standard LZ4 block compressor (match offset capped < 0xE000 so the
            // LZ4LW offset-extension byte is never emitted). The LZ4LW decoder's
            // final sequence must END with a match (it always reads offset after
            // literals), so any trailing bytes that don't form a full match are
            // left as the uncompressed 'header' tail (safeDecodingOffset = us - header).
            // Returns (blockBytes, headerTailCount).
            // Standard LZ4 block compressor for WDL LZ4LW (scheme 3). Match offsets are
            // capped < 0xE000 (no offset-extension byte). The LZ4LW decoder's loop
            // always reads a match after literals, so the LAST sequence must end in a
            // match; trailing bytes that can't form a full match are left as the
            // uncompressed 'header' tail (safeDecodingOffset = us - header).
            private static void LZ4CompressBlock(byte[] input, out byte[] block, out int tailCount)
            {
                int len = input.Length;
                const int hashLog = 16;
                var hashTable = new int[1 << hashLog];
                for (int i = 0; i < hashTable.Length; i++) hashTable[i] = -1;

                var seq = new System.Collections.Generic.List<(int litStart, int litLen, int matchEnd, int matchOffset)>();
                int ip = 0;
                int anchor = 0;
                int matchLimit = len - 12;
                int lastMatchEnd = -1;

                uint Hash(int p)
                {
                    uint v = (uint)(input[p] | (input[p + 1] << 8) | (input[p + 2] << 16) | (input[p + 3] << 24));
                    return (v * 2654435761u) >> (32 - hashLog);
                }
                int MatchLen(int s1, int s2, int maxLen)
                {
                    int n = 0;
                    while (n < maxLen && input[s1 + n] == input[s2 + n]) n++;
                    return n;
                }

                while (ip <= matchLimit)
                {
                    int h = (int)Hash(ip);
                    int candidate = hashTable[h];
                    hashTable[h] = ip;
                    if (candidate >= 0 && ip - candidate < 0xE000 &&
                        input[candidate] == input[ip] && input[candidate + 1] == input[ip + 1] &&
                        input[candidate + 2] == input[ip + 2] && input[candidate + 3] == input[ip + 3])
                    {
                        int ml = MatchLen(candidate, ip, len - ip);
                        if (ml >= 4)
                        {
                            seq.Add((anchor, ip - anchor, ip + ml, ip - candidate));
                            ip += ml;
                            anchor = ip;
                            lastMatchEnd = ip;
                            continue;
                        }
                    }
                    ip++;
                }

                // 'coveredLen' = bytes the LZ4 block will decode = last match end.
                // Trailing literals past lastMatchEnd go to the tail.
                int coveredLen = lastMatchEnd < 0 ? 0 : lastMatchEnd;
                int tail = len - coveredLen;

                var output = new System.Collections.Generic.List<byte>(coveredLen / 2 + 16);
                foreach (var (litStart, litLen, matchEnd, matchOffset) in seq)
                {
                    if (litStart >= coveredLen) break; // past covered region -> tail
                    int ll = litLen;
                    // match length = (matchEnd - litStart) - litLen; stored = that - 4
                    int matchLen = (matchEnd - litStart) - litLen;
                    int sm = matchLen - 4;
                    int lln = ll > 15 ? 15 : ll;
                    int mln = sm > 15 ? 15 : sm;
                    output.Add((byte)((lln << 4) | mln));
                    if (ll >= 15) { int r = ll - 15; while (r >= 255) { output.Add(255); r -= 255; } output.Add((byte)r); }
                    for (int k = 0; k < ll; k++) output.Add(input[litStart + k]);
                    // LZ4LW decoder reads OFFSET then match-length-ext: emit offset first.
                    output.Add((byte)(matchOffset & 0xFF));
                    output.Add((byte)((matchOffset >> 8) & 0xFF));
                    if (sm >= 15) { int r = sm - 15; while (r >= 255) { output.Add(255); r -= 255; } output.Add((byte)r); }
                }
                block = output.ToArray();
                tailCount = tail;
            }
        }
    }
}
