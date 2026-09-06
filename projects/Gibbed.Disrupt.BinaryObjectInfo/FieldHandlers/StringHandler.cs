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
using System.Text;
using Gibbed.Disrupt.BinaryObjectInfo.Definitions;

namespace Gibbed.Disrupt.BinaryObjectInfo.FieldHandlers
{
    internal class StringHandler : ValueHandler<string>
    {
        public override byte[] Serialize(string value)
        {
            var data = Encoding.UTF8.GetBytes(value);
            Array.Resize(ref data, data.Length + 1);
            return data;
        }

        public override string Parse(FieldDefinition def, string text)
        {
            return text;
        }

        public override string Deserialize(byte[] buffer, int offset, int count, out int read)
        {
            if (Helpers.HasLeft(buffer, offset, count, 1) == false)
            {
                throw new FormatException("String requires at least 1 byte");
            }

            // Find the null terminator within field bounds
            int length;
            int nullPos = -1;
            for (length = 0; length < count; length++)
            {
                if (buffer[offset + length] == 0)
                {
                    nullPos = offset + length;
                    break;
                }
            }

            // Always consume exactly 'count' bytes so FieldHandling.Export's
            // read==count check passes, even if the field has trailing null
            // padding beyond the string's own terminator.
            read = count;

            if (nullPos >= 0)
            {
                return Encoding.UTF8.GetString(buffer, offset, length);
            }

            // No null terminator — truncated/corrupt data. Use all bytes.
            System.Diagnostics.Debug.WriteLine(
                $"WARNING: String at offset 0x{offset:X} missing null terminator within {count} bytes, using raw data");
            return Encoding.UTF8.GetString(buffer, offset, count);
        }

        public override string Compose(FieldDefinition def, string value)
        {
            return value;
        }
    }
}
