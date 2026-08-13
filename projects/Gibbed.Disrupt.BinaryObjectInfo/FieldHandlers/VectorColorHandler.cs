using System;
using System.Globalization;
using System.Xml;
using System.Xml.XPath;
using Gibbed.Disrupt.BinaryObjectInfo.Definitions;

namespace Gibbed.Disrupt.BinaryObjectInfo.FieldHandlers
{
    internal class VectorColorHandler : IFieldHandler, IValueHandler
    {
        public byte[] Import(FieldDefinition def, FieldType arrayFieldType, XPathNavigator nav)
        {
            return ImportFromString(nav.Value);
        }

        public byte[] Import(FieldDefinition def, FieldType arrayFieldType, string text)
        {
            return ImportFromString(text);
        }

        private static byte[] ImportFromString(string text)
        {
            text = text.TrimStart('#');

            if (uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) == false)
            {
                throw new FormatException($"VectorColor requires a hex color code like #RRGGBBAA, got '{text}'");
            }

            var r = ((hex >> 24) & 0xFF) / 255.0f;
            var g = ((hex >> 16) & 0xFF) / 255.0f;
            var b = ((hex >> 8) & 0xFF) / 255.0f;
            var a = (hex & 0xFF) / 255.0f;

            var data = new byte[16];
            Array.Copy(BitConverter.GetBytes(r), 0, data, 0, 4);
            Array.Copy(BitConverter.GetBytes(g), 0, data, 4, 4);
            Array.Copy(BitConverter.GetBytes(b), 0, data, 8, 4);
            Array.Copy(BitConverter.GetBytes(a), 0, data, 12, 4);
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
            if (count != 16)
            {
                throw new FormatException($"VectorColor requires 16 bytes, got {count}");
            }

            if (Helpers.HasLeft(buffer, offset, count, 16) == false)
            {
                throw new System.IO.EndOfStreamException("VectorColor requires 16 bytes");
            }

            read = 16;

            var r = (byte)(BitConverter.ToSingle(buffer, offset + 0) * 255.0f);
            var g = (byte)(BitConverter.ToSingle(buffer, offset + 4) * 255.0f);
            var b = (byte)(BitConverter.ToSingle(buffer, offset + 8) * 255.0f);
            var a = (byte)(BitConverter.ToSingle(buffer, offset + 12) * 255.0f);

            writer.WriteString($"#{r:X2}{g:X2}{b:X2}{a:X2}");
        }
    }
}
