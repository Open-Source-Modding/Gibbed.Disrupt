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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using Gibbed.IO;
using BigDependency = Gibbed.Disrupt.FileFormats.Big.Dependency<ulong>;
using BigEntry = Gibbed.Disrupt.FileFormats.Big.Entry<ulong>;

namespace Gibbed.Disrupt.FileFormats
{
    // Watch Dogs: Legion (WDL) big file format — version 13.
    // Split from the unified BigFileV5 (which previously bundled V11/WD2 + V13/WDL).
    // This class is V13-only.
    public class BigFileV13 : Big.IDependentArchive<ulong>
    {
        public const uint Signature = 0x46415435; // 'FAT5'

        #region Fields
        private Endian _Endian;
        private int _Version;
        private Big.Platform _Platform;
        private byte _CompressionVersion;
        private byte _NameHashVersion;
        private ulong _ArchiveHash;
        private readonly List<BigDependency> _Dependencies;
        private readonly List<BigEntry> _Entries;
        #endregion

        public BigFileV13()
        {
            this._Endian = Endian.Little;
            this._Dependencies = new List<BigDependency>();
            this._Entries = new List<BigEntry>();
        }

        #region Properties
        public Endian Endian
        {
            get { return this._Endian; }
            set { this._Endian = value; }
        }

        public int Version
        {
            get { return this._Version; }
            set { this._Version = value; }
        }

        public Big.Platform Platform
        {
            get { return this._Platform; }
            set { this._Platform = value; }
        }

        public byte CompressionVersion
        {
            get { return this._CompressionVersion; }
            set { this._CompressionVersion = value; }
        }

        public byte NameHashVersion
        {
            get { return this._NameHashVersion; }
            set { this._NameHashVersion = value; }
        }

        public ulong ArchiveHash
        {
            get { return this._ArchiveHash; }
            set { this._ArchiveHash = value; }
        }

        public bool HasArchiveHash
        {
            get { return this.ArchiveHash != ulong.MaxValue; }
        }

        public List<BigDependency> Dependencies
        {
            get { return this._Dependencies; }
        }

        public List<BigEntry> Entries
        {
            get { return this._Entries; }
        }
        #endregion

        // Serialize: write the v13 .fat (FAT5 magic, version 13, flags, archive hash,
        // dependency count + deps, entry count + 20-byte entries).
        // Mirrors Deserialize so pack/unpack round-trip byte-compatibly.
        public void Serialize(Stream output)
        {
            output.WriteValueU32(Signature, Endian.Little);

            var flags = (uint)FromPlatform(this._Platform);
            flags |= (uint)this._CompressionVersion << 8;
            flags |= (uint)this._NameHashVersion << 16;

            output.WriteValueS32(13, Endian.Little);
            output.WriteValueU32(flags, Endian.Little);
            output.WriteValueU64(this._ArchiveHash, Endian.Little);

            output.WriteValueU32((uint)this._Dependencies.Count, Endian.Little);
            foreach (var dependency in this._Dependencies)
            {
                output.WriteValueU64(dependency.ArchiveHash, Endian.Little);
                output.WriteValueU64(dependency.NameHash, Endian.Little);
            }

            output.WriteValueU32((uint)this._Entries.Count, Endian.Little);
            var entrySerializer = new Big.EntrySerializerV13();
            foreach (var entry in this._Entries)
            {
                entrySerializer.Serialize(output, entry, Endian.Little);
            }
        }

        public void SerializeNfo(Stream output)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "\t",
                OmitXmlDeclaration = true
            };

            using (var writer = XmlWriter.Create(output, settings))
            {
                writer.WriteStartElement("Root");
                writer.WriteStartElement("common");

                foreach (var entry in this.Entries)
                {
                    writer.WriteStartElement("File");

                    writer.WriteAttributeString("Path", entry.Name ?? "");

                    writer.WriteAttributeString("Crc", entry.NameHash.ToString(CultureInfo.InvariantCulture));

                    writer.WriteAttributeString("FilePosition", entry.Offset.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("FileSize", entry.CompressedSize.ToString(CultureInfo.InvariantCulture));

                    writer.WriteAttributeString("FileTime", entry.DataHash.ToString(CultureInfo.InvariantCulture));

                    writer.WriteEndElement();
                }

                writer.WriteEndElement(); // common
                writer.WriteEndElement(); // Root
            }
        }

        public void Deserialize(Stream input)
        {
            var magic = input.ReadValueU32(Endian.Little);
            if (magic != Signature && magic.Swap() != Signature)
            {
                throw new FormatException("bad magic");
            }
            var endian = magic == Signature ? Endian.Little : Endian.Big;

            var version = input.ReadValueS32(endian);
            if (version != 13)
            {
                throw new FormatException(string.Format("unsupported version {0} (expected 13)", version));
            }

            var flags = input.ReadValueU32(endian);
            var platform = ToPlatform((byte)(flags & 0xFF));
            var compressionVersion = (byte)((flags >> 8) & 0xFF);
            var nameHashVersion = (byte)((flags >> 16) & 0xFF);

            var archiveHash = input.ReadValueU64(endian);

            var dependencyCount = input.ReadValueU32(endian);
            var dependencies = new List<BigDependency>();
            for (uint i = 0; i < dependencyCount; i++)
            {
                // version 13: 16 bytes (8+8)
                var dependencyArchiveHash = input.ReadValueU64(endian);
                var dependencyNameHash = input.ReadValueU64(endian);
                dependencies.Add(new BigDependency(dependencyArchiveHash, dependencyNameHash));
            }

            var entryCount = input.ReadValueU32(endian);
            var entrySerializer = new Big.EntrySerializerV13();
            var entries = new List<BigEntry>();
            for (uint i = 0; i < entryCount; i++)
            {
                entrySerializer.Deserialize(input, endian, out var entry);
                entries.Add(entry);
            }

            // Duplicate and localization sections are v13 metadata that we don't need
            // for unpacking/rebuilding file lists. Wrap in try-catch so archives with
            // unrecognized sub-formats still get their entries parsed successfully.
            try
            {
                var duplicateCount = input.ReadValueU32(endian);
                if (duplicateCount <= 1000000)
                {
                    // 20 bytes per duplicate entry: u64 hash + u32 offset + u32 size + u32 flags
                    input.Seek(duplicateCount * 20, SeekOrigin.Current);

                    // Optional localization section (v12 style: count + count * (nameLen + name + 8))
                    long posAfterDuplicates = input.Position;
                    long remaining = input.Length - posAfterDuplicates;
                    if (remaining >= 4)
                    {
                        var localizationCount = input.ReadValueU32(endian);
                        if (localizationCount <= 1000000 && localizationCount > 0)
                        {
                            bool valid = true;
                            long posAfterLocCount = input.Position;
                            for (uint i = 0; i < localizationCount; i++)
                            {
                                if (input.Length - input.Position < 4)
                                {
                                    valid = false;
                                    break;
                                }
                                var nameLength = input.ReadValueU32(endian);
                                if (nameLength > 256)
                                {
                                    valid = false;
                                    break;
                                }
                                if (input.Length - input.Position < nameLength + 8)
                                {
                                    valid = false;
                                    break;
                                }
                                input.Seek(nameLength, SeekOrigin.Current);
                                input.Seek(8, SeekOrigin.Current); // unknown u64
                            }
                            if (valid == false)
                            {
                                input.Position = posAfterLocCount;
                                input.Position = input.Length;
                            }
                        }
                        else if (localizationCount != 0)
                        {
                            input.Position = posAfterDuplicates;
                            input.Position = input.Length;
                        }
                    }
                }
                else
                {
                    input.Position = input.Length;
                }
            }
            catch
            {
                input.Position = input.Length;
            }

            this._Endian = endian;
            this._Version = version;
            this._Platform = platform;
            this._CompressionVersion = compressionVersion;
            this._NameHashVersion = nameHashVersion;
            this._ArchiveHash = archiveHash;
            this._Dependencies.Clear();
            this._Dependencies.AddRange(dependencies);
            this._Entries.Clear();
            this._Entries.AddRange(entries);
        }

        public static Big.Platform ToPlatform(byte id)
        {
            switch (id)
            {
                case 0: return Big.Platform.Any;
                case 1: return Big.Platform.Win64;
                case 3: return Big.Platform.Orbis;
            }
            throw new NotSupportedException("unknown platform");
        }

        public static byte FromPlatform(Big.Platform platform)
        {
            switch (platform)
            {
                case Big.Platform.Any: return 0;
                case Big.Platform.Win64: return 1;
                case Big.Platform.Orbis: return 3;
            }
            throw new NotSupportedException("unknown platform");
        }

        public static ulong ComputeNameHash(string s, Big.TryGetHashOverride<ulong> tryGetOverride)
        {
            if (tryGetOverride != null)
            {
                throw new InvalidOperationException();
            }
            if (s == null || s.Length == 0)
            {
                return ulong.MaxValue;
            }
            var hash = Hashing.FNV1a64.Compute(s.ToLowerInvariant());
            if (hash == ulong.MaxValue)
            {
                return ulong.MaxValue;
            }
            hash &= 0x1FFFFFFFFFFFFFFFul;
            hash |= 0xA000000000000000ul;
            return hash;
        }

        public static bool TryParseNameHash(string s, out ulong value)
        {
            return ulong.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
        }

        public static string RenderNameHash(ulong value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:X16}", value);
        }

        ulong Big.IArchive<ulong>.ComputeNameHash(string s, Big.TryGetHashOverride<ulong> tryGetOverride)
        {
            return ComputeNameHash(s, tryGetOverride);
        }

        bool Big.IArchive<ulong>.TryParseNameHash(string s, out ulong value)
        {
            return TryParseNameHash(s, out value);
        }

        string Big.IArchive<ulong>.RenderNameHash(ulong value)
        {
            return RenderNameHash(value);
        }

        public Big.CompressionScheme ToCompressionScheme(byte id, int uncompressedSize)
        {
            return Big.CompressionSchemeV8.ToCompressionScheme(id);
        }

        public byte FromCompressionSCheme(Big.CompressionScheme compressionScheme)
        {
            return Big.CompressionSchemeV8.FromCompressionScheme(compressionScheme);
        }
    }
}