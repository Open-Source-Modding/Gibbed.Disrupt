using System;
using System.Globalization;
using System.Xml;
using System.Xml.XPath;
using Gibbed.Disrupt.BinaryObjectInfo.Definitions;

namespace Gibbed.Disrupt.BinaryObjectInfo.FieldHandlers
{
    internal class VectorHandler : IFieldHandler
    {
        public byte[] Import(FieldDefinition def, FieldType arrayFieldType, XPathNavigator nav)
        {
            return ImportFromString(def, nav.Value);
        }

        public byte[] Import(FieldDefinition def, FieldType arrayFieldType, string text)
        {
            return ImportFromString(def, text);
        }

        private static byte[] ImportFromString(FieldDefinition def, string text)
        {
            var parts = text.Split(',');
            var componentCount = parts.Length;

            // Determine byte count from component count
            int byteCount = componentCount * 4;

            var data = new byte[byteCount];
            for (int i = 0; i < componentCount; i++)
            {
                if (Helpers.TryParseFloat32(parts[i], out var value) == false)
                {
                    throw new FormatException($"failed to parse Vector component {i}: '{parts[i]}'");
                }
                Array.Copy(BitConverter.GetBytes(value), 0, data, i * 4, 4);
            }

            return data;
        }

        public void Export(
            FieldDefinition def,
            FieldType arrayFieldType,
            byte[] buffer,
            int offset,
            int count,
            XmlWriter writer,
            out int read)
        {
            if (count % 4 != 0)
            {
                throw new FormatException($"Vector data size ({count} bytes) is not a multiple of 4");
            }

            var componentCount = count / 4;

            if (Helpers.HasLeft(buffer, offset, count, count) == false)
            {
                throw new System.IO.EndOfStreamException($"Vector requires {count} bytes");
            }

            read = count;

            var components = new string[componentCount];
            for (int i = 0; i < componentCount; i++)
            {
                components[i] = BitConverter.ToSingle(buffer, offset + i * 4)
                    .ToString("G", CultureInfo.InvariantCulture);
            }

            writer.WriteString(string.Join(",", components));
        }
    }
}
