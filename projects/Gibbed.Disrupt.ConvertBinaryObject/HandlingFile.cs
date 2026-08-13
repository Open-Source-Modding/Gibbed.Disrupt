using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Gibbed.Disrupt.ConvertBinaryObject
{
    public class HandlingFile
    {
        public List<HandlingParameter> Parameters { get; } = new();

        public void Deserialize(Stream input)
        {
            var reader = new BinaryReader(input);

            while (reader.BaseStream.Position + 12 <= reader.BaseStream.Length)
            {
                var param = new HandlingParameter
                {
                    Index = reader.ReadUInt32(),
                    Type = (ParameterType)reader.ReadUInt32(),
                    RawValue = reader.ReadUInt32(),
                };
                Parameters.Add(param);
            }
        }

        public void Serialize(Stream output)
        {
            var writer = new BinaryWriter(output);
            foreach (var p in Parameters)
            {
                writer.Write(p.Index);
                writer.Write((uint)p.Type);
                writer.Write(p.RawValue);
            }
        }

        public XDocument ToXml()
        {
            var doc = new XDocument(
                new XElement("handling",
                    new XAttribute("parameters", Parameters.Count)
                )
            );
            var root = doc.Root;

            foreach (var p in Parameters)
            {
                var el = new XElement("parameter",
                    new XAttribute("index", p.Index)
                );

                if (p.Type == ParameterType.Float)
                {
                    el.Add(new XAttribute("type", "Float"));
                    el.Add(new XAttribute("value", p.FloatValue));
                }
                else
                {
                    el.Add(new XAttribute("type", "UInt"));
                    el.Add(new XAttribute("value", p.UIntValue));
                }

                if (string.IsNullOrEmpty(p.Name) == false)
                {
                    el.Add(new XAttribute("name", p.Name));
                }

                root.Add(el);
            }

            return doc;
        }

        public static HandlingFile FromXml(XDocument doc)
        {
            var handling = new HandlingFile();
            var root = doc.Root;

            foreach (var el in root.Elements("parameter"))
            {
                var param = new HandlingParameter
                {
                    Index = (uint)el.Attribute("index"),
                    Type = (string)el.Attribute("type") == "Float" ? ParameterType.Float : ParameterType.UInt,
                    Name = (string)el.Attribute("name"),
                };

                var valueAttr = el.Attribute("value");
                if (param.Type == ParameterType.Float)
                {
                    param.FloatValue = (float)valueAttr;
                }
                else
                {
                    param.UIntValue = (uint)valueAttr;
                }

                handling.Parameters.Add(param);
            }

            return handling;
        }
    }

    public class HandlingParameter
    {
        public uint Index { get; set; }
        public ParameterType Type { get; set; }
        public uint RawValue { get; set; }
        public string Name { get; set; }

        public float FloatValue
        {
            get
            {
                var bytes = BitConverter.GetBytes(RawValue);
                return BitConverter.ToSingle(bytes, 0);
            }
            set
            {
                RawValue = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
            }
        }

        public uint UIntValue
        {
            get => RawValue;
            set => RawValue = value;
        }
    }

    public enum ParameterType : uint
    {
        UInt = 1,
        Float = 2,
    }
}
