using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Gibbed.Disrupt.FileFormats.Hashing;

namespace Gibbed.Disrupt.BinaryObjectInfo
{
    public static class StringLookup
    {
        private static readonly Dictionary<uint, string> _crc32 = new Dictionary<uint, string>();
        private static readonly Dictionary<uint, string> _fnv32 = new Dictionary<uint, string>();

        public static void Load(string projectPath)
        {
            var files = new[]
            {
                Path.Combine(projectPath, "strings.txt"),
                Path.Combine(projectPath, "strings.user.txt"),
            };

            foreach (var file in files)
            {
                if (File.Exists(file))
                {
                    LoadFile(file);
                }
            }
        }

        private static void LoadFile(string path)
        {
            var encoding = Encoding.GetEncoding("iso-8859-1");
            var lines = File.ReadAllLines(path, encoding);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length == 0)
                    continue;
                if (i == 0 && line[0] == '#')
                    continue;

                // CRC32 for FCB field names
                var crc = CRC32.Compute(line);
                if (!_crc32.ContainsKey(crc))
                {
                    _crc32[crc] = line;
                }

                // FNV1a 32-bit for WD1 file path refs
                var fnv = FNV1a32.Compute(line);
                if (!_fnv32.ContainsKey(fnv))
                {
                    _fnv32[fnv] = line;
                }
            }
        }

        public static bool TryResolve(uint hash, out string result)
        {
            // Try CRC32 first (most common for field names)
            if (_crc32.TryGetValue(hash, out result))
                return true;

            // Try FNV32 (WD1 file paths)
            if (_fnv32.TryGetValue(hash, out result))
                return true;

            result = null;
            return false;
        }

        public static string Resolve(uint hash)
        {
            if (TryResolve(hash, out var result))
                return result;
            return null;
        }
    }
}
