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
using System.IO;
using System.Linq;
using System.Text;
using Gibbed.Disrupt.FileFormats;
using Gibbed.ProjectData;
using NDesk.Options;
using Big = Gibbed.Disrupt.FileFormats.Big;

namespace Gibbed.Disrupt.Packing
{
    public static class RebuildFileLists<TArchive, THash>
        where TArchive : Big.IArchive<THash>, new()
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        private static string GetListPath(string installPath, string inputPath)
        {
            installPath = installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToLowerInvariant();
            inputPath = inputPath.ToLowerInvariant();

            if (inputPath.StartsWith(installPath) == false)
            {
                return null;
            }

            var baseName = inputPath.Substring(installPath.Length + 1);

            string outputPath;
            outputPath = Path.Combine("files", baseName);
            outputPath = Path.ChangeExtension(outputPath, ".filelist");
            return outputPath;
        }

        public static void Main(string[] args, string projectName)
        {
            Main(args, projectName, null);
        }

        public static void Main(string[] args, string projectName, Big.TryGetHashOverride<THash> tryGetHashOverride)
        {
            bool showHelp = false;
            string installPathOverride = null;
            string outputDirOverride = null;
            string dataPrefix = null;

            var options = new OptionSet()
            {
                { "p|install-path=", "override install path (skips registry detection)", v => installPathOverride = v },
                { "o|output-dir=", "override output directory for filelists", v => outputDirOverride = v },
                { "d|data-prefix=", "remap data directory prefix in filelist paths (e.g. data_win64 → data_xenon)", v => dataPrefix = v },
                { "h|help", "show this message and exit", v => showHelp = v != null },
            };

            List<string> extras;

            try
            {
                extras = options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("{0}: ", GetExecutableName());
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());
                return;
            }

            if (extras.Count != 0 || showHelp == true)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+", GetExecutableName());
                Console.WriteLine();
                Console.WriteLine("Rebuilds per-archive file lists from the game's FAT archives.");
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            Console.WriteLine("Loading project...");

            var project = ProjectHelpers.LoadProject(projectName);
            if (project == null)
            {
                Console.WriteLine("Nothing to do: no active project loaded.");
                return;
            }
            byte? nameHashVersion = null;
            HashList<THash> knownHashes = null;

            var installPath = installPathOverride ?? project.InstallPath;
            var listsPath = outputDirOverride ?? project.ListsPath;

            if (installPath == null)
            {
                Console.WriteLine("Could not detect install path.");
                Console.WriteLine("Tip: use --install-path=<path> to specify manually.");
                return;
            }

            if (installPathOverride != null)
            {
                Console.WriteLine("Using install path: {0}", installPath);
            }

            if (outputDirOverride != null)
            {
                Console.WriteLine("Using output directory: {0}", listsPath);
                Directory.CreateDirectory(listsPath);
            }

            if (listsPath == null)
            {
                Console.WriteLine("Could not detect lists path.");
                return;
            }

            Console.WriteLine("Searching for archives...");
            var fatPaths = new List<string>();
            fatPaths.AddRange(Directory.GetFiles(installPath, "*.fat.bak", SearchOption.AllDirectories));
            foreach (var fatPath in Directory.GetFiles(installPath, "*.fat", SearchOption.AllDirectories))
            {
                if (fatPaths.Contains(fatPath + ".bak") == false)
                {
                    fatPaths.Add(fatPath);
                }
            }
            fatPaths.Sort();

            var outputPaths = new List<string>();

            var tracking = new Tracking();
            TArchive lastFat = default;

            Console.WriteLine("Processing...");
            BigFileV5.ClearSanityWarnings();
            for (int i = 0; i < fatPaths.Count; i++)
            {
                var fatPath = fatPaths[i];
                var inputPath = fatPath;
                if (fatPath.EndsWith(".bak") == true)
                {
                    fatPath = fatPath.Substring(0, fatPath.Length - 4);
                }

                var outputPath = GetListPath(installPath, fatPath);
                if (outputPath == null)
                {
                    throw new InvalidOperationException();
                }

                Console.WriteLine(outputPath);
                outputPath = Path.Combine(listsPath, outputPath);

                if (outputPaths.Contains(outputPath) == true)
                {
                    throw new InvalidOperationException();
                }

                outputPaths.Add(outputPath);

                var fat = new TArchive();
                try
                {
                    using (var input = File.OpenRead(inputPath))
                    {
                        fat.Deserialize(input);
                    }
                }
                catch (FormatException ex)
                {
                    Console.Error.WriteLine("WARNING: skipping {0} ({1})", fatPath, ex.Message);
                    continue;
                }
                catch (EndOfStreamException ex)
                {
                    Console.Error.WriteLine("WARNING: skipping {0} ({1})", fatPath, ex.Message);
                    continue;
                }

                if (nameHashVersion == null)
                {
                    nameHashVersion = fat.NameHashVersion;
                    lastFat = fat;

                    Console.WriteLine("Loading file lists for version {0}...", nameHashVersion);

                    THash wrappedComputeNameHash(string s) =>
                        fat.ComputeNameHash(s, tryGetHashOverride);

                    if (dataPrefix != null)
                    {
                        // Remap filelist paths: dataPrefix → actual archive base directory name
                        // e.g. data_win64 → data_xenon for X360 archives
                        string archiveBase = Path.GetFileName(installPath.TrimEnd(
                            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                        string PrefixModifier(string line)
                        {
                            line = line.Replace(@"/", @"\");
                            if (line.StartsWith(dataPrefix + @"\", StringComparison.OrdinalIgnoreCase))
                            {
                                return archiveBase + line.Substring(dataPrefix.Length);
                            }
                            return line;
                        }
                        Console.WriteLine("Remapping filelist prefix: {0} → {1}", dataPrefix, archiveBase);
                        knownHashes = project.LoadLists("*.filelist", wrappedComputeNameHash, PrefixModifier);
                    }
                    else
                    {
                        project.LoadListsFileNames(wrappedComputeNameHash, out knownHashes);
                    }
                }

                if (knownHashes == null)
                {
                    throw new InvalidOperationException();
                }

                HandleEntries(
                    fat,
                    knownHashes,
                    tracking,
                    outputPath);
            }

            var breakdown = new Breakdown()
            {
                Known = tracking.Names.Distinct().Count(),
                Total = tracking.Hashes.Distinct().Count(),
            };
            Console.WriteLine("{0}", breakdown);
            var statusPath = Path.Combine(listsPath, "files", "status.txt");
            using (var output = new StreamWriter(statusPath, false, new UTF8Encoding(false)))
            {
                output.WriteLine("{0}", breakdown);

                // TODO(gibbed): breakdown all archives individually
            }

            WriteFailures(listsPath, knownHashes, fatPaths, lastFat);
        }

        private static void WriteFailures(
            string listsPath,
            HashList<THash> knownHashes,
            List<string> fatPaths,
            TArchive fat)
        {
            var failures = knownHashes.GetFailures().ToList();
            var removed = BigFileV5.RemovedEntries;
            if (failures.Count == 0 && removed.Count == 0)
            {
                return;
            }

            // Use the first archive's renderer to format hash values.
            string Render(THash hash)
            {
                var probe = new TArchive();
                return probe.RenderNameHash(hash);
            }

            var failurePath = Path.Combine(listsPath, "files", "failure.txt");
            using (var output = new StreamWriter(failurePath, false, new UTF8Encoding(false)))
            {
                output.WriteLine("; {0} hash collisions (filtered from file lists)", failures.Count);
                foreach (var failure in failures.OrderBy(f => f.Key.ToString()))
                {
                    var names = failure.Value.Distinct().ToArray();
                    output.WriteLine("{0}: {1}", Render(failure.Key), string.Join(" vs ", names));
                }

                if (removed.Count > 0)
                {
                    output.WriteLine();
                    output.WriteLine("; {0} entries removed (failed FAT entry sanity checks)", removed.Count);
                    foreach (var entry in removed.OrderBy(e => e.Reason))
                    {
                        // BigFileV5.RemovedEntries stores ulong, but THash may be uint (WD1).
                        // Upper 32 bits are always zero for 32-bit archives, so truncation is safe.
                        output.WriteLine("{0}: {1}",
                            fat.RenderNameHash((THash)(object)entry.Hash), entry.Reason);
                    }
                }
            }

            Console.WriteLine("Wrote {0} hash collisions and {1} removed entries to {2}",
                failures.Count, removed.Count, failurePath);
        }

        private static void HandleEntries(
            TArchive fat,
            HashList<THash> knownHashes,
            Tracking tracking,
            string outputPath)
        {
            var localBreakdown = new Breakdown();

            var localNames = new List<string>();
            var localHashes = fat.Entries
                .Select(e => e.NameHash)
                .Concat(GetDependentHashes(fat))
                .Distinct()
                .ToArray();
            foreach (var hash in localHashes)
            {
                var name = knownHashes[hash];
                if (name != null)
                {
                    localNames.Add(name);
                }
                localBreakdown.Total++;
            }

            tracking.Hashes.AddRange(localHashes);
            tracking.Names.AddRange(localNames);

            var distinctLocalNames = localNames.Distinct().ToArray();
            localBreakdown.Known += distinctLocalNames.Length;

            var outputParent = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputParent) == false)
            {
                Directory.CreateDirectory(outputParent);
            }

            using (var writer = new StringWriter())
            {
                writer.WriteLine("; {0}", localBreakdown);
                if (fat is Big.IDependentArchive<THash> dependentFat)
                {
                    if (dependentFat.HasArchiveHash == true || dependentFat.Dependencies.Count > 0)
                    {
                        writer.WriteLine(";");
                    }
                    if (dependentFat.HasArchiveHash == true)
                    {
                        writer.WriteLine("; archive={0}",
                            knownHashes[dependentFat.ArchiveHash] ??
                                fat.RenderNameHash(dependentFat.ArchiveHash));
                    }
                    foreach (var dependency in dependentFat.Dependencies)
                    {
                        writer.WriteLine("; dependency={0} @ {1}",
                            knownHashes[dependency.ArchiveHash] ??
                                fat.RenderNameHash(dependency.ArchiveHash),
                            knownHashes[dependency.NameHash] ??
                                fat.RenderNameHash(dependency.NameHash));
                    }
                }
                foreach (string name in distinctLocalNames.OrderBy(dn => dn))
                {
                    writer.WriteLine(name);
                }
                writer.Flush();

                using (var output = new StreamWriter(outputPath, false, new UTF8Encoding(false)))
                {
                    output.Write(writer.GetStringBuilder());
                }
            }
        }

        private static IEnumerable<THash> GetDependentHashes(TArchive fat)
        {
            if (fat is Big.IDependentArchive<THash> dependentFat)
            {
                if (dependentFat.HasArchiveHash == true)
                {
                    yield return dependentFat.ArchiveHash;
                }

                foreach (var dependency in dependentFat.Dependencies)
                {
                    yield return dependency.ArchiveHash;
                    yield return dependency.NameHash;
                }
            }
        }

        internal class Tracking
        {
            public readonly List<THash> Hashes = new List<THash>();
            public readonly List<string> Names = new List<string>();
        }

        internal class Breakdown
        {
            public long Known = 0;
            public long Total = 0;

            public int Percent
            {
                get
                {
                    return this.Total == 0
                        ? 0
                        : (int)Math.Floor(((float)this.Known / this.Total) * 100.0f);
                }
            }

            public override string ToString()
            {
                return $"{this.Known}/{this.Total} ({this.Percent}%)";
            }
        }
    }
}
