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
using Gibbed.Disrupt.FileFormats;
using Gibbed.IO;

namespace Gibbed.WatchDogs.Unpack
{
    internal class Program
    {
        /// <summary>
        /// Resolve any supported input extension (.dat, .nfo, .dup, .fat)
        /// to the corresponding .fat path.
        /// Mirrors the extension resolution logic in Unpack.cs lines 112-134.
        /// </summary>
        private static string ResolveFatPath(string inputPath)
        {
            string ext = Path.GetExtension(inputPath);

            if (ext == ".dat")
            {
                return Path.ChangeExtension(inputPath, ".fat");
            }
            else if (ext == ".nfo")
            {
                return Path.ChangeExtension(inputPath, ".fat");
            }
            else if (ext == ".dup")
            {
                // .dup → .nfo → .fat
                return Path.ChangeExtension(
                    Path.ChangeExtension(inputPath, ".nfo"),
                    ".fat");
            }
            else
            {
                // .fat or any other extension → assume .fat
                return Path.ChangeExtension(inputPath, ".fat");
            }
        }

        /// <summary>
        /// Read the first 4 bytes of a .fat file and determine the format.
        /// Returns true if FAT2 (X360 / BigFileV2), false if FAT3 (retail / BigFileV3).
        /// Throws if the magic is unrecognized.
        /// </summary>
        private static bool DetectFatFormat(string fatPath)
        {
            using (var input = File.OpenRead(fatPath))
            {
                var magic = input.ReadValueU32(Endian.Little);

                if (magic == BigFileV2.Signature)
                {
                    return true; // FAT2 — X360
                }
                else if (magic == BigFileV3.Signature)
                {
                    return false; // FAT3 — retail PC/PS3
                }
                else
                {
                    throw new InvalidDataException(
                        string.Format(
                            "Unrecognized FAT magic: 0x{0:X8} in '{1}'. " +
                            "Expected FAT2 (0x{2:X8}) or FAT3 (0x{3:X8}).",
                            magic,
                            fatPath,
                            BigFileV2.Signature,
                            BigFileV3.Signature));
                }
            }
        }

        /// <summary>
        /// Find the first positional argument (non-option) in args.
        /// Options start with '-'; positional args are file paths.
        /// Returns null if no positional arg is found.
        /// </summary>
        private static string FindInputPath(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("-") == false)
                {
                    return arg;
                }
            }
            return null;
        }

        public static void Main(string[] args)
        {
            string inputPath = FindInputPath(args);

            if (inputPath == null)
            {
                // No positional arg — likely --help or no args.
                // Forward to BigFileV3 (default retail); it will print usage and exit.
                Disrupt.Packing.Unpack<BigFileV3, uint>.Main(
                    args,
                    "Watch Dogs",
                    Packing.HashOverrides.TryGet);
                return;
            }

            string fatPath = ResolveFatPath(inputPath);

            if (File.Exists(fatPath) == false)
            {
                System.Console.Error.WriteLine(
                    "Error: Could not locate FAT file '{0}'.", fatPath);
                return;
            }

            bool isX360 = DetectFatFormat(fatPath);

            if (isX360)
            {
                // FAT2 — X360: ulong CRC64 hashes, no hash overrides
                Disrupt.Packing.Unpack<BigFileV2, ulong>.Main(
                    args,
                    "Watch Dogs",
                    null);
            }
            else
            {
                // FAT3 — retail: uint FNV1a hashes, with hash overrides
                Disrupt.Packing.Unpack<BigFileV3, uint>.Main(
                    args,
                    "Watch Dogs",
                    Packing.HashOverrides.TryGet);
            }
        }
    }
}
