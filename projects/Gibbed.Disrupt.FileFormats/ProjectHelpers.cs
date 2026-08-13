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
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Gibbed.Disrupt.FileFormats
{
    public static class ProjectHelpers
    {
        public static string Modifier(string s)
        {
            return s.Replace(@"/", @"\");
        }

        public static ProjectData.Project LoadProject(string projectName = null)
        {
            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            basePath = basePath != null ? Path.Combine(basePath, "projects") : "projects";

            if (!Directory.Exists(basePath))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(projectName))
            {
                var projectBase = Path.Combine(basePath, projectName.Trim());
                foreach (var ext in new[] { ".json", ".xml" })
                {
                    var projectPath = projectBase + ext;
                    if (File.Exists(projectPath))
                    {
                        return ProjectData.Project.Load(projectPath);
                    }
                }
                return null;
            }

            var currentPath = Path.Combine(basePath, "current.txt");
            if (File.Exists(currentPath))
            {
                var name = File.ReadAllText(currentPath).Trim();
                var projectBase = Path.Combine(basePath, name);
                foreach (var ext in new[] { ".json", ".xml" })
                {
                    var projectPath = projectBase + ext;
                    if (File.Exists(projectPath))
                    {
                        return ProjectData.Project.Load(projectPath);
                    }
                }
            }

            return null;
        }

        public static void LoadListsFileNames<T>(
            this ProjectData.Project project,
            Func<string, T> hasher,
            out ProjectData.HashList<T> hashList)
        {
            hashList = project.LoadLists("*.filelist", hasher, Modifier);
        }
    }

    public interface INameLookup<THash>
    {
        string this[THash hash] { get; }
    }

    public class NfoNameLookup<THash> : INameLookup<THash>
    {
        private readonly Dictionary<string, string> _map;

        public NfoNameLookup(BigFileInfo nfo)
        {
            _map = new Dictionary<string, string>();

            foreach (var entry in nfo.Entries)
            {
                if (string.IsNullOrEmpty(entry.Path))
                {
                    continue;
                }

                var path = entry.Path.Replace("/", "\\").TrimStart('\\');
                _map[entry.Crc] = path;
            }
        }

        public string this[THash hash]
        {
            get
            {
                string key = string.Format(CultureInfo.InvariantCulture, "{0}", hash);
                _map.TryGetValue(key, out var value);
                return value;
            }
        }
    }

    public class HashListLookupAdapter<THash> : INameLookup<THash>
    {
        private readonly ProjectData.HashList<THash> _inner;

        public HashListLookupAdapter(ProjectData.HashList<THash> inner)
        {
            _inner = inner;
        }

        public string this[THash hash]
        {
            get { return _inner[hash]; }
        }
    }
}
