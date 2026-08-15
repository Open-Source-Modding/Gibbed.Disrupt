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
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace Gibbed.Disrupt.Packing
{
    internal static class EntryDecompression
    {
        public static void Decompress<T>(IArchive<T> archive, IEntry entry, Stream input, Stream output)
        {
            input.Seek(entry.Offset, SeekOrigin.Begin);

            var compressionScheme = archive.ToCompressionScheme(entry.CompressionScheme, entry.UncompressedSize);
            if (compressionScheme == CompressionScheme.None)
            {
                output.WriteFromStream(input, entry.CompressedSize);
            }
            else if (compressionScheme == CompressionScheme.LZO1x)
            {
                throw new PlatformNotSupportedException("LZO decompression is not supported on this platform.");
            }
            else if (compressionScheme == CompressionScheme.Zlib)
            {
                DecompressZlib(entry, input, output);
            }
            else if (compressionScheme == CompressionScheme.XMemCompress)
            {
                DecompressXMemCompress(entry, input, output);
            }
            else if (compressionScheme == CompressionScheme.LZ4LW)
            {
                try
                {
                    DecompressLZ4LW(entry, input, output);
                }
                catch (Exception)
                {
                    input.Seek(entry.Offset, SeekOrigin.Begin);
                    output.WriteFromStream(input, entry.CompressedSize);
                }
            }
            else if (compressionScheme == CompressionScheme.LZMA)
            {
                try
                {
                    DecompressLZMA(entry, input, output);
                }
                catch (Exception)
                {
                    input.Seek(entry.Offset, SeekOrigin.Begin);
                    output.WriteFromStream(input, entry.CompressedSize);
                }
            }
            else
            {
                throw new NotImplementedException("unimplemented compression scheme");
            }
        }

        private static void DecompressZlib(IEntry entry, Stream input, Stream output)
        {
            if (entry.CompressedSize < 16)
            {
                throw new EndOfStreamException("not enough data for zlib compressed data");
            }

            var sizes = new ushort[8];
            for (int i = 0; i < 8; i++)
            {
                sizes[i] = input.ReadValueU16(Endian.Little);
            }

            var blockCount = sizes[0];
            var maximumUncompressedBlockSize = 16 * (sizes[1] + 1);

            long left = entry.UncompressedSize;
            for (int i = 0, c = 2; i < blockCount; i++, c++)
            {
                if (c == 8)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        sizes[j] = input.ReadValueU16(Endian.Little);
                    }

                    c = 0;
                }

                uint compressedBlockSize = sizes[c];
                if (compressedBlockSize != 0)
                {
                    var uncompressedBlockSize = i + 1 < blockCount
                        ? Math.Min(maximumUncompressedBlockSize, left)
                        : left;
                    //var uncompressedBlockSize = Math.Min(maximumUncompressedBlockSize, left);

                    using (var temp = input.ReadToMemoryStream((int)compressedBlockSize))
                    {
                        var zlib = new InflaterInputStream(temp, new Inflater(true));
                        output.WriteFromStream(zlib, uncompressedBlockSize);
                        left -= uncompressedBlockSize;
                    }

                    var padding = (16 - (compressedBlockSize % 16)) % 16;
                    if (padding > 0)
                    {
                        input.Seek(padding, SeekOrigin.Current);
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            if (left > 0)
            {
                throw new InvalidOperationException("did not decompress enough data");
            }
        }

        private static void DecompressXMemCompress(IEntry entry, Stream input, Stream output)
        {
            var magic = input.ReadValueU32(Endian.Big);
            if (magic != 0x0FF512EE)
            {
                throw new FormatException("invalid magic");
            }

            var version = input.ReadValueU32(Endian.Big);
            if (version != 0x01030000)
            {
                throw new FormatException("invalid version");
            }

            var unknown08 = input.ReadValueU32(Endian.Big);
            if (unknown08 != 0)
            {
                throw new FormatException("don't know how to handle a non-zero unknown08");
            }

            var unknown0C = input.ReadValueU32(Endian.Big);
            if (unknown0C != 0)
            {
                throw new FormatException("don't know how to handle a non-zero unknown0C");
            }

            var windowSize = input.ReadValueU32(Endian.Big);
            var chunkSize = input.ReadValueU32(Endian.Big);

            var uncompressedSize = input.ReadValueS64(Endian.Big);
            var compressedSize = input.ReadValueS64(Endian.Big);
            var largestUncompressedChunkSize = input.ReadValueS32(Endian.Big);
            var largestCompressedChunkSize = input.ReadValueS32(Endian.Big);

            if (uncompressedSize < 0 ||
                compressedSize < 0 ||
                largestUncompressedChunkSize < 0 ||
                largestCompressedChunkSize < 0)
            {
                throw new FormatException("bad size value");
            }

            if (uncompressedSize != entry.UncompressedSize)
            {
                throw new InvalidOperationException("uncompressed size mismatch");
            }

            var uncompressedBytes = new byte[largestUncompressedChunkSize];
            var compressedBytes = new byte[largestCompressedChunkSize];

            var remaining = uncompressedSize;
            while (remaining > 0)
            {
                var compressedChunkSize = input.ReadValueS32(Endian.Big);
                if (compressedChunkSize < 0 ||
                    compressedChunkSize > largestCompressedChunkSize)
                {
                    throw new InvalidOperationException("compressed size mismatch");
                }

                if (input.Read(compressedBytes, 0, compressedChunkSize) != compressedChunkSize)
                {
                    throw new EndOfStreamException("could not read all compressed bytes");
                }

                var uncompressedChunkSize = (int)Math.Min(largestUncompressedChunkSize, remaining);
                var actualUncompressedChunkSize = uncompressedChunkSize;
                var actualCompressedChunkSize = compressedChunkSize;

                bool ok = false;

                try
                {
                    using (var context = new XCompression.DecompressionContext(windowSize, chunkSize))
                    {
                        var result = context.Decompress(
                            compressedBytes,
                            0,
                            ref actualCompressedChunkSize,
                            uncompressedBytes,
                            0,
                            ref actualUncompressedChunkSize);
                        if (result == XCompression.ErrorCode.None)
                        {
                            ok = true;
                        }
                    }
                }
                catch
                {
                }

                if (ok == false)
                {
                    using (var context = new XCompression.ManagedDecompressionContext(windowSize, chunkSize))
                    {
                        var result = context.Decompress(
                            compressedBytes,
                            0,
                            ref actualCompressedChunkSize,
                            uncompressedBytes,
                            0,
                            ref actualUncompressedChunkSize);
                        if (result != XCompression.ErrorCode.None)
                        {
                            return;
                        }
                    }
                }

                if (actualUncompressedChunkSize != uncompressedChunkSize)
                {
                    throw new InvalidOperationException("XCompression decompression failure (uncompressed size mismatch)");
                }

                output.Write(uncompressedBytes, 0, actualUncompressedChunkSize);

                remaining -= actualUncompressedChunkSize;
            }
        }

        private static void DecompressLZ4LW(IEntry entry, Stream input, Stream output)
        {
            int header = ReadPackedS32(input, out var headerSize);
            var buffer = new byte[entry.UncompressedSize];
            int inputStart = entry.UncompressedSize - entry.CompressedSize + headerSize;
            if (input.Read(buffer, inputStart, entry.CompressedSize - headerSize) != entry.CompressedSize - headerSize)
            {
                throw new EndOfStreamException("could not read all compressed bytes");
            }
            DecompressLZ4LWInPlace(buffer, inputStart, entry.UncompressedSize - header);
            output.Write(buffer, 0, entry.UncompressedSize);
        }

        private static void DecompressLZ4LWInPlace(byte[] buffer, int inputStartPosition, int safeDecodingOffset)
        {
            int inputPos = inputStartPosition;
            int outputPos = 0;
            while (outputPos < safeDecodingOffset || outputPos < inputPos)
            {
                byte token = buffer[inputPos++];
                int literalLength = token >> 4;
                if (literalLength == 15)
                {
                    byte value;
                    do
                    {
                        value = buffer[inputPos++];
                        literalLength += value;
                    }
                    while (value == byte.MaxValue);
                }
                if (literalLength > 0)
                {
                    Buffer.BlockCopy(buffer, inputPos, buffer, outputPos, literalLength);
                    inputPos += literalLength;
                    outputPos += literalLength;
                }
                byte offsetLo = buffer[inputPos++];
                byte offsetHi = buffer[inputPos++];
                int offset = offsetLo | (offsetHi << 8);
                if (offset >= 0xE000)
                {
                    int offsetEx = buffer[inputPos++];
                    offset += offsetEx << 13;
                }
                int matchLength = token & 0xF;
                if (matchLength == 15)
                {
                    byte value;
                    do
                    {
                        value = buffer[inputPos++];
                        matchLength += value;
                    }
                    while (value == byte.MaxValue);
                }
                matchLength += 4;
                for (int i = 0; i < matchLength; i++)
                {
                    buffer[outputPos] = buffer[outputPos - offset];
                    outputPos++;
                }
            }
        }

        private static int ReadPackedS32(Stream input, out int read)
        {
            read = 1;
            byte value = input.ReadValueU8();
            int result = value & 0x7F;
            int shift = 7;
            while ((value & 0x80) != 0)
            {
                if (shift > 21)
                {
                    throw new InvalidOperationException();
                }
                read++;
                value = input.ReadValueU8();
                result |= (value & 0x7F) << shift;
                shift += 7;
            }
            return result;
        }

        private static void DecompressLZMA(IEntry entry, Stream input, Stream output)
        {
            output.Seek(0, SeekOrigin.Begin);
            output.SetLength(0);

            // PS4 (Orbis) LZMA entries carry a one-byte leading flag before the
            // standard LZMA header (properties + dictionary size).
            if (input.ReadByte() < 0)
            {
                throw new EndOfStreamException("could not read LZMA leading byte");
            }

            var decoder = new SevenZip.Compression.LZMA.Decoder();
            var properties = new byte[5];
            if (input.Read(properties, 0, 5) != 5)
            {
                throw new EndOfStreamException("could not read LZMA properties");
            }

            decoder.SetDecoderProperties(properties);
            decoder.Code(input, output, entry.CompressedSize - 6, entry.UncompressedSize, null);
        }
    }
}
