using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Gibbed.Disrupt.BinaryObjectInfo;
using Gibbed.Disrupt.FileFormats;

namespace Gibbed.Disrupt.DefinitionsFixer;

internal class Program
{
    private static InfoManager? _infoManager;

    // Hash → human-readable name mappings
    private static readonly (string Hash, string Name)[] HashReplacements =
    [
        ("72DE4948", "libobj"),
        ("4666D46C", "obj"),
        ("38B17551", "End"),
        ("9301DFBD", "text_matMaterial"),
        ("7EBF8F6B", "FOV"),
        ("F390740B", "PoV"),
        ("D0558B01", "Fov"),
        ("52C9BBAB", "Modifier"),
        ("B006ED20", "RotationPitchMin"),
        ("8C0BD279", "RotationPitchMax"),
        ("4A270098", "RotationYawMin"),
        ("762A3FC1", "RotationYawMax"),
        ("1C33C293", "Settings"),
        ("DD510595", "MixerContexts"),
        ("2DE0BCE2", "Context"),
        ("A16E4285", "cameracontextContextParameters"),
        ("FF22650F", "text_archShopKeeper"),
        ("32F4B348", "text_archCashRegister"),
        ("9ED75256", "text_sPlayerGreetingsTrigger"),
        ("CABE74B9", "text_fileInsideShopMenu"),
        ("1C71844F", "text_fileFugitiveMediaBroadcastInProgress"),
        ("5B7B9C2C", "text_fileConsumingAdrenalineCameraAnimation"),
        ("94FC20C4", "text_availabilityruleRuleType"),
        ("3C3C04B8", "AmbientReflectionOverride"),
        ("066B6B53", "gradAmbientReflectionTopColor"),
        ("4394E17F", "curveAmbientReflectionTopIntensity"),
        ("29C88D7F", "gradAmbientReflectionBottomColor"),
        ("90B8652F", "curveAmbientReflectionBottomIntensity"),
        ("F32C0E1C", "Knots"),
        ("9A152447", "Knot"),
        ("A79767ED", "Color"),
    ];

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: Gibbed.Disrupt.DefinitionsFixer <file.xml|folder> [...]");
            return;
        }

        // Load project using current.txt (same as ConvertBinaryObject)
        var project = ProjectHelpers.LoadProject();
        if (project != null)
        {
            Console.WriteLine($"Loaded project: {project.Name}");

            // Load string dictionaries from project's ListsPath
            if (Directory.Exists(project.ListsPath))
            {
                StringLookup.Load(project.ListsPath);
                Console.WriteLine($"Loaded string dictionaries from: {project.ListsPath}");
            }

            // Load binary class/object definitions
            try
            {
                _infoManager = InfoManager.Load(project.ListsPath);
                Console.WriteLine($"Loaded class definitions from: {project.ListsPath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: could not load definitions: {ex.Message}");
            }
        }
        else
        {
            Console.Error.WriteLine("Warning: no active project (check bin/projects/current.txt).");
        }

        foreach (var arg in args)
        {
            if (Directory.Exists(arg))
            {
                ProcessFolder(arg);
            }
            else if (Path.GetExtension(arg).Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                ProcessXML(arg);
            }
        }
    }

    private static void ProcessFolder(string folder)
    {
        // Place output as sibling, not inside the source dir
        var sourceDir = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
        var outputDir = sourceDir + "_output";
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }

        CopyDirectory(sourceDir, outputDir);

        var xmlFiles = Directory.GetFiles(outputDir, "*.xml", SearchOption.AllDirectories);        foreach (var xmlFile in xmlFiles)
        {
            ProcessXML(xmlFile);
        }

        Console.WriteLine($"Processed folder: {folder} → {outputDir}");
    }

    private static void ProcessXML(string xmlFile)
    {
        try
        {
            var lines = File.ReadAllLines(xmlFile);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = ProcessLine(lines[i], i > 0 ? lines[i - 1] : null);
            }
            File.WriteAllLines(xmlFile, lines);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error processing {xmlFile}: {ex.Message}");
        }
    }

    private static string ProcessLine(string line, string? prevLine)
    {
        // Hash → name replacements (hardcoded)
        foreach (var (hash, name) in HashReplacements)
        {
            if (line.Contains($"hash=\"{hash}\""))
            {
                line = line.Replace($"hash=\"{hash}\"", $"name=\"{name}\"");
            }
        }

        // StringLookup fallback — resolve any remaining hashes via strings.txt/strings.user.txt/current.txt
        var hashMatch = Regex.Match(line, @"hash=""([0-9A-Fa-f]{8})""");
        if (hashMatch.Success)
        {
            var hexHash = hashMatch.Groups[1].Value;
            if (uint.TryParse(hexHash, NumberStyles.HexNumber, null, out var numericHash))
            {
                var resolved = StringLookup.Resolve(numericHash);
                if (resolved != null)
                {
                    line = line.Replace($"hash=\"{hexHash}\"", $"name=\"{resolved}\"");
                }
            }
        }

        // Vector4 → Vector type fixes for known fields
        if (line.Contains("type=\"Vector4\""))
        {
            if ((line.Contains("name=\"Value\"") || line.Contains("name=\"Info\"")))
            {
                line = line.Replace("type=\"Vector4\"", "type=\"Vector\"");
            }
        }

        // Enum → BinHex for specific fields
        if (line.Contains("name=\"selPoseDefinitionId\"") && line.Contains("type=\"Enum\""))
        {
            var val = ExtractFieldValue(line, "selPoseDefinitionId", "Enum");
            if (val != null)
            {
                var hex = EnumToHex(val);
                line = ReplaceFieldType(line, "selPoseDefinitionId", "Enum", "BinHex", hex);
            }
        }
        if (line.Contains("name=\"selMaterialOverridesId\"") && line.Contains("type=\"Enum\""))
        {
            var val = ExtractFieldValue(line, "selMaterialOverridesId", "Enum");
            if (val != null)
            {
                var hex = EnumToHex(val);
                line = ReplaceFieldType(line, "selMaterialOverridesId", "Enum", "BinHex", hex);
            }
        }

        // BinHex → Boolean for known hashes
        if (line.Contains("type=\"BinHex\""))
        {
            // Boolean fields
            if (line.Contains("hash=\"045DA267\""))
                line = ConvertBinHexToNamedField(line, "045DA267", "bFovClampAtMax", "Boolean", BinHexToBoolean);
            if (line.Contains("hash=\"6198EB54\""))
                line = ConvertBinHexToNamedField(line, "6198EB54", "bKeepPitchYawRelativeToRef", "Boolean", BinHexToBoolean);
            if (line.Contains("hash=\"85B91A91\""))
                line = ConvertBinHexToNamedField(line, "85B91A91", "bFollowPitchUnderObject", "Boolean", BinHexToBoolean);
            if (line.Contains("hash=\"194DC297\""))
                line = ConvertBinHexToNamedField(line, "194DC297", "bAmbientReflectionOverrideFactorOverride", "Boolean", BinHexToBoolean);
            if (line.Contains("hash=\"574ED155\""))
                line = ConvertBinHexToNamedField(line, "574ED155", "bAmbientReflectionTopColorOverride", "Boolean", BinHexToBoolean);
            if (line.Contains("hash=\"6BA3AF1E\""))
                line = ConvertBinHexToNamedField(line, "6BA3AF1E", "bAmbientReflectionTopIntensityOverride", "Boolean", BinHexToBoolean);
            if (line.Contains("hash=\"837AF0AA\""))
                line = ConvertBinHexToNamedField(line, "837AF0AA", "bAmbientReflectionBottomColorOverride", "Boolean", BinHexToBoolean);
            if (line.Contains("hash=\"B6BD6002\""))
                line = ConvertBinHexToNamedField(line, "B6BD6002", "bAmbientReflectionBottomIntensityOverride", "Boolean", BinHexToBoolean);

            // Float fields
            if (line.Contains("hash=\"212E2BE6\""))
                line = ConvertBinHexToNamedField(line, "212E2BE6", "fPitchAngleUnderObject", "Float", BinHexToFloat);
            if (line.Contains("hash=\"75FBCDC9\""))
                line = ConvertBinHexToNamedField(line, "75FBCDC9", "fValue", "Float", BinHexToFloat);
            if (line.Contains("hash=\"8A791D8C\""))
                line = ConvertBinHexToNamedField(line, "8A791D8C", "fContextValue", "Float", BinHexToFloat);
            if (line.Contains("hash=\"089A0D57\""))
                line = ConvertBinHexToNamedField(line, "089A0D57", "fAmbientReflectionOverrideFactor", "Float", BinHexToFloat);
            if (line.Contains("hash=\"BF5A86A3\""))
                line = ConvertBinHexToNamedField(line, "BF5A86A3", "Position", "Float", BinHexToFloat);

            // Enum fields
            if (line.Contains("hash=\"C7C0DC81\""))
                line = ConvertBinHexToNamedField(line, "C7C0DC81", "selModifierType", "Enum", BinHexToEnum);
            if (line.Contains("hash=\"94D3B6E3\""))
                line = ConvertBinHexToNamedField(line, "94D3B6E3", "selValueType", "Enum", BinHexToEnum);
            if (line.Contains("hash=\"FE15C80B\""))
                line = ConvertBinHexToNamedField(line, "FE15C80B", "selMixerReference", "Enum", BinHexToEnum);

            // Vector2 fields
            if (line.Contains("hash=\"3145968B\""))
                line = ConvertBinHexToNamedField(line, "3145968B", "vec2ValueCurve", "Vector2", BinHexToVector2);

            // Vector4 → Vector fields
            if (line.Contains("hash=\"DCB67730\""))
                line = ConvertBinHexToNamedField(line, "DCB67730", "Value", "Vector", BinHexToVector4);
            if (line.Contains("hash=\"6BBB9E69\""))
                line = ConvertBinHexToNamedField(line, "6BBB9E69", "Info", "Vector", BinHexToVector4);

            // VectorColor fields
            if (line.Contains("name=\"clrLightColor\""))
                line = ConvertBinHexToNamedField(line, "clrLightColor", "clrLightColor", "VectorColor", BinHexToVectorColor, useHash: false);

            // hidNumKnots BinHex → Enum
            if (line.Contains("name=\"hidNumKnots\""))
                line = ConvertBinHexToEnumByName(line, "hidNumKnots");

            // Type BinHex → Enum
            if (line.Contains("name=\"Type\""))
                line = ConvertBinHexToEnumByName(line, "Type");

            // Hash-based hidNumKnots and Type
            if (line.Contains("hash=\"AECEF355\""))
                line = ConvertBinHexToNamedField(line, "AECEF355", "hidNumKnots", "Enum", BinHexToEnum);
            if (line.Contains("hash=\"2CECF817\""))
                line = ConvertBinHexToNamedField(line, "2CECF817", "Type", "Enum", BinHexToEnum);
        }

        // Vector type fixes based on context
        if (line.Contains("name=\"vectorValue\"") && line.Contains("type=\"Vector") && prevLine != null)
        {
            if ((prevLine.Contains("IdealMin") || prevLine.Contains("IdealMax")) && line.Contains("type=\"Vector3\""))
            {
                line = line.Replace("type=\"Vector3\"", "type=\"Vector\"");
            }
        }
        if (line.Contains("name=\"vec2Value\"") && line.Contains("type=\"Vector") && prevLine != null)
        {
            if (prevLine.Contains("FocusRange") && line.Contains("type=\"Vector2\""))
            {
                line = line.Replace("type=\"Vector2\"", "type=\"Vector\"");
            }
        }

        return line;
    }

    // --- Conversion helpers ---

    private static string? ExtractFieldValue(string line, string fieldName, string typeName)
    {
        var match = Regex.Match(line,
            $@"<field name=""{fieldName}"" type=""{typeName}"">(.*?)</field>");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string ReplaceFieldType(string line, string fieldName, string oldType, string newType, string newValue)
    {
        return Regex.Replace(line,
            $@"(<field name=""{fieldName}"") type=""{oldType}"">(.*?)</field>",
            $@"$1 type=""{newType}"">{newValue}</field>");
    }

    private static string ConvertBinHexToNamedField(
        string line, string hashOrName, string newName, string newType,
        Func<string, string> converter, bool useHash = true)
    {
        string pattern;
        if (useHash)
        {
            pattern = $@"<field hash=""{hashOrName}"" type=""BinHex"">(.*?)</field>";
        }
        else
        {
            pattern = $@"<field name=""{hashOrName}"" type=""BinHex"">(.*?)</field>";
        }

        var match = Regex.Match(line, pattern);
        if (!match.Success)
            return line;

        var hexValue = match.Groups[1].Value;
        if (string.IsNullOrEmpty(hexValue) || line.Contains("type=\"BinHex\" />"))
        {
            // Empty/self-closing — use default
            return Regex.Replace(line, pattern,
                useHash
                    ? $@"<field name=""{newName}"" type=""{newType}"">0</field>"
                    : $@"<field name=""{newName}"" type=""{newType}"">0</field>");
        }

        var converted = converter(hexValue);
        return Regex.Replace(line, pattern,
            $@"<field name=""{newName}"" type=""{newType}"">{converted}</field>");
    }

    private static string ConvertBinHexToEnumByName(string line, string fieldName)
    {
        var pattern = $@"<field name=""{fieldName}"" type=""BinHex"">(.*?)</field>";
        var selfClosePattern = $@"<field name=""{fieldName}"" type=""BinHex"" />";

        if (line.Contains("type=\"BinHex\" />"))
        {
            return Regex.Replace(line, selfClosePattern,
                $@"<field name=""{fieldName}"" type=""Enum"">0</field>");
        }

        var match = Regex.Match(line, pattern);
        if (!match.Success)
            return line;

        var hexValue = match.Groups[1].Value;
        string enumValue;
        try
        {
            enumValue = byte.Parse(hexValue, NumberStyles.HexNumber).ToString();
        }
        catch
        {
            return line;
        }

        return Regex.Replace(line, pattern,
            $@"<field name=""{fieldName}"" type=""Enum"">{enumValue}</field>");
    }

    // --- Binary conversion functions ---

    /// <summary>Swap byte pairs in a hex string (little-endian ↔ big-endian).</summary>
    private static string SwapBytes(string hex)
    {
        if (hex.Length % 2 != 0)
            return hex;

        var bytes = new string[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = hex.Substring(i * 2, 2);
        }
        Array.Reverse(bytes);
        return string.Concat(bytes);
    }

    private static string BinHexToBoolean(string hex)
    {
        return hex.Contains("01") ? "True" : "False";
    }

    private static string BinHexToFloat(string hex)
    {
        var swapped = SwapBytes(hex);
        var raw = uint.Parse(swapped, NumberStyles.HexNumber);
        var bytes = BitConverter.GetBytes(raw);
        return BitConverter.ToSingle(bytes, 0).ToString(CultureInfo.InvariantCulture);
    }

    private static string BinHexToEnum(string hex)
    {
        var swapped = SwapBytes(hex);
        return byte.Parse(swapped, NumberStyles.HexNumber).ToString();
    }

    private static string BinHexToVector2(string hex)
    {
        var swapped = SwapBytes(hex);
        var x = ExtractFloat(swapped, 0);
        var y = ExtractFloat(swapped, 1);
        return $"{x},{y}";
    }

    private static string BinHexToVector4(string hex)
    {
        var swapped = SwapBytes(hex);
        var x = ExtractFloat(swapped, 0);
        var y = ExtractFloat(swapped, 1);
        var z = ExtractFloat(swapped, 2);
        var w = ExtractFloat(swapped, 3);
        return $"{w},{z},{y},{x}";
    }

    private static string BinHexToVectorColor(string hex)
    {
        var swapped = SwapBytes(hex);
        var r = ExtractFloat(swapped, 0) * 255f;
        var g = ExtractFloat(swapped, 1) * 255f;
        var b = ExtractFloat(swapped, 2) * 255f;
        return $"{b},{g},{r}";
    }

    private static float ExtractFloat(string hex, int componentIndex)
    {
        var start = componentIndex * 8;
        if (start + 8 > hex.Length)
            return 0f;

        var segment = hex.Substring(start, 8);
        var raw = uint.Parse(segment, NumberStyles.HexNumber);
        var bytes = BitConverter.GetBytes(raw);
        return BitConverter.ToSingle(bytes, 0);
    }

    private static string EnumToHex(string decimalValue)
    {
        if (decimalValue == "-1")
            return "FF";

        var num = long.Parse(decimalValue);
        var hex = num.ToString("X8");
        hex = SwapBytes(hex);

        // Strip leading FFFFFFFF padding
        hex = hex.Replace("FFFFFFFF", "");
        return hex;
    }

    // --- File utilities ---

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)));
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            // Skip _output directories to avoid recursion
            if (dirName.EndsWith("_output"))
                continue;
            CopyDirectory(dir, Path.Combine(destDir, dirName));
        }
    }
}
