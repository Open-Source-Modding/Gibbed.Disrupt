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
                    entry.CompressionScheme = 0 /* CompressionScheme.None */;
                    entry.UncompressedSize = 0;
                    entry.CompressedSize = (int)input.Length;
                    output.WriteFromStream(input, input.Length);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }
    }
}
