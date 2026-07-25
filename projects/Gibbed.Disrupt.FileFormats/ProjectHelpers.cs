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

namespace Gibbed.Disrupt.FileFormats
{
    public static class ProjectHelpers
    {
        public static string Modifier(string s)
        {
            return s.Replace(@"/", @"\");
        }

        public static void LoadListsFileNames<T>(
            this ProjectData.Manager manager,
            Func<string, T> hasher,
            out ProjectData.HashList<T> hashList)
        {
            hashList = manager.LoadLists("*.filelist", hasher, Modifier);
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
