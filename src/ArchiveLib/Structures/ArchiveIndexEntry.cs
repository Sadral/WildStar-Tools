// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace ArchiveLib.Structures
{
    class ArchiveIndexEntry
    {
        public uint FolderEntryCount { get; set; }
        public uint FileEntryCount   { get; set; }

        public Dictionary<uint, FolderEntry> Folders;
        public List<FileEntry> Files;
    }
}
