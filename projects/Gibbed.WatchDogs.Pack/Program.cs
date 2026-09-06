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
 * 2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System.Collections.Generic;
using Gibbed.Disrupt.FileFormats;

namespace Gibbed.WatchDogs.Pack
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var x360 = false;
            var remaining = new List<string>();

            foreach (var arg in args)
            {
                if (arg == "--x360" || arg == "-x")
                {
                    x360 = true;
                }
                else
                {
                    remaining.Add(arg);
                }
            }

            if (x360)
            {
                // X360 FAT2: BigFileV2 with ulong CRC64 hashes.
                // Inject correct package parameters if not already specified.
                var finalArgs = new List<string>(remaining);

                bool hasVersion = false, hasTarget = false, hasNhv = false;
                foreach (var a in finalArgs)
                {
                    if (a.StartsWith("--package-version") || a.StartsWith("-pv")) hasVersion = true;
                    if (a.StartsWith("--package-target") || a.StartsWith("-pt")) hasTarget = true;
                    if (a.StartsWith("--name-hash-version") || a.StartsWith("-nhv")) hasNhv = true;
                }

                if (!hasNhv) { finalArgs.Insert(0, "--nhv"); finalArgs.Insert(1, "0"); }
                if (!hasTarget) { finalArgs.Insert(0, "--pt"); finalArgs.Insert(1, "Xenon"); }
                if (!hasVersion) { finalArgs.Insert(0, "--pv"); finalArgs.Insert(1, "8"); }

                Disrupt.Packing.Pack<BigFileV2, ulong>.Main(
                    finalArgs.ToArray(),
                    null); // No hash overrides for X360
            }
            else
            {
                // Default: FAT3 retail
                Disrupt.Packing.Pack<BigFileV3, uint>.Main(
                    remaining.ToArray(),
                    Packing.HashOverrides.TryGet);
            }
        }
    }
}
